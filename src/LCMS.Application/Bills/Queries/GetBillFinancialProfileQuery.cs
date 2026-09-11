using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

/// <summary>
/// Derived Bill financial profile (TD1-DB-003/004) — never stored as SoT on Bill.
/// Best Available per line: Actual → Confirmed → Expected. Totals split by currency_code.
/// </summary>
public sealed record GetBillFinancialProfileQuery(Guid BillId) : IRequest<BillFinancialProfileDto>;

public sealed record BillFinancialProfileDto(
    Guid BillId,
    string BillNo,
    string ViewKind,
    IReadOnlyList<CurrencyFinancialBucketDto> ByCurrency,
    bool HasMixedCurrencies,
    string Note);

public sealed record CurrencyFinancialBucketDto(
    string CurrencyCode,
    decimal RevenueBestAvailable,
    decimal CostBestAvailable,
    decimal ProfitBestAvailable,
    decimal DirectCostBestAvailable,
    decimal AllocatedCostAmount,
    int RevenueLineCount,
    int DirectCostLineCount,
    int AllocatedCostLineCount);

public sealed class GetBillFinancialProfileQueryHandler
    : IRequestHandler<GetBillFinancialProfileQuery, BillFinancialProfileDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetBillFinancialProfileQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BillFinancialProfileDto> Handle(
        GetBillFinancialProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var bill = await _db.Bills.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy Bill.");

        var revenues = await _db.Revenues.AsNoTracking()
            .Where(r => r.BillId == bill.Id && r.RecordStatus == "active")
            .ToListAsync(cancellationToken);

        var directCosts = await _db.Costs.AsNoTracking()
            .Where(c => c.BillId == bill.Id
                        && c.AttributionType == CostAttributionTypes.Direct
                        && c.RecordStatus == "active")
            .ToListAsync(cancellationToken);

        // Finalized allocated share of shared costs onto this Bill (thin slice: use frozen allocated_amount).
        var allocatedLines = await (
            from d in _db.CostAllocationDetails.AsNoTracking()
            join a in _db.CostAllocations.AsNoTracking() on d.AllocationId equals a.Id
            join c in _db.Costs.AsNoTracking() on a.CostId equals c.Id
            where d.BillId == bill.Id
                  && a.AllocationStatus == CostAllocationStatuses.Finalized
                  && c.RecordStatus == "active"
            select new { d.AllocatedAmount, c.CurrencyCode }
        ).ToListAsync(cancellationToken);

        var currencyCodes = revenues.Select(r => r.CurrencyCode)
            .Concat(directCosts.Select(c => c.CurrencyCode))
            .Concat(allocatedLines.Select(a => a.CurrencyCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var buckets = new List<CurrencyFinancialBucketDto>();
        foreach (var code in currencyCodes)
        {
            var revLines = revenues.Where(r => string.Equals(r.CurrencyCode, code, StringComparison.OrdinalIgnoreCase)).ToList();
            var costLines = directCosts.Where(c => string.Equals(c.CurrencyCode, code, StringComparison.OrdinalIgnoreCase)).ToList();
            var allocLines = allocatedLines.Where(a => string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase)).ToList();

            var revenueTotal = revLines.Sum(r => BestAvailable(r.ActualAmount, r.ConfirmedAmount, r.ExpectedAmount));
            var directTotal = costLines.Sum(c => BestAvailable(c.ActualAmount, c.ConfirmedAmount, c.ExpectedAmount));
            var allocatedTotal = allocLines.Sum(a => a.AllocatedAmount);
            var costTotal = decimal.Round(directTotal + allocatedTotal, 4, MidpointRounding.AwayFromZero);
            var profit = decimal.Round(revenueTotal - costTotal, 4, MidpointRounding.AwayFromZero);

            buckets.Add(new CurrencyFinancialBucketDto(
                code.ToUpperInvariant(),
                decimal.Round(revenueTotal, 4, MidpointRounding.AwayFromZero),
                costTotal,
                profit,
                decimal.Round(directTotal, 4, MidpointRounding.AwayFromZero),
                decimal.Round(allocatedTotal, 4, MidpointRounding.AwayFromZero),
                revLines.Count,
                costLines.Count,
                allocLines.Count));
        }

        var mixed = buckets.Count > 1;
        return new BillFinancialProfileDto(
            bill.Id,
            bill.BillNo,
            "best_available",
            buckets,
            mixed,
            mixed
                ? "Không cộng gộp số tiền khác loại tiền tệ; xem từng currency_code. Totals là read model, không phải SoT trên Bill."
                : "Best Available = Actual → Confirmed → Expected. Totals derive từ Cost/Revenue; không lưu SoT trên Bill.");
    }

    private static decimal BestAvailable(decimal? actual, decimal? confirmed, decimal expected)
    {
        if (actual.HasValue)
        {
            return actual.Value;
        }

        if (confirmed.HasValue)
        {
            return confirmed.Value;
        }

        return expected;
    }
}
