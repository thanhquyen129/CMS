using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record ReverseCollectionAllocationCommand(Guid AllocationId, string Reason) : IRequest;

public sealed class ReverseCollectionAllocationCommandValidator
    : AbstractValidator<ReverseCollectionAllocationCommand>
{
    public ReverseCollectionAllocationCommandValidator()
    {
        RuleFor(x => x.AllocationId).NotEmpty().WithMessage("Phân bổ thu tiền không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Phải nêu lý do đảo phân bổ thu tiền.")
            .MaximumLength(1024).WithMessage("Lý do đảo không được vượt quá 1024 ký tự.");
    }
}

/// <summary>
/// Reverses a finalized collection allocation: status → reversed; restores AR outstanding.
/// No hard delete / silent overwrite (C-013).
/// </summary>
public sealed class ReverseCollectionAllocationCommandHandler
    : IRequestHandler<ReverseCollectionAllocationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public ReverseCollectionAllocationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(ReverseCollectionAllocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var allocation = await _db.CollectionAllocations
            .FirstOrDefaultAsync(a => a.Id == request.AllocationId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phân bổ thu tiền.");

        if (string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Reversed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Phân bổ thu tiền đã được đảo.");
        }

        if (string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            allocation.AllocationStatus = SettlementAllocationStatuses.Reversed;
            allocation.ReversedAt = DateTimeOffset.UtcNow;
            allocation.ReversedBy = _user.UserId;
            allocation.ReverseReason = request.Reason.Trim();
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Finalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ đảo phân bổ thu tiền ở trạng thái nháp hoặc đã chốt.");
        }

        var ar = await _db.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == allocation.AccountsReceivableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");

        var now = DateTimeOffset.UtcNow;
        allocation.AllocationStatus = SettlementAllocationStatuses.Reversed;
        allocation.ReversedAt = now;
        allocation.ReversedBy = _user.UserId;
        allocation.ReverseReason = request.Reason.Trim();

        ar.FinalizedSettledAmount = decimal.Round(
            Math.Max(0m, ar.FinalizedSettledAmount - allocation.Amount), 4, MidpointRounding.AwayFromZero);
        ar.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ar.RecognizedAmount, ar.AdjustmentAmount, ar.FinalizedSettledAmount);
        ar.UpdatedAt = now;
        ar.UpdatedBy = _user.UserId;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
