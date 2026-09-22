using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.FinancialDocuments.Commands;

/// <summary>
/// Receive/create a financial document. Sets ReceiptStatus=received only —
/// does not invent Cost/Revenue (C-003/C-004) and does not imply Accepted/Matched.
/// </summary>
public sealed record ReceiveFinancialDocumentCommand(
    string DocumentType,
    string DocumentNo,
    string Direction,
    decimal TotalAmount,
    string CurrencyCode,
    DateOnly? DocumentDate,
    Guid? CounterpartyId,
    Guid? BillId,
    string? Notes,
    string? SourceSystem,
    string? ExternalId,
    string? IdempotencyKey = null) : IRequest<Guid>;

public sealed class ReceiveFinancialDocumentCommandValidator : AbstractValidator<ReceiveFinancialDocumentCommand>
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        FinancialDocumentTypes.Dn,
        FinancialDocumentTypes.Invoice,
        FinancialDocumentTypes.CreditNote,
        FinancialDocumentTypes.DebitNote,
        FinancialDocumentTypes.Other
    };

    public ReceiveFinancialDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .NotEmpty().WithMessage("Loại chứng từ không được để trống.")
            .Must(t => AllowedTypes.Contains(t))
            .WithMessage("Loại chứng từ phải là dn, invoice, credit_note, debit_note hoặc other.");
        RuleFor(x => x.DocumentNo)
            .NotEmpty().WithMessage("Số chứng từ không được để trống.")
            .MaximumLength(128).WithMessage("Số chứng từ không được vượt quá 128 ký tự.");
        RuleFor(x => x.Direction)
            .NotEmpty().WithMessage("Chiều chứng từ không được để trống.")
            .Must(d => d is FinancialDocumentDirections.Payable or FinancialDocumentDirections.Receivable)
            .WithMessage("Chiều chứng từ phải là payable hoặc receivable.");
        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Tổng tiền chứng từ không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
        RuleFor(x => x.SourceSystem).MaximumLength(64).When(x => x.SourceSystem is not null);
        RuleFor(x => x.ExternalId).MaximumLength(128).When(x => x.ExternalId is not null);
    }
}

public sealed class ReceiveFinancialDocumentCommandHandler : IRequestHandler<ReceiveFinancialDocumentCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly DocumentOptions _options;
    private readonly IPartyDirectoryService _parties;

    public ReceiveFinancialDocumentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IOptions<DocumentOptions> options,
        IPartyDirectoryService parties)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _options = options.Value;
        _parties = parties;
    }

    public async Task<Guid> Handle(ReceiveFinancialDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();
        if (idempotencyKey is not null)
        {
            var prior = await _db.IdempotencyRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Scope == "financial_document" && r.Key == idempotencyKey, cancellationToken);
            if (prior is not null)
            {
                return prior.ObjectId;
            }
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var documentType = request.DocumentType.Trim().ToLowerInvariant();
        var documentNo = request.DocumentNo.Trim();

        if (request.BillId.HasValue)
        {
            var bill = await _db.Bills.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken);
            if (bill is null)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }
        }

        if (request.CounterpartyId.HasValue)
        {
            var direction = request.Direction.Trim().ToLowerInvariant();
            var roles = direction == FinancialDocumentDirections.Payable
                ? PartyRoleCodes.VendorSide
                : PartyRoleCodes.CustomerSide;
            var purpose = direction == FinancialDocumentDirections.Payable
                ? "nhận chứng từ phải trả"
                : "nhận chứng từ phải thu";
            await _parties.EnsureUsableAsync(
                request.CounterpartyId.Value,
                roles,
                purpose,
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.SourceSystem) && !string.IsNullOrWhiteSpace(request.ExternalId))
        {
            var existing = await _db.FinancialDocuments.AsNoTracking()
                .FirstOrDefaultAsync(
                    d => d.SourceSystem == request.SourceSystem && d.ExternalId == request.ExternalId,
                    cancellationToken);
            if (existing is not null)
            {
                return existing.Id;
            }
        }

        if (_options.EnforceDuplicateControl)
        {
            var duplicateQuery = _db.FinancialDocuments.AsNoTracking()
                .Where(d =>
                    d.DocumentType == documentType
                    && d.DocumentNo == documentNo
                    && d.RecordStatus == FinancialDocumentRecordStatuses.Active);

            duplicateQuery = request.CounterpartyId.HasValue
                ? duplicateQuery.Where(d => d.CounterpartyId == request.CounterpartyId)
                : duplicateQuery.Where(d => d.CounterpartyId == null);

            if (await duplicateQuery.AnyAsync(cancellationToken))
            {
                throw new ConflictAppException(
                    "Chứng từ trùng loại/số/đối tác trong thuê bao (IDX-006 / kiểm soát trùng).");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var document = new FinancialDocument
        {
            TenantId = tenantId,
            DocumentType = documentType,
            DocumentNo = documentNo,
            Direction = request.Direction.Trim().ToLowerInvariant(),
            TotalAmount = decimal.Round(request.TotalAmount, 4, MidpointRounding.AwayFromZero),
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            DocumentDate = request.DocumentDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            CounterpartyId = request.CounterpartyId,
            BillId = request.BillId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? null : request.SourceSystem.Trim(),
            ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim(),
            // Independent dimensions: receive does NOT accept or match (AC-005).
            ReceiptStatus = FinancialDocumentReceiptStatuses.Received,
            AcceptanceStatus = FinancialDocumentAcceptanceStatuses.NotAccepted,
            MatchingStatus = FinancialDocumentMatchingStatuses.Unmatched,
            ReceivedAt = now,
            ReceivedBy = _user.UserId,
            RecordStatus = FinancialDocumentRecordStatuses.Active
        };

        _db.FinancialDocuments.Add(document);
        if (idempotencyKey is not null)
        {
            _db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                TenantId = _tenantContext.TenantId!.Value,
                Scope = "financial_document",
                Key = idempotencyKey,
                ObjectId = document.Id
            });
        }
        // Explicit: do not create Cost or Revenue rows from document intake (C-003 / C-004).
        await _db.SaveChangesAsync(cancellationToken);
        return document.Id;
    }
}
