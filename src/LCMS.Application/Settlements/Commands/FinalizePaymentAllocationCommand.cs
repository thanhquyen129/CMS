using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.FinancialCloses;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record FinalizePaymentAllocationCommand(Guid AllocationId, string? IfMatch = null) : IRequest;

public sealed class FinalizePaymentAllocationCommandValidator : AbstractValidator<FinalizePaymentAllocationCommand>
{
    public FinalizePaymentAllocationCommandValidator()
    {
        RuleFor(x => x.AllocationId).NotEmpty().WithMessage("Phân bổ thanh toán không hợp lệ.");
    }
}

/// <summary>
/// Finalizes draft payment allocation: updates AP.FinalizedSettledAmount (AC-007).
/// Re-checks C-008. Does not create Cost (C-003).
/// Idempotent: repeat finalize on already-finalized allocation is a safe no-op.
/// Period lock when financial close is Locked (Pass 2 Sprint 10 FULL).
/// </summary>
public sealed class FinalizePaymentAllocationCommandHandler : IRequestHandler<FinalizePaymentAllocationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly IPeriodLockGate _periodLockGate;
    private readonly IRowVersionGuard _versions;

    public FinalizePaymentAllocationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        IPeriodLockGate periodLockGate,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _periodLockGate = periodLockGate;
        _versions = versions;
    }

    public async Task Handle(FinalizePaymentAllocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var allocation = await _db.PaymentAllocations
            .FirstOrDefaultAsync(a => a.Id == request.AllocationId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phân bổ thanh toán.");

        // Idempotent finalize: already finalized → safe no-op (no double settle).
        if (string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Finalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _versions.EnsureCurrent(allocation, request.IfMatch);

        if (string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Reversed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException(
                "Không thể chốt phân bổ thanh toán đã đảo. Tạo phân bổ mới nếu cần.");
        }

        if (!string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ chốt phân bổ thanh toán ở trạng thái nháp.");
        }

        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == allocation.PaymentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thanh toán.");

        if (payment.Status == PaymentStatuses.Cancelled)
        {
            throw new ConflictAppException("Không thể chốt phân bổ của thanh toán đã hủy.");
        }

        var ap = await _db.AccountsPayable
            .FirstOrDefaultAsync(a => a.Id == allocation.AccountsPayableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả.");

        await _periodLockGate.EnsureMutationAllowedAsync(
            payment.BillId ?? ap.BillId,
            payment.ValueDate,
            "chốt phân bổ thanh toán",
            cancellationToken);

        // C-008: finalized on payment + this amount ≤ payment.Amount
        var paymentFinalizedAmounts = await _db.PaymentAllocations.AsNoTracking()
            .Where(a => a.PaymentId == payment.Id
                && a.AllocationStatus == SettlementAllocationStatuses.Finalized)
            .Select(a => a.Amount)
            .ToListAsync(cancellationToken);
        var paymentFinalized = paymentFinalizedAmounts.Sum();
        if (paymentFinalized + allocation.Amount > payment.Amount + SettlementHelpers.OverSettlementTolerance)
        {
            throw new ConflictAppException(
                "Tổng phân bổ đã chốt vượt số tiền thanh toán (C-008).");
        }

        // C-008: AP finalized + this ≤ recognized + adjustment
        var nextSettled = decimal.Round(
            ap.FinalizedSettledAmount + SettlementCurrency.TargetAmount(allocation), 4, MidpointRounding.AwayFromZero);
        var ceiling = ap.RecognizedAmount + ap.AdjustmentAmount + SettlementHelpers.OverSettlementTolerance;
        if (nextSettled > ceiling)
        {
            throw new ConflictAppException(
                "Tổng phân bổ đã chốt vượt số dư còn lại của khoản phải trả (C-008).");
        }

        var costCountBefore = await _db.Costs.CountAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var beforeJson = AuditJson.Serialize(new
        {
            allocationId = allocation.Id,
            paymentId = allocation.PaymentId,
            accountsPayableId = ap.Id,
            status = allocation.AllocationStatus,
            amount = allocation.Amount,
            apRecognized = ap.RecognizedAmount,
            apAdjustment = ap.AdjustmentAmount,
            apSettled = ap.FinalizedSettledAmount,
            apOutstanding = ap.DeriveOutstanding(),
            apSettlementStatus = ap.SettlementStatus
        });

        allocation.AllocationStatus = SettlementAllocationStatuses.Finalized;
        allocation.FinalizedAt = now;
        allocation.FinalizedBy = _user.UserId;

        ap.FinalizedSettledAmount = nextSettled;
        ap.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ap.RecognizedAmount, ap.AdjustmentAmount, ap.FinalizedSettledAmount);
        ap.UpdatedAt = now;
        ap.UpdatedBy = _user.UserId;

        _audit.Append(
            AuditActions.PaymentAllocationFinalize,
            AuditObjectTypes.PaymentAllocation,
            allocation.Id,
            beforeJson: beforeJson,
            afterJson: AuditJson.Serialize(new
            {
                allocationId = allocation.Id,
                paymentId = allocation.PaymentId,
                accountsPayableId = ap.Id,
                status = SettlementAllocationStatuses.Finalized,
                amount = allocation.Amount,
                apRecognized = ap.RecognizedAmount,
                apAdjustment = ap.AdjustmentAmount,
                apSettled = nextSettled,
                apOutstanding = ap.DeriveOutstanding(),
                apSettlementStatus = ap.SettlementStatus,
                finalizedAt = now
            }));

        await _db.SaveChangesAsync(cancellationToken);

        var costCountAfter = await _db.Costs.CountAsync(cancellationToken);
        if (costCountAfter != costCountBefore)
        {
            throw new ConflictAppException("Chốt phân bổ thanh toán không được tạo Chi phí mới (C-003).");
        }
    }
}
