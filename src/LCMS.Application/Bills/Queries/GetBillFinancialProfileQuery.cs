using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Terminology;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

/// <summary>
/// Derived Bill financial profile (TD1-DB-003/004) — never stored as SoT on Bill.
/// Best Available per line: Actual → Confirmed → Expected. Totals split by currency_code.
/// Sprint 11: maturity breakdown, allocated cost, settlement outstanding, optional asOf filter.
/// </summary>
public sealed record GetBillFinancialProfileQuery(Guid BillId, DateOnly? AsOf = null)
    : IRequest<BillFinancialProfileDto>;

public sealed record MaturityBreakdownDto(
    decimal ExpectedTotal,
    decimal ConfirmedTotal,
    decimal ActualTotal);

public sealed record SettlementOutstandingBucketDto(
    string CurrencyCode,
    decimal AccountsPayableOutstanding,
    decimal AccountsReceivableOutstanding);

public sealed record BillFinancialProfileDto(
    Guid BillId,
    string BillNo,
    string ViewKind,
    DateTimeOffset AsOfTimestamp,
    DateOnly? AsOfFilter,
    IReadOnlyList<CurrencyFinancialBucketDto> ByCurrency,
    IReadOnlyList<SettlementOutstandingBucketDto> SettlementOutstanding,
    bool HasMixedCurrencies,
    string Note,
    string? AsOfLimitationNote);

public sealed record CurrencyFinancialBucketDto(
    string CurrencyCode,
    decimal RevenueBestAvailable,
    decimal CostBestAvailable,
    decimal ProfitBestAvailable,
    decimal DirectCostBestAvailable,
    decimal AllocatedCostAmount,
    MaturityBreakdownDto RevenueMaturity,
    MaturityBreakdownDto DirectCostMaturity,
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

        var asOfTimestamp = DateTimeOffset.UtcNow;
        var asOf = request.AsOf;

        var bill = await _db.Bills.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy Bill.");

        var revenuesQuery = _db.Revenues.AsNoTracking()
            .Where(r => r.BillId == bill.Id && r.RecordStatus == "active");
        if (asOf.HasValue)
        {
            revenuesQuery = revenuesQuery.Where(r => r.EffectiveDate <= asOf.Value);
        }

        var revenues = await revenuesQuery.ToListAsync(cancellationToken);

        var directCostsQuery = _db.Costs.AsNoTracking()
            .Where(c => c.BillId == bill.Id
                        && c.AttributionType == CostAttributionTypes.Direct
                        && c.RecordStatus == "active");
        if (asOf.HasValue)
        {
            directCostsQuery = directCostsQuery.Where(c => c.EffectiveDate <= asOf.Value);
        }

        var directCosts = await directCostsQuery.ToListAsync(cancellationToken);

        // Finalized allocated share of shared costs onto this Bill (thin slice: frozen allocated_amount).
        // asOf FinalizedAt filter applied in-memory — SQLite cannot translate DateTimeOffset comparisons reliably.
        var allocatedRaw = await (
            from d in _db.CostAllocationDetails.AsNoTracking()
            join a in _db.CostAllocations.AsNoTracking() on d.AllocationId equals a.Id
            join c in _db.Costs.AsNoTracking() on a.CostId equals c.Id
            where d.BillId == bill.Id
                  && a.AllocationStatus == CostAllocationStatuses.Finalized
                  && c.RecordStatus == "active"
            select new { d.AllocatedAmount, c.CurrencyCode, a.FinalizedAt }
        ).ToListAsync(cancellationToken);

        var allocatedLines = allocatedRaw.AsEnumerable();
        if (asOf.HasValue)
        {
            var asOfDate = asOf.Value;
            allocatedLines = allocatedLines.Where(x =>
                x.FinalizedAt.HasValue
                && DateOnly.FromDateTime(x.FinalizedAt.Value.UtcDateTime) <= asOfDate);
        }

        var allocatedList = allocatedLines.ToList();

        var apRows = await _db.AccountsPayable.AsNoTracking()
            .Where(a => a.BillId == bill.Id && a.RecordStatus == "active")
            .ToListAsync(cancellationToken);
        var arRows = await _db.AccountsReceivable.AsNoTracking()
            .Where(a => a.BillId == bill.Id && a.RecordStatus == "active")
            .ToListAsync(cancellationToken);

        if (asOf.HasValue)
        {
            var asOfDate = asOf.Value;
            apRows = apRows
                .Where(a => DateOnly.FromDateTime(a.RecognizedAt.UtcDateTime) <= asOfDate)
                .ToList();
            arRows = arRows
                .Where(a => DateOnly.FromDateTime(a.RecognizedAt.UtcDateTime) <= asOfDate)
                .ToList();
        }

        var currencyCodes = revenues.Select(r => r.CurrencyCode)
            .Concat(directCosts.Select(c => c.CurrencyCode))
            .Concat(allocatedList.Select(a => a.CurrencyCode))
            .Concat(apRows.Select(a => a.CurrencyCode))
            .Concat(arRows.Select(a => a.CurrencyCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var buckets = new List<CurrencyFinancialBucketDto>();
        foreach (var code in currencyCodes)
        {
            var revLines = revenues
                .Where(r => string.Equals(r.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var costLines = directCosts
                .Where(c => string.Equals(c.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var allocLines = allocatedList
                .Where(a => string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();

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
                new MaturityBreakdownDto(
                    decimal.Round(revLines.Sum(r => r.ExpectedAmount), 4, MidpointRounding.AwayFromZero),
                    decimal.Round(revLines.Sum(r => r.ConfirmedAmount ?? 0m), 4, MidpointRounding.AwayFromZero),
                    decimal.Round(revLines.Sum(r => r.ActualAmount ?? 0m), 4, MidpointRounding.AwayFromZero)),
                new MaturityBreakdownDto(
                    decimal.Round(costLines.Sum(c => c.ExpectedAmount), 4, MidpointRounding.AwayFromZero),
                    decimal.Round(costLines.Sum(c => c.ConfirmedAmount ?? 0m), 4, MidpointRounding.AwayFromZero),
                    decimal.Round(costLines.Sum(c => c.ActualAmount ?? 0m), 4, MidpointRounding.AwayFromZero)),
                revLines.Count,
                costLines.Count,
                allocLines.Count));
        }

        var settlement = currencyCodes
            .Select(code =>
            {
                var apOut = apRows
                    .Where(a => string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                    .Sum(a => a.DeriveOutstanding());
                var arOut = arRows
                    .Where(a => string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                    .Sum(a => a.DeriveOutstanding());
                return new SettlementOutstandingBucketDto(
                    code.ToUpperInvariant(),
                    decimal.Round(apOut, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(arOut, 4, MidpointRounding.AwayFromZero));
            })
            .Where(s => s.AccountsPayableOutstanding != 0m || s.AccountsReceivableOutstanding != 0m)
            .ToList();

        var mixed = buckets.Count > 1;
        var profileLabel = VietnameseUiTerms.Get("BILL_FINANCIAL_PROFILE");
        var bestAvailableLabel = VietnameseUiTerms.Get("BEST_AVAILABLE");
        string note;
        if (mixed)
        {
            note =
                $"{profileLabel}: Không cộng gộp số tiền khác loại tiền tệ; xem từng currency_code. " +
                $"{bestAvailableLabel} là read model, không phải SoT trên Bill.";
        }
        else
        {
            note =
                $"{profileLabel}: {bestAvailableLabel} = Actual → Confirmed → Expected. " +
                "Totals derive từ Cost/Revenue/Allocation/AP-AR; không lưu SoT trên Bill.";
        }

        string? asOfLimitation = null;
        if (asOf.HasValue)
        {
            asOfLimitation =
                "asOf lọc theo EffectiveDate (Cost/Revenue), FinalizedAt (allocation), RecognizedAt (AP/AR). " +
                "Không reconstruct lịch sử maturity layer tại thời điểm asOf (deferred Pass 2).";
        }

        return new BillFinancialProfileDto(
            bill.Id,
            bill.BillNo,
            "best_available",
            asOfTimestamp,
            asOf,
            buckets,
            settlement,
            mixed,
            note,
            asOfLimitation);
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
