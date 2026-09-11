using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record FinalizeCollectionAllocationCommand(Guid AllocationId) : IRequest;

public sealed class FinalizeCollectionAllocationCommandValidator
    : AbstractValidator<FinalizeCollectionAllocationCommand>
{
    public FinalizeCollectionAllocationCommandValidator()
    {
        RuleFor(x => x.AllocationId).NotEmpty().WithMessage("Phân bổ thu tiền không hợp lệ.");
    }
}

/// <summary>
/// Finalizes draft collection allocation: updates AR.FinalizedSettledAmount (AC-007).
/// Re-checks C-008. Does not create Revenue (C-004).
/// </summary>
public sealed class FinalizeCollectionAllocationCommandHandler
    : IRequestHandler<FinalizeCollectionAllocationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public FinalizeCollectionAllocationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(FinalizeCollectionAllocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var allocation = await _db.CollectionAllocations
            .FirstOrDefaultAsync(a => a.Id == request.AllocationId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phân bổ thu tiền.");

        if (!string.Equals(allocation.AllocationStatus, SettlementAllocationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ chốt phân bổ thu tiền ở trạng thái nháp.");
        }

        var collection = await _db.Collections
            .FirstOrDefaultAsync(c => c.Id == allocation.CollectionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thu tiền.");

        if (collection.Status == CollectionStatuses.Cancelled)
        {
            throw new ConflictAppException("Không thể chốt phân bổ của thu tiền đã hủy.");
        }

        var ar = await _db.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == allocation.AccountsReceivableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");

        var collectionFinalized = await _db.CollectionAllocations.AsNoTracking()
            .Where(a => a.CollectionId == collection.Id
                && a.AllocationStatus == SettlementAllocationStatuses.Finalized)
            .SumAsync(a => a.Amount, cancellationToken);
        if (collectionFinalized + allocation.Amount > collection.Amount + SettlementHelpers.OverSettlementTolerance)
        {
            throw new ConflictAppException(
                "Tổng phân bổ đã chốt vượt số tiền thu (C-008).");
        }

        var nextSettled = decimal.Round(
            ar.FinalizedSettledAmount + allocation.Amount, 4, MidpointRounding.AwayFromZero);
        var ceiling = ar.RecognizedAmount + ar.AdjustmentAmount + SettlementHelpers.OverSettlementTolerance;
        if (nextSettled > ceiling)
        {
            throw new ConflictAppException(
                "Tổng phân bổ đã chốt vượt số dư còn lại của khoản phải thu (C-008).");
        }

        var revenueCountBefore = await _db.Revenues.CountAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        allocation.AllocationStatus = SettlementAllocationStatuses.Finalized;
        allocation.FinalizedAt = now;
        allocation.FinalizedBy = _user.UserId;

        ar.FinalizedSettledAmount = nextSettled;
        ar.SettlementStatus = SettlementHelpers.DeriveApArSettlementStatus(
            ar.RecognizedAmount, ar.AdjustmentAmount, ar.FinalizedSettledAmount);
        ar.UpdatedAt = now;
        ar.UpdatedBy = _user.UserId;

        await _db.SaveChangesAsync(cancellationToken);

        var revenueCountAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException("Chốt phân bổ thu tiền không được tạo Doanh thu mới (C-004).");
        }
    }
}
