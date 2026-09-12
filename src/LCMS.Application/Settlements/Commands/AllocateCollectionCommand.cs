using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.FinancialCloses;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record AllocateCollectionCommand(
    Guid CollectionId,
    Guid AccountsReceivableId,
    decimal Amount,
    string? Notes) : IRequest<Guid>;

public sealed class AllocateCollectionCommandValidator : AbstractValidator<AllocateCollectionCommand>
{
    public AllocateCollectionCommandValidator()
    {
        RuleFor(x => x.CollectionId).NotEmpty().WithMessage("Thu tiền không hợp lệ.");
        RuleFor(x => x.AccountsReceivableId).NotEmpty().WithMessage("Khoản phải thu không hợp lệ.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền phân bổ thu tiền phải lớn hơn 0.");
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
    }
}

/// <summary>
/// Draft allocation Collection → AR. Does NOT change outstanding (AC-007).
/// Enforces C-008 ceilings (over policy stub = 0).
/// Supports multi-allocation until Unapplied/AvailableToAllocate is exhausted.
/// Period lock when financial close is Locked (Pass 2 Sprint 10 FULL).
/// </summary>
public sealed class AllocateCollectionCommandHandler : IRequestHandler<AllocateCollectionCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISettlementFxStub _fx;
    private readonly IPeriodLockGate _periodLockGate;

    public AllocateCollectionCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ISettlementFxStub fx,
        IPeriodLockGate periodLockGate)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fx = fx;
        _periodLockGate = periodLockGate;
    }

    public async Task<Guid> Handle(AllocateCollectionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);

        var collection = await _db.Collections
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thu tiền.");

        if (collection.Status == CollectionStatuses.Cancelled)
        {
            throw new ConflictAppException("Không thể phân bổ thu tiền đã hủy.");
        }

        var ar = await _db.AccountsReceivable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsReceivableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");

        await _periodLockGate.EnsureMutationAllowedAsync(
            collection.BillId ?? ar.BillId,
            collection.ValueDate,
            "phân bổ thu tiền",
            cancellationToken);

        if (!string.Equals(collection.CurrencyCode, ar.CurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không phân bổ khác tiền tệ (C-014).");
        }

        var collectionActiveAmounts = await _db.CollectionAllocations.AsNoTracking()
            .Where(a => a.CollectionId == collection.Id
                && (a.AllocationStatus == SettlementAllocationStatuses.Draft
                    || a.AllocationStatus == SettlementAllocationStatuses.Finalized))
            .Select(a => a.Amount)
            .ToListAsync(cancellationToken);
        var collectionActive = collectionActiveAmounts.Sum();

        var collectionRemaining = collection.Amount - collectionActive + SettlementHelpers.OverSettlementTolerance;
        if (amount > collectionRemaining)
        {
            throw new ConflictAppException(
                $"Tổng phân bổ vượt số tiền thu (còn lại {collectionRemaining}) (C-008).");
        }

        var arActiveAmounts = await _db.CollectionAllocations.AsNoTracking()
            .Where(a => a.AccountsReceivableId == ar.Id
                && (a.AllocationStatus == SettlementAllocationStatuses.Draft
                    || a.AllocationStatus == SettlementAllocationStatuses.Finalized))
            .Select(a => a.Amount)
            .ToListAsync(cancellationToken);
        var arActive = arActiveAmounts.Sum();

        var arCeiling = ar.RecognizedAmount + ar.AdjustmentAmount + SettlementHelpers.OverSettlementTolerance;
        if (arActive + amount > arCeiling)
        {
            throw new ConflictAppException(
                "Tổng phân bổ vượt số dư còn lại của khoản phải thu (C-008).");
        }

        var revenueCountBefore = await _db.Revenues.CountAsync(cancellationToken);

        var allocation = new CollectionAllocation
        {
            TenantId = tenantId,
            CollectionId = collection.Id,
            AccountsReceivableId = ar.Id,
            Amount = amount,
            AllocationStatus = SettlementAllocationStatuses.Draft,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };
        await _fx.ApplyToCollectionAllocationAsync(
            allocation,
            collection.CurrencyCode,
            amount,
            collection.ValueDate,
            cancellationToken);

        _db.CollectionAllocations.Add(allocation);
        await _db.SaveChangesAsync(cancellationToken);

        var revenueCountAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException("Phân bổ thu tiền không được tạo Doanh thu mới (C-004).");
        }

        return allocation.Id;
    }
}
