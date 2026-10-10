using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Fx;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Profitability;

public sealed record GetChargeProfitabilityQuery(Guid? BillId, Guid? OrderId, string View) : IRequest<ChargeProfitabilityDto>;

public sealed class GetChargeProfitabilityQueryHandler : IRequestHandler<GetChargeProfitabilityQuery, ChargeProfitabilityDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IReportingCurrencyProvider _reporting;

    public GetChargeProfitabilityQueryHandler(ILcmsDbContext db, ITenantContext tenant, IReportingCurrencyProvider reporting)
    {
        _db = db;
        _tenant = tenant;
        _reporting = reporting;
    }

    public async Task<ChargeProfitabilityDto> Handle(GetChargeProfitabilityQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var view = string.IsNullOrWhiteSpace(request.View) ? "best" : request.View.Trim().ToLowerInvariant();
        if (view is not ("expected" or "confirmed" or "actual" or "best"))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["View"] = ["Góc nhìn lợi nhuận phải là expected, confirmed, actual hoặc best."]
            });
        }

        var billIds = await ResolveBillsAsync(request, cancellationToken);
        var orderLinks = request.OrderId is null
            ? new Dictionary<Guid, int>()
            : await _db.OrderBillLinks.AsNoTracking()
                .Where(l => billIds.Contains(l.BillId))
                .GroupBy(l => l.BillId)
                .Select(g => new { g.Key, Count = g.Select(x => x.OrderId).Distinct().Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var reporting = await _reporting.GetAsync(cancellationToken);
        var names = await _db.EconomicChargeTypes.AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var costs = await _db.Costs.AsNoTracking()
            .Where(c => c.RecordStatus == "active" && c.BillId != null && billIds.Contains(c.BillId.Value))
            .ToListAsync(cancellationToken);
        var revenues = await _db.Revenues.AsNoTracking()
            .Where(r => r.RecordStatus == "active" && billIds.Contains(r.BillId))
            .ToListAsync(cancellationToken);

        var slices = new List<ChargeProfitSlice>();
        foreach (var cost in costs)
        {
            var expected = ReportingValue.Of(cost, cost.ExpectedAmount, reporting);
            var confirmed = cost.ConfirmedAmount is decimal confirmedAmount
                ? ReportingValue.Of(cost, confirmedAmount, reporting)
                : null;
            var actual = cost.ActualAmount is decimal actualAmount
                ? ReportingValue.Of(cost, actualAmount, reporting)
                : null;
            var costFactor = OrderFactor(request.OrderId, cost.BillId, orderLinks);
            slices.Add(Slice(
                cost.EconomicChargeTypeId,
                cost.CostTypeCode,
                names,
                "cost",
                cost.FinancialMaturity,
                Scale(expected, costFactor),
                Scale(confirmed, costFactor),
                Scale(actual, costFactor),
                cost.Id,
                "cost",
                cost.VendorPartyId,
                cost.BillId,
                expected is null,
                cost.ConfirmedAmount is not null && confirmed is null,
                cost.ActualAmount is not null && actual is null,
                costFactor));
        }

        foreach (var revenue in revenues)
        {
            var expected = ReportingValue.Of(revenue, revenue.ExpectedAmount, reporting);
            var confirmed = revenue.ConfirmedAmount is decimal confirmedAmount
                ? ReportingValue.Of(revenue, confirmedAmount, reporting)
                : null;
            var actual = revenue.ActualAmount is decimal actualAmount
                ? ReportingValue.Of(revenue, actualAmount, reporting)
                : null;
            var revenueFactor = OrderFactor(request.OrderId, revenue.BillId, orderLinks);
            slices.Add(Slice(
                revenue.EconomicChargeTypeId,
                revenue.RevenueTypeCode,
                names,
                "revenue",
                revenue.FinancialMaturity,
                Scale(expected, revenueFactor),
                Scale(confirmed, revenueFactor),
                Scale(actual, revenueFactor),
                revenue.Id,
                "revenue",
                revenue.CustomerPartyId,
                revenue.BillId,
                expected is null,
                revenue.ConfirmedAmount is not null && confirmed is null,
                revenue.ActualAmount is not null && actual is null,
                revenueFactor));
        }

        var allocated = await (
            from detail in _db.CostAllocationDetails.AsNoTracking()
            join allocation in _db.CostAllocations.AsNoTracking() on detail.AllocationId equals allocation.Id
            join cost in _db.Costs.AsNoTracking() on allocation.CostId equals cost.Id
            where billIds.Contains(detail.BillId)
                  && allocation.AllocationStatus == CostAllocationStatuses.Finalized
                  && cost.RecordStatus == "active"
            select new { detail, cost })
            .ToListAsync(cancellationToken);

        foreach (var row in allocated)
        {
            var share = row.detail.ManualOverrideAmount ?? row.detail.AllocatedAmount;
            var reportingShare = ReportingValue.Of(row.cost, share, reporting);
            var layers = ChargeProfitability.LayersForAllocation(row.cost.FinancialMaturity, reportingShare);
            var allocationFactor = OrderFactor(request.OrderId, row.detail.BillId, orderLinks);
            slices.Add(Slice(
                row.cost.EconomicChargeTypeId,
                row.cost.CostTypeCode,
                names,
                "cost",
                row.cost.FinancialMaturity,
                Scale(layers.Expected, allocationFactor),
                Scale(layers.Confirmed, allocationFactor),
                Scale(layers.Actual, allocationFactor),
                row.cost.Id,
                "allocation",
                row.cost.VendorPartyId,
                row.detail.BillId,
                layers.ExpectedFx,
                layers.ConfirmedFx,
                layers.ActualFx,
                allocationFactor));
        }

        return ChargeProfitability.Compose(view, reporting, slices);
    }

    private async Task<List<Guid>> ResolveBillsAsync(GetChargeProfitabilityQuery request, CancellationToken cancellationToken)
    {
        if (request.BillId is Guid billId)
        {
            var exists = await _db.Bills.AsNoTracking().AnyAsync(b => b.Id == billId, cancellationToken);
            if (!exists)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }

            return [billId];
        }

        if (request.OrderId is Guid orderId)
        {
            var exists = await _db.Orders.AsNoTracking().AnyAsync(o => o.Id == orderId, cancellationToken);
            if (!exists)
            {
                throw new NotFoundAppException("Không tìm thấy đơn hàng.");
            }

            return await _db.OrderBillLinks.AsNoTracking()
                .Where(l => l.OrderId == orderId)
                .Select(l => l.BillId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        throw new ValidationAppException(new Dictionary<string, string[]>
        {
            ["BillId"] = ["Chọn Bill hoặc đơn hàng."]
        });
    }

    private static ChargeProfitSlice Slice(
        Guid? chargeTypeId,
        string? code,
        IReadOnlyDictionary<Guid, string> names,
        string side,
        string maturity,
        decimal? expected,
        decimal? confirmed,
        decimal? actual,
        Guid sourceId,
        string sourceKind,
        Guid? partnerId,
        Guid? billId,
        bool expectedFx = false,
        bool confirmedFx = false,
        bool actualFx = false,
        decimal attributionFactor = 1m)
    {
        var chargeCode = string.IsNullOrWhiteSpace(code) ? "UNMAPPED" : code.Trim();
        var chargeName = chargeTypeId is Guid id && names.TryGetValue(id, out var name) ? name : chargeCode;
        return new ChargeProfitSlice(
            chargeTypeId,
            chargeCode,
            chargeName,
            side,
            maturity,
            expected,
            confirmed,
            actual,
            sourceId,
            sourceKind,
            partnerId,
            billId,
            expectedFx,
            confirmedFx,
            actualFx,
            attributionFactor);
    }

    private static decimal OrderFactor(Guid? orderId, Guid? billId, IReadOnlyDictionary<Guid, int> orderLinks)
    {
        if (orderId is null || billId is not Guid id)
        {
            return 1m;
        }

        return orderLinks.TryGetValue(id, out var links) && links > 1 ? 1m / links : 1m;
    }

    private static decimal? Scale(decimal? amount, decimal factor) =>
        amount is null || factor == 1m
            ? amount
            : decimal.Round(amount.Value * factor, 4, MidpointRounding.AwayFromZero);
}
