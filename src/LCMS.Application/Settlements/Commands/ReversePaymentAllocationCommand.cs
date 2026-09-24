using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record ReversePaymentAllocationCommand(Guid AllocationId, string Reason, string? IfMatch = null) : IRequest;

public sealed class ReversePaymentAllocationCommandValidator : AbstractValidator<ReversePaymentAllocationCommand>
{
    public ReversePaymentAllocationCommandValidator()
    {
        RuleFor(x => x.AllocationId).NotEmpty().WithMessage("Phân bổ thanh toán không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Phải nêu lý do đảo phân bổ thanh toán.")
            .MaximumLength(1024).WithMessage("Lý do đảo không được vượt quá 1024 ký tự.");
    }
}

/// <summary>
/// Reverses a finalized payment allocation: status → reversed; restores AP outstanding.
/// No hard delete / silent overwrite (C-013).
/// </summary>
public sealed class ReversePaymentAllocationCommandHandler : IRequestHandler<ReversePaymentAllocationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IRowVersionGuard _versions;

    public ReversePaymentAllocationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _versions = versions;
    }

    public async Task Handle(ReversePaymentAllocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var allocation = await _db.PaymentAllocations
            .FirstOrDefaultAsync(a => a.Id == request.AllocationId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phân bổ thanh toán.");
        _versions.EnsureCurrent(allocation, request.IfMatch);

        if (string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Reversed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Phân bổ thanh toán đã được đảo.");
        }

        if (string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            // Cancel draft without touching outstanding
            allocation.AllocationStatus = SettlementAllocationStatuses.Reversed;
            allocation.ReversedAt = DateTimeOffset.UtcNow;
            allocation.ReversedBy = _user.UserId;
            allocation.ReverseReason = request.Reason.Trim();
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Finalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ đảo phân bổ thanh toán ở trạng thái nháp hoặc đã chốt.");
        }

        var ap = await _db.AccountsPayable
            .FirstOrDefaultAsync(a => a.Id == allocation.AccountsPayableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả.");

        var now = DateTimeOffset.UtcNow;
        allocation.AllocationStatus = SettlementAllocationStatuses.Reversed;
        allocation.ReversedAt = now;
        allocation.ReversedBy = _user.UserId;
        allocation.ReverseReason = request.Reason.Trim();

        ap.FinalizedSettledAmount = decimal.Round(
            Math.Max(0m, ap.FinalizedSettledAmount - allocation.Amount), 4, MidpointRounding.AwayFromZero);
        ap.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ap.RecognizedAmount, ap.AdjustmentAmount, ap.FinalizedSettledAmount);
        ap.UpdatedAt = now;
        ap.UpdatedBy = _user.UserId;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
