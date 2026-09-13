using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Tenancy;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Commands;

/// <summary>
/// Recognize a (partial) slice of payable exposure into a new accounts_payable row.
/// Does not create Cost (C-003). Exposure and AP remain separate records.
/// </summary>
public sealed record RecognizePayableExposureCommand(
    Guid PayableExposureId,
    decimal Amount,
    DateOnly? DueDate,
    string? Notes) : IRequest<Guid>;

public sealed class RecognizePayableExposureCommandValidator : AbstractValidator<RecognizePayableExposureCommand>
{
    public RecognizePayableExposureCommandValidator()
    {
        RuleFor(x => x.PayableExposureId).NotEmpty().WithMessage("Exposure phải trả không hợp lệ.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền ghi nhận phải trả phải lớn hơn 0.");
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
    }
}

public sealed class RecognizePayableExposureCommandHandler : IRequestHandler<RecognizePayableExposureCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly TenantFinancialOptionsResolver _financial;

    public RecognizePayableExposureCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        TenantFinancialOptionsResolver financial)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _financial = financial;
    }

    public async Task<Guid> Handle(RecognizePayableExposureCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var exposure = await _db.PayableExposures
            .FirstOrDefaultAsync(e => e.Id == request.PayableExposureId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy nghĩa vụ phải trả (exposure).");

        if (exposure.Status == ExposureStatuses.Cancelled)
        {
            throw new ConflictAppException("Không thể ghi nhận exposure đã hủy.");
        }

        if (exposure.Status == ExposureStatuses.FullyRecognized)
        {
            throw new ConflictAppException("Exposure phải trả đã được ghi nhận đủ.");
        }

        var (policyMode, policyVersion) = await _financial.GetRecognitionPolicyAsync(cancellationToken);
        if (policyMode == RecognitionPolicyModes.RequireDocumentLink
            && !exposure.FinancialDocumentId.HasValue)
        {
            throw new ConflictAppException(
                "Chính sách ghi nhận yêu cầu exposure đã liên kết chứng từ (require_document_link). " +
                $"Phiên bản chính sách: {policyVersion}.");
        }

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);

        // SoT for recognized_amount = sum of active AP recognition slices (cache kept in sync).
        var recognizedSum = await _db.AccountsPayable
            .Where(a => a.PayableExposureId == exposure.Id && a.RecordStatus == ApArRecordStatuses.Active)
            .Select(a => a.RecognizedAmount)
            .ToListAsync(cancellationToken);
        var alreadyRecognized = decimal.Round(recognizedSum.Sum(), 4, MidpointRounding.AwayFromZero);
        exposure.RecognizedAmount = alreadyRecognized;

        var open = exposure.Amount - alreadyRecognized;
        if (amount > open)
        {
            throw new ConflictAppException(
                $"Số tiền ghi nhận ({amount}) vượt phần còn lại của exposure ({open}).");
        }

        var costCountBefore = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountBefore = await _db.Revenues.CountAsync(cancellationToken);

        var beforeJson = AuditJson.Serialize(new
        {
            exposureId = exposure.Id,
            billId = exposure.BillId,
            exposureAmount = exposure.Amount,
            recognizedAmount = alreadyRecognized,
            openAmount = open,
            exposureStatus = exposure.Status,
            currency = exposure.CurrencyCode
        });

        var now = DateTimeOffset.UtcNow;
        var ap = new AccountsPayable
        {
            TenantId = tenantId,
            PayableExposureId = exposure.Id,
            BillId = exposure.BillId,
            CounterpartyId = exposure.CounterpartyId,
            RecognizedAmount = amount,
            AdjustmentAmount = 0m,
            FinalizedSettledAmount = 0m,
            CurrencyCode = exposure.CurrencyCode,
            DueDate = request.DueDate ?? exposure.DueDate,
            SettlementStatus = ApArSettlementStatuses.Open,
            RecognizedAt = now,
            RecognizedBy = _user.UserId,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            RecordStatus = "active"
        };

        exposure.RecognizedAmount = decimal.Round(
            alreadyRecognized + amount, 4, MidpointRounding.AwayFromZero);
        exposure.Status = exposure.RecognizedAmount >= exposure.Amount
            ? ExposureStatuses.FullyRecognized
            : ExposureStatuses.PartiallyRecognized;
        exposure.UpdatedAt = now;
        exposure.UpdatedBy = _user.UserId;
        exposure.TouchRowVersion();

        _db.AccountsPayable.Add(ap);
        _audit.Append(
            AuditActions.AccountsPayableRecognize,
            AuditObjectTypes.AccountsPayable,
            ap.Id,
            beforeJson: beforeJson,
            afterJson: AuditJson.Serialize(new
            {
                accountsPayableId = ap.Id,
                exposureId = exposure.Id,
                billId = ap.BillId,
                recognizedAmount = amount,
                exposureRecognizedTotal = exposure.RecognizedAmount,
                exposureStatus = exposure.Status,
                settlementStatus = ap.SettlementStatus,
                outstanding = ap.DeriveOutstanding(),
                currency = ap.CurrencyCode,
                dueDate = ap.DueDate
            }));

        // Explicit: recognition must not invent Cost or Revenue (C-003 / C-004).
        await _db.SaveChangesAsync(cancellationToken);

        var costCountAfter = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (costCountAfter != costCountBefore || revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException(
                "Ghi nhận AP không được tạo Chi phí hoặc Doanh thu mới (C-003/C-004).");
        }

        return ap.Id;
    }
}
