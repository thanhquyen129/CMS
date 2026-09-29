using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Fx;
using LCMS.Application.Revenues;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using LCMS.Domain.Terminology;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

/// <summary>
/// Derived Bill financial profile (TD1-DB-003/004) — never stored as SoT on Bill.
/// Best Available per line: Actual → Confirmed → Expected. Totals split by currency_code.
/// Sprint 11 FULL: asOf reconstructs maturity via ConfirmedAt/ActualizedAt; settlement outstanding
/// from finalized allocations at asOf where available.
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
    string? AsOfLimitationNote,
    BillReportingSummaryDto? Reporting = null);

/// <summary>Bill totals in the tenant reporting currency only (FX-ARCH-10); lines carry the FX trace.</summary>
public sealed record BillReportingSummaryDto(
    string ReportingCurrencyCode,
    bool CanViewRevenue,
    bool CanViewCost,
    decimal? RevenueBestAvailable,
    decimal? CostBestAvailable,
    decimal? DirectCostBestAvailable,
    decimal? AllocatedCostAmount,
    decimal? ProfitBestAvailable,
    decimal? MarginPercent,
    MaturityBreakdownDto? RevenueMaturity,
    MaturityBreakdownDto? DirectCostMaturity,
    decimal? AccountsPayableOutstanding,
    decimal? AccountsReceivableOutstanding,
    int MissingFxCount,
    bool Complete,
    IReadOnlyList<ReportingLineDto> Lines);

public sealed record CurrencyFinancialBucketDto(
    string CurrencyCode,
    decimal RevenueBestAvailable,
    decimal CostBestAvailable,
    decimal ProfitBestAvailable,
    decimal DirectCostBestAvailable,
    decimal AllocatedCostAmount,
    MaturityBreakdownDto RevenueMaturity,
    MaturityBreakdownDto DirectCostMaturity,
    decimal RevenueVarianceExpectedVsActual,
    decimal DirectCostVarianceExpectedVsActual,
    decimal ProfitVarianceExpectedVsActual,
    int RevenueLineCount,
    int DirectCostLineCount,
    int AllocatedCostLineCount);

public sealed class GetBillFinancialProfileQueryHandler
    : IRequestHandler<GetBillFinancialProfileQuery, BillFinancialProfileDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IReportingCurrencyProvider _reporting;
    private readonly IPermissionService _permissions;

    public GetBillFinancialProfileQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IReportingCurrencyProvider reporting,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _reporting = reporting;
        _permissions = permissions;
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
            select new
            {
                d.AllocatedAmount,
                c.CurrencyCode,
                a.FinalizedAt,
                AllocationId = a.Id,
                DetailId = d.Id,
                CostId = c.Id,
                c.FxRate,
                c.FxStatus,
                c.FxSourceType,
                c.FxSourceName,
                c.FxRateDate
            }
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

        var apIds = apRows.Select(a => a.Id).ToList();
        var arIds = arRows.Select(a => a.Id).ToList();

        var paymentAllocs = apIds.Count == 0
            ? []
            : await _db.PaymentAllocations.AsNoTracking()
                .Where(p => apIds.Contains(p.AccountsPayableId))
                .ToListAsync(cancellationToken);
        var collectionAllocs = arIds.Count == 0
            ? []
            : await _db.CollectionAllocations.AsNoTracking()
                .Where(c => arIds.Contains(c.AccountsReceivableId))
                .ToListAsync(cancellationToken);

        var apAdjs = apIds.Count == 0
            ? []
            : await _db.AccountsPayableAdjustments.AsNoTracking()
                .Where(a => apIds.Contains(a.AccountsPayableId))
                .Select(a => new { a.AccountsPayableId, a.DeltaAmount, a.EffectiveDate })
                .ToListAsync(cancellationToken);
        var arAdjs = arIds.Count == 0
            ? []
            : await _db.AccountsReceivableAdjustments.AsNoTracking()
                .Where(a => arIds.Contains(a.AccountsReceivableId))
                .Select(a => new { a.AccountsReceivableId, a.DeltaAmount, a.EffectiveDate })
                .ToListAsync(cancellationToken);

        decimal AdjustmentAtAsOf(
            Guid accountsId,
            decimal liveAdjustment,
            bool payable)
        {
            if (!asOf.HasValue)
            {
                return liveAdjustment;
            }

            var asOfDate = asOf.Value;
            if (payable)
            {
                return apAdjs
                    .Where(a => a.AccountsPayableId == accountsId && a.EffectiveDate <= asOfDate)
                    .Sum(a => a.DeltaAmount);
            }

            return arAdjs
                .Where(a => a.AccountsReceivableId == accountsId && a.EffectiveDate <= asOfDate)
                .Sum(a => a.DeltaAmount);
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

            var revProjected = revLines
                .Select(r => ProjectMaturity(r.ExpectedAmount, r.ConfirmedAmount, r.ConfirmedAt, r.ActualAmount, r.ActualizedAt, asOf))
                .ToList();
            var costProjected = costLines
                .Select(c => ProjectMaturity(c.ExpectedAmount, c.ConfirmedAmount, c.ConfirmedAt, c.ActualAmount, c.ActualizedAt, asOf))
                .ToList();

            var revenueTotal = revProjected.Sum(p => p.BestAvailable);
            var directTotal = costProjected.Sum(p => p.BestAvailable);
            var allocatedTotal = allocLines.Sum(a => a.AllocatedAmount);
            var costTotal = decimal.Round(directTotal + allocatedTotal, 4, MidpointRounding.AwayFromZero);
            var profit = decimal.Round(revenueTotal - costTotal, 4, MidpointRounding.AwayFromZero);

            var revExpected = revProjected.Sum(p => p.Expected);
            var revConfirmed = revProjected.Sum(p => p.Confirmed ?? 0m);
            var revActual = revProjected.Sum(p => p.Actual ?? 0m);
            var costExpected = costProjected.Sum(p => p.Expected);
            var costConfirmed = costProjected.Sum(p => p.Confirmed ?? 0m);
            var costActual = costProjected.Sum(p => p.Actual ?? 0m);
            // Allocated is frozen actual share — included in both expected/actual cost views for variance fairness.
            var profitExpected = revExpected - (costExpected + allocatedTotal);
            var profitActual = revActual - (costActual + allocatedTotal);

            buckets.Add(new CurrencyFinancialBucketDto(
                code.ToUpperInvariant(),
                decimal.Round(revenueTotal, 4, MidpointRounding.AwayFromZero),
                costTotal,
                profit,
                decimal.Round(directTotal, 4, MidpointRounding.AwayFromZero),
                decimal.Round(allocatedTotal, 4, MidpointRounding.AwayFromZero),
                new MaturityBreakdownDto(
                    decimal.Round(revExpected, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(revConfirmed, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(revActual, 4, MidpointRounding.AwayFromZero)),
                new MaturityBreakdownDto(
                    decimal.Round(costExpected, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(costConfirmed, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(costActual, 4, MidpointRounding.AwayFromZero)),
                decimal.Round(revExpected - revActual, 4, MidpointRounding.AwayFromZero),
                decimal.Round(costExpected - costActual, 4, MidpointRounding.AwayFromZero),
                decimal.Round(profitExpected - profitActual, 4, MidpointRounding.AwayFromZero),
                revLines.Count,
                costLines.Count,
                allocLines.Count));
        }

        var settlement = currencyCodes
            .Select(code =>
            {
                var apOut = apRows
                    .Where(a => string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                    .Sum(a => asOf.HasValue
                        ? OutstandingAtAsOf(
                            a.RecognizedAmount,
                            AdjustmentAtAsOf(a.Id, a.AdjustmentAmount, payable: true),
                            paymentAllocs.Where(p => p.AccountsPayableId == a.Id)
                                .Select(p => new SettlementAllocSlice(
                                    p.Amount, p.AllocationStatus, p.FinalizedAt, p.ReversedAt)),
                            asOf.Value)
                        : a.DeriveOutstanding());
                var arOut = arRows
                    .Where(a => string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                    .Sum(a => asOf.HasValue
                        ? OutstandingAtAsOf(
                            a.RecognizedAmount,
                            AdjustmentAtAsOf(a.Id, a.AdjustmentAmount, payable: false),
                            collectionAllocs.Where(c => c.AccountsReceivableId == a.Id)
                                .Select(c => new SettlementAllocSlice(
                                    c.Amount, c.AllocationStatus, c.FinalizedAt, c.ReversedAt)),
                            asOf.Value)
                        : a.DeriveOutstanding());
                return new SettlementOutstandingBucketDto(
                    code.ToUpperInvariant(),
                    decimal.Round(apOut, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(arOut, 4, MidpointRounding.AwayFromZero));
            })
            .Where(s => s.AccountsPayableOutstanding != 0m || s.AccountsReceivableOutstanding != 0m)
            .ToList();

        var reportingSummary = await BuildReportingAsync(
            revenues,
            directCosts,
            allocatedList.Select(a => new AllocatedLine(
                a.AllocationId, a.DetailId, a.CostId, a.AllocatedAmount, a.CurrencyCode,
                a.FxRate, a.FxStatus, a.FxSourceType, a.FxSourceName, a.FxRateDate)).ToList(),
            apRows.Select(a => (Row: a, Outstanding: asOf.HasValue
                ? OutstandingAtAsOf(
                    a.RecognizedAmount,
                    AdjustmentAtAsOf(a.Id, a.AdjustmentAmount, payable: true),
                    paymentAllocs.Where(p => p.AccountsPayableId == a.Id)
                        .Select(p => new SettlementAllocSlice(p.Amount, p.AllocationStatus, p.FinalizedAt, p.ReversedAt)),
                    asOf.Value)
                : a.DeriveOutstanding())).ToList(),
            arRows.Select(a => (Row: a, Outstanding: asOf.HasValue
                ? OutstandingAtAsOf(
                    a.RecognizedAmount,
                    AdjustmentAtAsOf(a.Id, a.AdjustmentAmount, payable: false),
                    collectionAllocs.Where(c => c.AccountsReceivableId == a.Id)
                        .Select(c => new SettlementAllocSlice(c.Amount, c.AllocationStatus, c.FinalizedAt, c.ReversedAt)),
                    asOf.Value)
                : a.DeriveOutstanding())).ToList(),
            asOf,
            cancellationToken);

        var mixed = buckets.Count > 1;
        var profileLabel = VietnameseUiTerms.Get("BILL_FINANCIAL_PROFILE");
        var bestAvailableLabel = VietnameseUiTerms.Get("BEST_AVAILABLE");
        var varianceLabel = VietnameseUiTerms.Get("VARIANCE_EXPECTED_VS_ACTUAL");
        string note;
        if (mixed)
        {
            note =
                $"{profileLabel}: Không cộng gộp số tiền khác loại tiền tệ; xem từng currency_code. " +
                $"{bestAvailableLabel} là read model, không phải SoT trên Bill. " +
                $"{varianceLabel} = Expected − Actual (Actual thiếu = 0); allocated cost luôn cộng vào Cost.";
        }
        else
        {
            note =
                $"{profileLabel}: {bestAvailableLabel} = Actual → Confirmed → Expected. " +
                $"{varianceLabel} = Expected − Actual (Actual thiếu = 0). " +
                "Totals derive từ Cost/Revenue/Allocation/AP-AR; không lưu SoT trên Bill.";
        }

        string? asOfLimitation = null;
        if (asOf.HasValue)
        {
            asOfLimitation =
                "asOf: reconstruct maturity qua ConfirmedAt/ActualizedAt; allocation theo FinalizedAt; " +
                "AP/AR outstanding theo RecognizedAt + điều chỉnh ledger (EffectiveDate ≤ asOf) + phân bổ đã chốt tại asOf. " +
                "Giới hạn còn lại: Cost/Revenue adjustment layer replay chưa đầy đủ; AP đã reverse_recognize sau asOf có thể thiếu trên profile.";
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
            asOfLimitation,
            reportingSummary);
    }

    private sealed record AllocatedLine(
        Guid AllocationId,
        Guid DetailId,
        Guid CostId,
        decimal Amount,
        string CurrencyCode,
        decimal? FxRate,
        string FxStatus,
        string? FxSourceType,
        string? FxSourceName,
        DateOnly? FxRateDate);

    private async Task<BillReportingSummaryDto> BuildReportingAsync(
        IReadOnlyList<Revenue> revenues,
        IReadOnlyList<Cost> directCosts,
        IReadOnlyList<AllocatedLine> allocated,
        IReadOnlyList<(AccountsPayable Row, decimal Outstanding)> payables,
        IReadOnlyList<(AccountsReceivable Row, decimal Outstanding)> receivables,
        DateOnly? asOf,
        CancellationToken cancellationToken)
    {
        var reporting = await _reporting.GetAsync(cancellationToken);
        var canRevenue = await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken);
        var canCost = await _permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken);
        var lines = new List<ReportingLineDto>();

        ReportingSum revBest = new(), revExp = new(), revConf = new(), revAct = new();
        foreach (var r in revenues)
        {
            var p = ProjectMaturity(r.ExpectedAmount, r.ConfirmedAmount, r.ConfirmedAt, r.ActualAmount, r.ActualizedAt, asOf);
            revBest.Add(ReportingValue.Of(r, p.BestAvailable, reporting));
            revExp.Add(ReportingValue.Of(r, p.Expected, reporting));
            revConf.Add(ReportingValue.Of(r, p.Confirmed ?? 0m, reporting));
            revAct.Add(ReportingValue.Of(r, p.Actual ?? 0m, reporting));
            lines.Add(ReportingValue.Line("revenue", r.Id, r.RevenueTypeCode, r, p.BestAvailable, reporting));
        }

        ReportingSum costBest = new(), costExp = new(), costConf = new(), costAct = new();
        foreach (var c in directCosts)
        {
            var p = ProjectMaturity(c.ExpectedAmount, c.ConfirmedAmount, c.ConfirmedAt, c.ActualAmount, c.ActualizedAt, asOf);
            costBest.Add(ReportingValue.Of(c, p.BestAvailable, reporting));
            costExp.Add(ReportingValue.Of(c, p.Expected, reporting));
            costConf.Add(ReportingValue.Of(c, p.Confirmed ?? 0m, reporting));
            costAct.Add(ReportingValue.Of(c, p.Actual ?? 0m, reporting));
            lines.Add(ReportingValue.Line("cost", c.Id, c.CostTypeCode, c, p.BestAvailable, reporting));
        }

        var allocationIds = allocated.Select(a => a.AllocationId).Distinct().ToList();
        var allDetails = allocationIds.Count == 0
            ? []
            : await _db.CostAllocationDetails.AsNoTracking()
                .Where(d => allocationIds.Contains(d.AllocationId))
                .Select(d => new AllocationDetailSlice(d.AllocationId, d.Id, d.AllocatedAmount))
                .ToListAsync(cancellationToken);
        var fxByAllocation = allocated
            .GroupBy(a => a.AllocationId)
            .ToDictionary(g => g.Key, g => new AllocationFx(g.First().CurrencyCode, g.First().FxRate, g.First().FxStatus));
        var shares = ReportingAllocation.Split(allDetails, fxByAllocation, reporting);
        var alloc = new ReportingSum();
        foreach (var a in allocated)
        {
            decimal? share = shares.TryGetValue(a.DetailId, out var s) ? s : null;
            alloc.Add(share);
            var same = ReportingValue.SameCurrency(a.CurrencyCode, reporting);
            lines.Add(new ReportingLineDto(
                "allocated",
                a.CostId,
                "Phân bổ chi phí chung",
                a.CurrencyCode.ToUpperInvariant(),
                decimal.Round(a.Amount, 4, MidpointRounding.AwayFromZero),
                same ? 1m : a.FxRate,
                same ? FxSourceTypes.Identity : a.FxSourceType,
                same ? null : a.FxSourceName,
                same ? null : a.FxRateDate,
                share,
                share is null ? FxStatuses.Missing : FxStatuses.Converted));
        }

        var apOut = new ReportingSum();
        foreach (var (row, outstanding) in payables)
        {
            apOut.Add(ReportingValue.Of(row, outstanding, reporting));
            lines.Add(ReportingValue.Line("ap", row.Id, "Công nợ phải trả còn lại", row, outstanding, reporting));
        }

        var arOut = new ReportingSum();
        foreach (var (row, outstanding) in receivables)
        {
            arOut.Add(ReportingValue.Of(row, outstanding, reporting));
            lines.Add(ReportingValue.Line("ar", row.Id, "Công nợ phải thu còn lại", row, outstanding, reporting));
        }

        var costTotal = costBest.Rounded + alloc.Rounded;
        var profit = revBest.Rounded - costTotal;
        var costMissing = costBest.Missing + alloc.Missing + apOut.Missing;
        var revenueMissing = revBest.Missing + arOut.Missing;
        var missing = (canCost ? costMissing : 0) + (canRevenue ? revenueMissing : 0);
        var visibleLines = lines
            .Where(l => l.Kind is "revenue" or "ar" ? canRevenue : canCost)
            .ToList();
        var both = canRevenue && canCost;

        return new BillReportingSummaryDto(
            reporting,
            canRevenue,
            canCost,
            canRevenue ? revBest.Rounded : null,
            canCost ? costTotal : null,
            canCost ? costBest.Rounded : null,
            canCost ? alloc.Rounded : null,
            both ? profit : null,
            both && revBest.Complete && costBest.Complete && alloc.Complete
                ? ProfitabilityShare.MarginPercent(revBest.Rounded, profit)
                : null,
            canRevenue ? new MaturityBreakdownDto(revExp.Rounded, revConf.Rounded, revAct.Rounded) : null,
            canCost ? new MaturityBreakdownDto(costExp.Rounded, costConf.Rounded, costAct.Rounded) : null,
            canCost ? apOut.Rounded : null,
            canRevenue ? arOut.Rounded : null,
            missing,
            missing == 0,
            visibleLines);
    }

    /// <summary>
    /// Projects maturity layers visible at asOf. Without asOf, uses live layers.
    /// Missing ConfirmedAt/ActualizedAt with amount present → treat as present (residual limit).
    /// </summary>
    internal static ProjectedMaturity ProjectMaturity(
        decimal expected,
        decimal? confirmedAmount,
        DateTimeOffset? confirmedAt,
        decimal? actualAmount,
        DateTimeOffset? actualizedAt,
        DateOnly? asOf)
    {
        decimal? confirmed = confirmedAmount;
        decimal? actual = actualAmount;

        if (asOf.HasValue)
        {
            if (confirmedAmount.HasValue)
            {
                confirmed = IsLayerVisibleAt(confirmedAt, asOf.Value) ? confirmedAmount : null;
            }

            if (actualAmount.HasValue)
            {
                actual = IsLayerVisibleAt(actualizedAt, asOf.Value) ? actualAmount : null;
            }
        }

        return new ProjectedMaturity(expected, confirmed, actual, BestAvailable(actual, confirmed, expected));
    }

    private static bool IsLayerVisibleAt(DateTimeOffset? layerAt, DateOnly asOf)
    {
        // No timestamp → cannot prove absence; include and document residual limit.
        if (!layerAt.HasValue)
        {
            return true;
        }

        return DateOnly.FromDateTime(layerAt.Value.UtcDateTime) <= asOf;
    }

    private static decimal OutstandingAtAsOf(
        decimal recognizedAmount,
        decimal adjustmentAmount,
        IEnumerable<SettlementAllocSlice> allocations,
        DateOnly asOf)
    {
        var settled = allocations.Where(a => WasSettledAt(a, asOf)).Sum(a => a.Amount);
        return recognizedAmount + adjustmentAmount - settled;
    }

    private static bool WasSettledAt(SettlementAllocSlice a, DateOnly asOf)
    {
        if (!a.FinalizedAt.HasValue)
        {
            return false;
        }

        if (DateOnly.FromDateTime(a.FinalizedAt.Value.UtcDateTime) > asOf)
        {
            return false;
        }

        // Finalized and never reversed, or reversed after asOf → counted as settled at asOf.
        if (a.AllocationStatus == SettlementAllocationStatuses.Finalized)
        {
            return true;
        }

        if (a.AllocationStatus == SettlementAllocationStatuses.Reversed
            && a.ReversedAt.HasValue
            && DateOnly.FromDateTime(a.ReversedAt.Value.UtcDateTime) > asOf)
        {
            return true;
        }

        return false;
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

    internal sealed record ProjectedMaturity(
        decimal Expected,
        decimal? Confirmed,
        decimal? Actual,
        decimal BestAvailable);

    private sealed record SettlementAllocSlice(
        decimal Amount,
        string AllocationStatus,
        DateTimeOffset? FinalizedAt,
        DateTimeOffset? ReversedAt);
}
