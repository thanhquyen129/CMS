using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Queries;

public sealed record CostAdjustmentDto(
    Guid Id,
    string AdjustmentType,
    decimal DeltaAmount,
    string CurrencyCode,
    string Reason,
    DateOnly EffectiveDate,
    string AppliedToMaturity,
    decimal AmountBefore,
    decimal AmountAfter,
    DateTimeOffset CreatedAt);

public sealed record CostAllocationDetailDto(
    Guid Id,
    Guid BillId,
    decimal BasisValue,
    decimal BasisRatio,
    decimal AllocatedAmount,
    decimal RoundingAdjustment,
    decimal? ManualOverrideAmount,
    string? OverrideReason);

public sealed record CostAllocationDto(
    Guid Id,
    int VersionNo,
    string AllocationBasis,
    string AllocationStatus,
    decimal AllocatableAmount,
    decimal AllocatedAmount,
    DateTimeOffset? FinalizedAt,
    IReadOnlyList<CostAllocationDetailDto> Details);

public sealed record CostDto(
    Guid Id,
    Guid? BillId,
    string AttributionType,
    string FinancialMaturity,
    decimal ExpectedAmount,
    decimal? ConfirmedAmount,
    decimal? ActualAmount,
    decimal Amount,
    string CurrencyCode,
    string? CostTypeCode,
    Guid? VendorPartyId,
    string? SourceType,
    Guid? SourceId,
    string RecordStatus,
    string ApprovalStatus,
    DateOnly EffectiveDate,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ActualizedAt,
    IReadOnlyList<CostAdjustmentDto> Adjustments,
    IReadOnlyList<CostAllocationDto> Allocations);

public sealed record CostListItemDto(
    Guid Id,
    Guid? BillId,
    string AttributionType,
    string FinancialMaturity,
    decimal Amount,
    string CurrencyCode,
    string? CostTypeCode,
    string RecordStatus,
    DateOnly EffectiveDate);

public sealed record GetCostByIdQuery(Guid Id) : IRequest<CostDto>;

public sealed class GetCostByIdQueryHandler : IRequestHandler<GetCostByIdQuery, CostDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetCostByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<CostDto> Handle(GetCostByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var cost = await _db.Costs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi phí.");

        var adjustments = await _db.CostAdjustments.AsNoTracking()
            .Where(a => a.CostId == cost.Id)
            .OrderBy(a => a.Id)
            .Select(a => new CostAdjustmentDto(
                a.Id,
                a.AdjustmentType,
                a.DeltaAmount,
                a.CurrencyCode,
                a.Reason,
                a.EffectiveDate,
                a.AppliedToMaturity,
                a.AmountBefore,
                a.AmountAfter,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        var allocations = await _db.CostAllocations.AsNoTracking()
            .Where(a => a.CostId == cost.Id)
            .OrderBy(a => a.VersionNo)
            .ToListAsync(cancellationToken);

        var allocationIds = allocations.Select(a => a.Id).ToList();
        var details = await _db.CostAllocationDetails.AsNoTracking()
            .Where(d => allocationIds.Contains(d.AllocationId))
            .ToListAsync(cancellationToken);
        var detailsByAlloc = details.GroupBy(d => d.AllocationId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.BillId).ToList());

        var allocationDtos = allocations.Select(a =>
        {
            detailsByAlloc.TryGetValue(a.Id, out var lines);
            lines ??= [];
            return new CostAllocationDto(
                a.Id,
                a.VersionNo,
                a.AllocationBasis,
                a.AllocationStatus,
                a.AllocatableAmount,
                a.AllocatedAmount,
                a.FinalizedAt,
                lines.Select(d => new CostAllocationDetailDto(
                    d.Id,
                    d.BillId,
                    d.BasisValue,
                    d.BasisRatio,
                    d.AllocatedAmount,
                    d.RoundingAdjustment,
                    d.ManualOverrideAmount,
                    d.OverrideReason)).ToList());
        }).ToList();

        return new CostDto(
            cost.Id,
            cost.BillId,
            cost.AttributionType,
            cost.FinancialMaturity,
            cost.ExpectedAmount,
            cost.ConfirmedAmount,
            cost.ActualAmount,
            cost.Amount,
            cost.CurrencyCode,
            cost.CostTypeCode,
            cost.VendorPartyId,
            cost.SourceType,
            cost.SourceId,
            cost.RecordStatus,
            cost.ApprovalStatus,
            cost.EffectiveDate,
            cost.ConfirmedAt,
            cost.ActualizedAt,
            adjustments,
            allocationDtos);
    }
}

public sealed record ListCostsQuery(Guid? BillId, string? FinancialMaturity) : IRequest<IReadOnlyList<CostListItemDto>>;

public sealed class ListCostsQueryHandler : IRequestHandler<ListCostsQuery, IReadOnlyList<CostListItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListCostsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<CostListItemDto>> Handle(ListCostsQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.Costs.AsNoTracking().AsQueryable();
        if (request.BillId.HasValue)
        {
            query = query.Where(c => c.BillId == request.BillId);
        }

        if (!string.IsNullOrWhiteSpace(request.FinancialMaturity))
        {
            var maturity = request.FinancialMaturity.Trim().ToLowerInvariant();
            query = query.Where(c => c.FinancialMaturity == maturity);
        }

        return await query
            .OrderByDescending(c => c.EffectiveDate)
            .ThenBy(c => c.Id)
            .Select(c => new CostListItemDto(
                c.Id,
                c.BillId,
                c.AttributionType,
                c.FinancialMaturity,
                c.Amount,
                c.CurrencyCode,
                c.CostTypeCode,
                c.RecordStatus,
                c.EffectiveDate))
            .ToListAsync(cancellationToken);
    }
}
