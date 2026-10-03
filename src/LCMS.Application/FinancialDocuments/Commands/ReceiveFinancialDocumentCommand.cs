using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.DocumentMatches;
using LCMS.Application.FinancialCloses;
using LCMS.Application.Fx;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.FinancialDocuments.Commands;

public sealed record SourceLineSelectionDto(
    Guid SourceId,
    string SourceType, // "cost" or "revenue"
    decimal Amount,
    Guid? BillId = null);

/// <summary>
/// Receive/create a financial document (PO 02/10/2026).
/// Supports Mode A (external_received) and Mode B (lcms_generated).
/// Does not invent Cost/Revenue (FD-R01 / C-003 / C-004) and maintains FX snapshot baseline.
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
    string? IdempotencyKey = null,
    string? Mode = null,
    IReadOnlyList<SourceLineSelectionDto>? SelectedSourceLines = null,
    decimal? ManualFxRate = null,
    string? FxOverrideReason = null) : IRequest<Guid>;

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
            .Must(d => d is FinancialDocumentDirections.Payable or FinancialDocumentDirections.Receivable or "ap" or "ar")
            .WithMessage("Chiều chứng từ phải là payable hoặc receivable.");
        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Tổng tiền chứng từ không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Mode)
            .Must(m => string.IsNullOrWhiteSpace(m) || FinancialDocumentModes.All.Contains(m.Trim()))
            .WithMessage("Chế độ chứng từ phải là external_received hoặc lcms_generated.");
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
    private readonly ILateDocumentGate _lateDocuments;
    private readonly IIdempotencyGate _idempotency;
    private readonly IAuditWriter _audit;
    private readonly IFxSnapshotService _fxSnapshot;

    public ReceiveFinancialDocumentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IOptions<DocumentOptions> options,
        IPartyDirectoryService parties,
        ILateDocumentGate lateDocuments,
        IIdempotencyGate idempotency,
        IAuditWriter audit,
        IFxSnapshotService fxSnapshot)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _options = options.Value;
        _parties = parties;
        _lateDocuments = lateDocuments;
        _idempotency = idempotency;
        _audit = audit;
        _fxSnapshot = fxSnapshot;
    }

    public async Task<Guid> Handle(ReceiveFinancialDocumentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var priorId = await _idempotency.FindAsync(
            IdempotencyScopes.FinancialDocument,
            request.IdempotencyKey,
            cancellationToken);
        if (priorId.HasValue)
        {
            return priorId.Value;
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var mode = string.IsNullOrWhiteSpace(request.Mode)
            ? FinancialDocumentModes.ExternalReceived
            : request.Mode.Trim().ToLowerInvariant();

        var rawDirection = request.Direction.Trim().ToLowerInvariant();
        var direction = rawDirection is "ap" or FinancialDocumentDirections.Payable
            ? FinancialDocumentDirections.Payable
            : FinancialDocumentDirections.Receivable;
        var isPayable = direction == FinancialDocumentDirections.Payable;

        var documentType = request.DocumentType.Trim().ToLowerInvariant();
        var documentNo = request.DocumentNo.Trim();
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var documentDate = request.DocumentDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var currencyRow = await _db.Currencies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == currency, cancellationToken);
        if (currencyRow is null)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["currencyCode"] = [$"Tiền tệ {currency} không có trong danh mục."]
            });
        }

        if (!currencyRow.IsActive)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["currencyCode"] = [$"Tiền tệ {currency} đã ngừng dùng."]
            });
        }

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
            var roles = isPayable
                ? PartyRoleCodes.VendorSide
                : PartyRoleCodes.CustomerSide;
            var purpose = isPayable
                ? "nhận chứng từ phải trả"
                : "nhận chứng từ phải thu";
            await _parties.EnsureUsableAsync(
                request.CounterpartyId.Value,
                roles,
                purpose,
                cancellationToken);
        }

        var selectedLines = request.SelectedSourceLines?
            .Where(l => l.Amount > 0)
            .ToList() ?? new List<SourceLineSelectionDto>();

        // Mode B requirement (FD-R02): Must select at least one source line
        if (mode == FinancialDocumentModes.LcmsGenerated && selectedLines.Count == 0)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["selectedSourceLines"] = ["Tạo chứng từ từ LCMS bắt buộc chọn ít nhất một dòng nguồn (FD-R02)."]
            });
        }

        var loadedCosts = new Dictionary<Guid, Cost>();
        var loadedRevenues = new Dictionary<Guid, Revenue>();

        // Validate source lines (FD-R04, FD-R07, FD-FX-04, AC-FD-007, AC-FD-009, AC-FD-010, AC-FD-014, AC-FD-019)
        if (selectedLines.Count > 0)
        {
            foreach (var line in selectedLines)
            {
                var st = line.SourceType.Trim().ToLowerInvariant();
                if (isPayable && st != "cost")
                {
                    throw new ValidationAppException(new Dictionary<string, string[]>
                    {
                        ["direction"] = ["Chiều phải trả (AP) chỉ được chọn các dòng chi phí (FD-R07, AC-FD-009)."]
                    });
                }
                if (!isPayable && st != "revenue")
                {
                    throw new ValidationAppException(new Dictionary<string, string[]>
                    {
                        ["direction"] = ["Chiều phải thu (AR) chỉ được chọn các dòng doanh thu (FD-R07, AC-FD-010)."]
                    });
                }

                if (isPayable)
                {
                    if (!loadedCosts.TryGetValue(line.SourceId, out var cost))
                    {
                        cost = await _db.Costs.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.Id == line.SourceId, cancellationToken)
                            ?? throw new NotFoundAppException($"Không tìm thấy chi phí nguồn {line.SourceId}.");
                        loadedCosts[cost.Id] = cost;
                    }

                    // Multi-currency check in Mode B (FD-FX-04, AC-FD-014)
                    if (mode == FinancialDocumentModes.LcmsGenerated && !string.Equals(cost.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ValidationAppException(new Dictionary<string, string[]>
                        {
                            ["currencyCode"] = ["Các dòng nguồn khác loại tiền tệ không được cộng trực tiếp; yêu cầu chọn loại tiền chứng từ và quy đổi (FD-FX-04, AC-FD-014)."]
                        });
                    }

                    // Counterparty check (AC-FD-019)
                    if (request.CounterpartyId.HasValue && cost.VendorPartyId.HasValue && cost.VendorPartyId.Value != request.CounterpartyId.Value)
                    {
                        throw new ValidationAppException(new Dictionary<string, string[]>
                        {
                            ["counterpartyId"] = ["Không thể khớp chi phí của nhà cung cấp khác với đối tác của chứng từ (AC-FD-019)."]
                        });
                    }

                    // Remaining eligible amount check (FD-R04, AC-FD-005, AC-FD-007)
                    var documented = (await _db.DocumentMatchDetails.AsNoTracking()
                        .Where(d => d.TargetCostId == cost.Id && d.DetailStatus == DocumentMatchDetailStatuses.Active && d.Match!.MatchStatus != DocumentMatchStatuses.Cancelled)
                        .Select(d => d.MatchedAmount)
                        .ToListAsync(cancellationToken))
                        .Sum();
                    var remaining = decimal.Round(cost.Amount - documented, 4, MidpointRounding.AwayFromZero);
                    if (line.Amount > remaining)
                    {
                        throw new ValidationAppException(new Dictionary<string, string[]>
                        {
                            ["amount"] = [$"Số tiền muốn gắn ({line.Amount}) vượt quá số dư còn lại ({remaining}) của chi phí {cost.CostTypeCode ?? cost.Id.ToString()} (AC-FD-007)."]
                        });
                    }
                }
                else
                {
                    if (!loadedRevenues.TryGetValue(line.SourceId, out var rev))
                    {
                        rev = await _db.Revenues.AsNoTracking()
                            .FirstOrDefaultAsync(r => r.Id == line.SourceId, cancellationToken)
                            ?? throw new NotFoundAppException($"Không tìm thấy doanh thu nguồn {line.SourceId}.");
                        loadedRevenues[rev.Id] = rev;
                    }

                    // Multi-currency check in Mode B (FD-FX-04, AC-FD-014)
                    if (mode == FinancialDocumentModes.LcmsGenerated && !string.Equals(rev.CurrencyCode, currency, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ValidationAppException(new Dictionary<string, string[]>
                        {
                            ["currencyCode"] = ["Các dòng nguồn khác loại tiền tệ không được cộng trực tiếp; yêu cầu chọn loại tiền chứng từ và quy đổi (FD-FX-04, AC-FD-014)."]
                        });
                    }

                    // Counterparty check (AC-FD-019)
                    if (request.CounterpartyId.HasValue && rev.CustomerPartyId.HasValue && rev.CustomerPartyId.Value != request.CounterpartyId.Value)
                    {
                        throw new ValidationAppException(new Dictionary<string, string[]>
                        {
                            ["counterpartyId"] = ["Không thể khớp doanh thu của khách hàng khác với đối tác của chứng từ (AC-FD-019)."]
                        });
                    }

                    // Remaining eligible amount check (FD-R04, AC-FD-005, AC-FD-007)
                    var documented = (await _db.DocumentMatchDetails.AsNoTracking()
                        .Where(d => d.TargetRevenueId == rev.Id && d.DetailStatus == DocumentMatchDetailStatuses.Active && d.Match!.MatchStatus != DocumentMatchStatuses.Cancelled)
                        .Select(d => d.MatchedAmount)
                        .ToListAsync(cancellationToken))
                        .Sum();
                    var remaining = decimal.Round(rev.Amount - documented, 4, MidpointRounding.AwayFromZero);
                    if (line.Amount > remaining)
                    {
                        throw new ValidationAppException(new Dictionary<string, string[]>
                        {
                            ["amount"] = [$"Số tiền muốn gắn ({line.Amount}) vượt quá số dư còn lại ({remaining}) của doanh thu {rev.RevenueTypeCode ?? rev.Id.ToString()} (AC-FD-007)."]
                        });
                    }
                }
            }
        }

        // Mode B derives amount from selected lines (FD-R02, AC-FD-001, AC-FD-002)
        // Mode A accepts user document amount (FD-R03, AC-FD-003)
        var finalTotalAmount = mode == FinancialDocumentModes.LcmsGenerated
            ? decimal.Round(selectedLines.Sum(l => l.Amount), 4, MidpointRounding.AwayFromZero)
            : decimal.Round(request.TotalAmount, 4, MidpointRounding.AwayFromZero);

        await _lateDocuments.EnsureReceiveAllowedAsync(
            request.BillId,
            documentDate,
            finalTotalAmount,
            cancellationToken);

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

        // Resolve FX snapshot (FD-FX-01..06, AC-FD-013..017)
        var fxSnapshot = await _fxSnapshot.ResolveAsync(
            new FxResolveRequest(currency, documentDate, request.ManualFxRate, request.FxOverrideReason),
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var document = new FinancialDocument
        {
            TenantId = tenantId,
            Mode = mode,
            DocumentType = documentType,
            DocumentNo = documentNo,
            Direction = direction,
            TotalAmount = finalTotalAmount,
            CurrencyCode = currency,
            DocumentDate = documentDate,
            CounterpartyId = request.CounterpartyId,
            BillId = request.BillId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? null : request.SourceSystem.Trim(),
            ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim(),
            ReceiptStatus = FinancialDocumentReceiptStatuses.Received,
            AcceptanceStatus = FinancialDocumentAcceptanceStatuses.NotAccepted,
            MatchingStatus = FinancialDocumentMatchingStatuses.Unmatched,
            ReceivedAt = now,
            ReceivedBy = _user.UserId,
            RecordStatus = FinancialDocumentRecordStatuses.Active
        };

        _fxSnapshot.Apply(document, fxSnapshot, "financial_document", document.Id);
        document.BaseAmount = FxMath.ToReporting(finalTotalAmount, fxSnapshot.Rate);

        _db.FinancialDocuments.Add(document);

        // If source lines were selected, create DocumentLines, DocumentMatch, and DocumentMatchDetails
        if (selectedLines.Count > 0)
        {
            var totalMatched = selectedLines.Sum(l => l.Amount);
            var variance = finalTotalAmount - totalMatched;

            var match = new DocumentMatch
            {
                TenantId = tenantId,
                PrimaryDocumentId = document.Id,
                MatchMethod = isPayable ? DocumentMatchMethods.LineToCost : DocumentMatchMethods.LineToRevenue,
                MatchStatus = DocumentMatchStatuses.Confirmed,
                VersionNo = 1,
                ConfirmedAt = now,
                ConfirmedBy = _user.UserId,
                Notes = $"Khớp tự động khi {(mode == FinancialDocumentModes.LcmsGenerated ? "tạo chứng từ từ LCMS" : "nhận chứng từ bên ngoài")}."
            };
            _db.DocumentMatches.Add(match);

            for (var i = 0; i < selectedLines.Count; i++)
            {
                var sel = selectedLines[i];
                Guid? lineBillId = sel.BillId ?? (isPayable ? loadedCosts[sel.SourceId].BillId : loadedRevenues[sel.SourceId].BillId);
                string? costCode = isPayable ? loadedCosts[sel.SourceId].CostTypeCode : null;
                string? revCode = !isPayable ? loadedRevenues[sel.SourceId].RevenueTypeCode : null;
                decimal origAmount = isPayable ? loadedCosts[sel.SourceId].Amount : loadedRevenues[sel.SourceId].Amount;

                var docLine = new FinancialDocumentLine
                {
                    TenantId = tenantId,
                    DocumentId = document.Id,
                    LineNo = i + 1,
                    Description = costCode ?? revCode ?? $"Dòng {i + 1}",
                    Amount = sel.Amount,
                    MatchedAmount = sel.Amount,
                    CurrencyCode = currency,
                    BillId = lineBillId,
                    CostTypeCode = costCode,
                    RevenueTypeCode = revCode
                };
                _db.FinancialDocumentLines.Add(docLine);

                var detailVariance = (i == 0 && mode == FinancialDocumentModes.ExternalReceived) ? variance : 0m;

                var detail = new DocumentMatchDetail
                {
                    TenantId = tenantId,
                    MatchId = match.Id,
                    SourceLineId = docLine.Id,
                    TargetCostId = isPayable ? sel.SourceId : null,
                    TargetRevenueId = !isPayable ? sel.SourceId : null,
                    SourceType = isPayable ? "cost" : "revenue",
                    SourceOriginalAmount = origAmount,
                    MatchedAmount = sel.Amount,
                    MatchedReportingAmount = FxMath.ToReporting(sel.Amount, fxSnapshot.Rate),
                    VarianceAmount = detailVariance,
                    BillId = lineBillId,
                    OutcomeCode = DocumentMatchOutcomes.Matched,
                    DetailStatus = DocumentMatchDetailStatuses.Active
                };
                _db.DocumentMatchDetails.Add(detail);
            }

            if (mode == FinancialDocumentModes.LcmsGenerated)
            {
                document.MatchingStatus = FinancialDocumentMatchingStatuses.Matched;
            }
            else
            {
                document.MatchingStatus = variance == 0m
                    ? FinancialDocumentMatchingStatuses.Matched
                    : FinancialDocumentMatchingStatuses.PartiallyMatched;
            }
        }

        _audit.Append(
            AuditActions.FinancialDocumentReceive,
            AuditObjectTypes.FinancialDocument,
            document.Id,
            afterJson: $"{{\"billId\":\"{document.BillId}\",\"currency\":\"{document.CurrencyCode}\",\"documentNo\":\"{document.DocumentNo}\",\"mode\":\"{document.Mode}\",\"totalAmount\":{finalTotalAmount},\"baseAmount\":{document.BaseAmount}}}");

        _idempotency.Remember(
            IdempotencyScopes.FinancialDocument,
            request.IdempotencyKey ?? string.Empty,
            document.Id,
            tenantId);

        await _db.SaveChangesAsync(cancellationToken);
        return document.Id;
    }
}

