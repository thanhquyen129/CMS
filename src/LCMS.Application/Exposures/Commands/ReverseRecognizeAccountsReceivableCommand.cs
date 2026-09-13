using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Commands;

public sealed record ReverseRecognizeAccountsReceivableCommand(Guid AccountsReceivableId, string Reason) : IRequest;

public sealed class ReverseRecognizeAccountsReceivableCommandValidator
    : AbstractValidator<ReverseRecognizeAccountsReceivableCommand>
{
    public ReverseRecognizeAccountsReceivableCommandValidator()
    {
        RuleFor(x => x.AccountsReceivableId).NotEmpty().WithMessage("Khoản phải thu không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do đảo ghi nhận không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do đảo ghi nhận không được vượt quá 1024 ký tự.");
    }
}

public sealed class ReverseRecognizeAccountsReceivableCommandHandler
    : IRequestHandler<ReverseRecognizeAccountsReceivableCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public ReverseRecognizeAccountsReceivableCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(ReverseRecognizeAccountsReceivableCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var ar = await _db.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsReceivableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");

        if (string.Equals(ar.RecordStatus, ApArRecordStatuses.Reversed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Khoản phải thu đã được đảo ghi nhận.");
        }

        if (!string.Equals(ar.RecordStatus, ApArRecordStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ đảo ghi nhận khoản phải thu đang hiệu lực.");
        }

        if (ar.FinalizedSettledAmount > 0m)
        {
            throw new ConflictAppException(
                "Phải đảo phân bổ thu tiền đã chốt trước khi đảo ghi nhận AR.");
        }

        var exposure = await _db.ReceivableExposures
            .FirstOrDefaultAsync(e => e.Id == ar.ReceivableExposureId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy exposure phải thu.");

        var outstandingBefore = ar.DeriveOutstanding();
        var adjBefore = ar.AdjustmentAmount;
        var reason = request.Reason.Trim();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var delta = decimal.Round(-outstandingBefore, 4, MidpointRounding.AwayFromZero);
        ar.AdjustmentAmount = decimal.Round(ar.AdjustmentAmount + delta, 4, MidpointRounding.AwayFromZero);
        ar.RecordStatus = ApArRecordStatuses.Reversed;
        ar.SettlementStatus = ApArSettlementStatuses.Open;
        ar.UpdatedAt = now;
        ar.UpdatedBy = _user.UserId;

        var reasonLine = $"[đảo ghi nhận] {reason}";
        ar.Notes = string.IsNullOrWhiteSpace(ar.Notes)
            ? reasonLine
            : $"{ar.Notes}\n{reasonLine}";

        _db.AccountsReceivableAdjustments.Add(new AccountsReceivableAdjustment
        {
            TenantId = _tenantContext.TenantId!.Value,
            AccountsReceivableId = ar.Id,
            AdjustmentType = ApArAdjustmentTypes.ReverseRecognize,
            DeltaAmount = delta,
            CurrencyCode = ar.CurrencyCode,
            Reason = reason,
            EffectiveDate = today,
            AdjustmentAmountBefore = adjBefore,
            AdjustmentAmountAfter = ar.AdjustmentAmount,
            OutstandingBefore = outstandingBefore,
            OutstandingAfter = ar.DeriveOutstanding()
        });

        var activeRecognizedRows = await _db.AccountsReceivable.AsNoTracking()
            .Where(a =>
                a.ReceivableExposureId == exposure.Id
                && a.Id != ar.Id
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
            AuditActions.AccountsReceivableReverseRecognize,
            AuditObjectTypes.AccountsReceivable,
            ar.Id,
            beforeJson: AuditJson.Serialize(new
            {
                id = ar.Id,
                exposureId = exposure.Id,
                recognized = ar.RecognizedAmount,
                adjustmentBefore = adjBefore,
                outstandingBefore,
                recordStatus = ApArRecordStatuses.Active
            }),
            afterJson: AuditJson.Serialize(new
            {
                id = ar.Id,
                exposureId = exposure.Id,
                recognized = ar.RecognizedAmount,
                adjustment = ar.AdjustmentAmount,
                outstanding = ar.DeriveOutstanding(),
                recordStatus = ar.RecordStatus,
                exposureRecognizedTotal = exposure.RecognizedAmount,
                exposureStatus = exposure.Status
            }),
            reason: reason);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
