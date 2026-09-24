using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Commands;

/// <summary>
/// Soft-reverse an AP recognition slice. Requires no finalized settlement. Restores exposure open (C-013/C-015).
/// </summary>
public sealed record ReverseRecognizeAccountsPayableCommand(
    Guid AccountsPayableId,
    string Reason,
    string? IfMatch = null) : IRequest;

public sealed class ReverseRecognizeAccountsPayableCommandValidator
    : AbstractValidator<ReverseRecognizeAccountsPayableCommand>
{
    public ReverseRecognizeAccountsPayableCommandValidator()
    {
        RuleFor(x => x.AccountsPayableId).NotEmpty().WithMessage("Khoản phải trả không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do đảo ghi nhận không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do đảo ghi nhận không được vượt quá 1024 ký tự.");
    }
}

public sealed class ReverseRecognizeAccountsPayableCommandHandler
    : IRequestHandler<ReverseRecognizeAccountsPayableCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly IRowVersionGuard _versions;

    public ReverseRecognizeAccountsPayableCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _versions = versions;
    }

    public async Task Handle(ReverseRecognizeAccountsPayableCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var ap = await _db.AccountsPayable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsPayableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả.");
        _versions.EnsureCurrent(ap, request.IfMatch);

        if (string.Equals(ap.RecordStatus, ApArRecordStatuses.Reversed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Khoản phải trả đã được đảo ghi nhận.");
        }

        if (!string.Equals(ap.RecordStatus, ApArRecordStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ đảo ghi nhận khoản phải trả đang hiệu lực.");
        }

        if (ap.FinalizedSettledAmount > 0m)
        {
            throw new ConflictAppException(
                "Phải đảo phân bổ thanh toán đã chốt trước khi đảo ghi nhận AP.");
        }

        var exposure = await _db.PayableExposures
            .FirstOrDefaultAsync(e => e.Id == ap.PayableExposureId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy exposure phải trả.");

        var outstandingBefore = ap.DeriveOutstanding();
        var adjBefore = ap.AdjustmentAmount;
        var reason = request.Reason.Trim();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Zero outstanding via adjustment trail; keep RecognizedAmount as historical fact.
        var delta = decimal.Round(-outstandingBefore, 4, MidpointRounding.AwayFromZero);
        ap.AdjustmentAmount = decimal.Round(ap.AdjustmentAmount + delta, 4, MidpointRounding.AwayFromZero);
        ap.RecordStatus = ApArRecordStatuses.Reversed;
        ap.SettlementStatus = ApArSettlementStatuses.Open;
        ap.UpdatedAt = now;
        ap.UpdatedBy = _user.UserId;

        var reasonLine = $"[đảo ghi nhận] {reason}";
        ap.Notes = string.IsNullOrWhiteSpace(ap.Notes)
            ? reasonLine
            : $"{ap.Notes}\n{reasonLine}";

        _db.AccountsPayableAdjustments.Add(new AccountsPayableAdjustment
        {
            TenantId = _tenantContext.TenantId!.Value,
            AccountsPayableId = ap.Id,
            AdjustmentType = ApArAdjustmentTypes.ReverseRecognize,
            DeltaAmount = delta,
            CurrencyCode = ap.CurrencyCode,
            Reason = reason,
            EffectiveDate = today,
            AdjustmentAmountBefore = adjBefore,
            AdjustmentAmountAfter = ap.AdjustmentAmount,
            OutstandingBefore = outstandingBefore,
            OutstandingAfter = ap.DeriveOutstanding()
        });

        var activeRecognizedRows = await _db.AccountsPayable.AsNoTracking()
            .Where(a =>
                a.PayableExposureId == exposure.Id
                && a.Id != ap.Id
                && a.RecordStatus == ApArRecordStatuses.Active)
            .Select(a => a.RecognizedAmount)
            .ToListAsync(cancellationToken);
        var activeRecognized = decimal.Round(activeRecognizedRows.Sum(), 4, MidpointRounding.AwayFromZero);

        exposure.RecognizedAmount = decimal.Round(activeRecognized, 4, MidpointRounding.AwayFromZero);
        exposure.Status = exposure.RecognizedAmount <= 0m
            ? ExposureStatuses.Open
            : exposure.RecognizedAmount >= exposure.Amount
                ? ExposureStatuses.FullyRecognized
                : ExposureStatuses.PartiallyRecognized;
        exposure.UpdatedAt = now;
        exposure.UpdatedBy = _user.UserId;
        exposure.TouchRowVersion();

        _audit.Append(
            AuditActions.AccountsPayableReverseRecognize,
            AuditObjectTypes.AccountsPayable,
            ap.Id,
            beforeJson: AuditJson.Serialize(new
            {
                id = ap.Id,
                exposureId = exposure.Id,
                recognized = ap.RecognizedAmount,
                adjustmentBefore = adjBefore,
                outstandingBefore,
                recordStatus = ApArRecordStatuses.Active
            }),
            afterJson: AuditJson.Serialize(new
            {
                id = ap.Id,
                exposureId = exposure.Id,
                recognized = ap.RecognizedAmount,
                adjustment = ap.AdjustmentAmount,
                outstanding = ap.DeriveOutstanding(),
                recordStatus = ap.RecordStatus,
                exposureRecognizedTotal = exposure.RecognizedAmount,
                exposureStatus = exposure.Status
            }),
            reason: reason);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
