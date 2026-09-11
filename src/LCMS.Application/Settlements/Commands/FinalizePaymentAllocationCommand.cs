using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record FinalizePaymentAllocationCommand(Guid AllocationId) : IRequest;

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
/// </summary>
public sealed class FinalizePaymentAllocationCommandHandler : IRequestHandler<FinalizePaymentAllocationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public FinalizePaymentAllocationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
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
            ap.FinalizedSettledAmount + allocation.Amount, 4, MidpointRounding.AwayFromZero);
        var ceiling = ap.RecognizedAmount + ap.AdjustmentAmount + SettlementHelpers.OverSettlementTolerance;
        if (nextSettled > ceiling)
        {
            throw new ConflictAppException(
                "Tổng phân bổ đã chốt vượt số dư còn lại của khoản phải trả (C-008).");
        }

        var costCountBefore = await _db.Costs.CountAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        allocation.AllocationStatus = SettlementAllocationStatuses.Finalized;
        allocation.FinalizedAt = now;
        allocation.FinalizedBy = _user.UserId;

        ap.FinalizedSettledAmount = nextSettled;
        ap.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ap.RecognizedAmount, ap.AdjustmentAmount, ap.FinalizedSettledAmount);
        ap.UpdatedAt = now;
        ap.UpdatedBy = _user.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        var costCountAfter = await _db.Costs.CountAsync(cancellationToken);
        if (costCountAfter != costCountBefore)
        {
            throw new ConflictAppException("Chốt phân bổ thanh toán không được tạo Chi phí mới (C-003).");
        }
    }
}
