using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record ReverseCollectionAllocationCommand(Guid AllocationId, string Reason, string? IfMatch = null) : IRequest;

public sealed class ReverseCollectionAllocationCommandValidator
    : AbstractValidator<ReverseCollectionAllocationCommand>
{
    public ReverseCollectionAllocationCommandValidator()
    {
        RuleFor(x => x.AllocationId).NotEmpty().WithMessage("Phân bổ thu tiền không hợp lệ.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Phải nêu lý do hủy phân bổ thu tiền.")
            .MaximumLength(1024).WithMessage("Lý do hủy phân bổ không được vượt quá 1024 ký tự.");
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
    private readonly IRowVersionGuard _versions;
    private readonly IAuditWriter _audit;

    public ReverseCollectionAllocationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IRowVersionGuard versions,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _versions = versions;
        _audit = audit;
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
        _versions.EnsureCurrent(allocation, request.IfMatch);

        if (string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Reversed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Phân bổ thu tiền đã được hủy.");
        }

        var reason = request.Reason.Trim();
        var statusBefore = allocation.AllocationStatus;

        if (string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            allocation.AllocationStatus = SettlementAllocationStatuses.Reversed;
            allocation.ReversedAt = DateTimeOffset.UtcNow;
            allocation.ReversedBy = _user.UserId;
            allocation.ReverseReason = reason;
            AppendAudit(allocation, statusBefore, null, null, reason);
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Finalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ hủy phân bổ thu tiền ở trạng thái nháp hoặc đã chốt.");
        }

        var ar = await _db.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == allocation.AccountsReceivableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");

        var now = DateTimeOffset.UtcNow;
        allocation.AllocationStatus = SettlementAllocationStatuses.Reversed;
        allocation.ReversedAt = now;
        allocation.ReversedBy = _user.UserId;
        allocation.ReverseReason = reason;

        var outstandingBefore = ar.DeriveOutstanding();
        ar.FinalizedSettledAmount = decimal.Round(
            Math.Max(0m, ar.FinalizedSettledAmount - SettlementCurrency.TargetAmount(allocation)),
            4,
            MidpointRounding.AwayFromZero);
        ar.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ar.RecognizedAmount, ar.AdjustmentAmount, ar.FinalizedSettledAmount);
        ar.UpdatedAt = now;
        ar.UpdatedBy = _user.UserId;

        AppendAudit(allocation, statusBefore, outstandingBefore, ar.DeriveOutstanding(), reason);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private void AppendAudit(
        CollectionAllocation allocation,
        string statusBefore,
        decimal? outstandingBefore,
        decimal? outstandingAfter,
        string reason)
    {
        _audit.Append(
            AuditActions.CollectionAllocationReverse,
            AuditObjectTypes.CollectionAllocation,
            allocation.Id,
            beforeJson: AuditJson.Serialize(new
            {
                allocationId = allocation.Id,
                collectionId = allocation.CollectionId,
                accountsReceivableId = allocation.AccountsReceivableId,
                status = statusBefore,
                outstanding = outstandingBefore
            }),
            afterJson: AuditJson.Serialize(new
            {
                allocationId = allocation.Id,
                collectionId = allocation.CollectionId,
                accountsReceivableId = allocation.AccountsReceivableId,
                status = allocation.AllocationStatus,
                amount = allocation.Amount,
                currencyCode = allocation.CurrencyCode,
                settledAmount = SettlementCurrency.TargetAmount(allocation),
                outstanding = outstandingAfter
            }),
            reason: reason);
    }
}
