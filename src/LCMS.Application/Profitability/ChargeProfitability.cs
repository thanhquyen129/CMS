namespace LCMS.Application.Profitability;

public sealed record ChargeProfitSlice(
    Guid? EconomicChargeTypeId,
    string ChargeCode,
    string ChargeName,
    string Side,
    string PeakMaturity,
    decimal? ExpectedReporting,
    decimal? ConfirmedReporting,
    decimal? ActualReporting,
    Guid SourceId,
    string SourceKind,
    Guid? PartnerId,
    Guid? BillId,
    bool ExpectedFxMissing = false,
    bool ConfirmedFxMissing = false,
    bool ActualFxMissing = false,
    decimal AttributionFactor = 1m);

public sealed record ChargeProfitSourceDto(
    Guid SourceId,
    string SourceKind,
    string Side,
    Guid? BillId,
    Guid? PartnerId,
    string PeakMaturity,
    decimal AttributionFactor = 1m);

public sealed record ChargeProfitRowDto(
    Guid? EconomicChargeTypeId,
    string ChargeCode,
    string ChargeName,
    decimal? CostReporting,
    decimal? RevenueReporting,
    decimal? ProfitReporting,
    decimal? MarginPercent,
    string ComparisonKind,
    string DataCompleteness,
    bool NegativeFlag,
    string Note,
    IReadOnlyList<ChargeProfitSourceDto> Sources);

public sealed record ChargeProfitabilityDto(
    string View,
    string? ReportingCurrency,
    IReadOnlyList<ChargeProfitRowDto> Rows);

public static class ChargeProfitability
{
    public const string Complete = "complete";
    public const string Incomplete = "incomplete";
    public const string SameBasis = "same_basis";
    public const string Mixed = "mixed";

    public readonly record struct AllocationLayers(
        decimal? Expected,
        decimal? Confirmed,
        decimal? Actual,
        bool ExpectedFx,
        bool ConfirmedFx,
        bool ActualFx);

    /// <summary>
    /// A shared-cost share belongs to the parent cost's peak maturity only.
    /// A missing FX conversion stays incomplete; it is not copied onto other layers.
    /// </summary>
    public static AllocationLayers LayersForAllocation(string? peak, decimal? reportingShare)
    {
        var maturity = string.IsNullOrWhiteSpace(peak) ? "expected" : peak.Trim().ToLowerInvariant();
        if (maturity is not ("confirmed" or "actual"))
        {
            maturity = "expected";
        }

        var missing = reportingShare is null;
        return new AllocationLayers(
            maturity == "expected" && !missing ? reportingShare : null,
            maturity == "confirmed" && !missing ? reportingShare : null,
            maturity == "actual" && !missing ? reportingShare : null,
            maturity == "expected" && missing,
            maturity == "confirmed" && missing,
            maturity == "actual" && missing);
    }

    /// <summary>One bill linked to several orders contributes an equal share to each order. The bill view stays whole.</summary>
    public static decimal OrderShare(decimal amount, int linkedOrders) =>
        linkedOrders <= 1
            ? amount
            : decimal.Round(amount / linkedOrders, 4, MidpointRounding.AwayFromZero);

    public static ChargeProfitabilityDto Compose(string view, string? reportingCurrency, IReadOnlyList<ChargeProfitSlice> lines)
    {
        var normalized = view.Trim().ToLowerInvariant();
        var paired = new Dictionary<Guid, List<ChargeProfitSlice>>();
        var unmapped = new List<ChargeProfitSlice>();
        foreach (var line in lines)
        {
            if (line.EconomicChargeTypeId is Guid id)
            {
                if (!paired.TryGetValue(id, out var bucket))
                {
                    bucket = [];
                    paired[id] = bucket;
                }

                bucket.Add(line);
            }
            else
            {
                unmapped.Add(line);
            }
        }

        var rows = new List<ChargeProfitRowDto>();
        foreach (var pair in paired.OrderBy(p => p.Value[0].ChargeCode, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(Build(pair.Key, pair.Value[0].ChargeCode, pair.Value[0].ChargeName, pair.Value, normalized));
        }

        foreach (var line in unmapped.OrderBy(l => l.ChargeCode, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(Build(null, line.ChargeCode, line.ChargeName, [line], normalized));
        }

        return new ChargeProfitabilityDto(normalized, reportingCurrency, rows);
    }

    private static ChargeProfitRowDto Build(
        Guid? chargeTypeId,
        string code,
        string name,
        IReadOnlyList<ChargeProfitSlice> lines,
        string view)
    {
        var costs = lines.Where(l => l.Side == "cost").ToList();
        var revenues = lines.Where(l => l.Side == "revenue").ToList();
        var cost = Sum(costs, view);
        var revenue = Sum(revenues, view);
        var sources = lines.Select(l => new ChargeProfitSourceDto(
            l.SourceId, l.SourceKind, l.Side, l.BillId, l.PartnerId, l.PeakMaturity, l.AttributionFactor)).ToList();

        if (chargeTypeId is null || costs.Count == 0 || revenues.Count == 0 || cost is null || revenue is null)
        {
            return new ChargeProfitRowDto(
                chargeTypeId,
                code,
                name,
                cost,
                revenue,
                null,
                null,
                SameBasis,
                Incomplete,
                false,
                "Chưa đủ dữ liệu",
                sources);
        }

        var profit = decimal.Round(revenue.Value - cost.Value, 4, MidpointRounding.AwayFromZero);
        var mixed = view == "best" && Maturity(costs, view) != Maturity(revenues, view);
        if (mixed)
        {
            return new ChargeProfitRowDto(
                chargeTypeId,
                code,
                name,
                cost,
                revenue,
                profit,
                Margin(revenue.Value, profit),
                Mixed,
                Complete,
                false,
                "So sánh hỗn hợp — không kết luận lỗ thực tế",
                sources);
        }

        var negative = profit < 0m;
        return new ChargeProfitRowDto(
            chargeTypeId,
            code,
            name,
            cost,
            revenue,
            profit,
            Margin(revenue.Value, profit),
            SameBasis,
            Complete,
            negative,
            negative ? "Giá bán thấp hơn giá mua trên cùng cơ sở so sánh" : "Đủ dữ liệu",
            sources);
    }

    private readonly record struct LayerPick(decimal? Amount, bool FxMissing, bool Absent);

    private static decimal? Sum(IReadOnlyList<ChargeProfitSlice> lines, string view)
    {
        if (lines.Count == 0)
        {
            return null;
        }

        var any = false;
        decimal total = 0m;
        foreach (var line in lines)
        {
            var pick = Pick(line, view);
            if (pick.FxMissing)
            {
                return null;
            }

            if (pick.Absent)
            {
                continue;
            }

            any = true;
            total += pick.Amount!.Value;
        }

        return any ? decimal.Round(total, 4, MidpointRounding.AwayFromZero) : null;
    }

    private static LayerPick Pick(ChargeProfitSlice line, string view)
    {
        if (view == "expected")
        {
            return Of(line.ExpectedReporting, line.ExpectedFxMissing);
        }

        if (view == "confirmed")
        {
            return Of(line.ConfirmedReporting, line.ConfirmedFxMissing);
        }

        if (view == "actual")
        {
            return Of(line.ActualReporting, line.ActualFxMissing);
        }

        if (line.ActualReporting is not null || line.ActualFxMissing)
        {
            return Of(line.ActualReporting, line.ActualFxMissing);
        }

        if (line.ConfirmedReporting is not null || line.ConfirmedFxMissing)
        {
            return Of(line.ConfirmedReporting, line.ConfirmedFxMissing);
        }

        return Of(line.ExpectedReporting, line.ExpectedFxMissing);
    }

    private static LayerPick Of(decimal? amount, bool fxMissing)
    {
        if (fxMissing)
        {
            return new LayerPick(null, true, false);
        }

        return amount is null
            ? new LayerPick(null, false, true)
            : new LayerPick(amount, false, false);
    }

    private static string Maturity(IReadOnlyList<ChargeProfitSlice> lines, string view)
    {
        if (view != "best")
        {
            return view;
        }

        var used = lines.Select(l =>
            l.ActualReporting is not null ? "actual"
            : l.ConfirmedReporting is not null ? "confirmed"
            : "expected").Distinct().OrderBy(x => x).ToArray();
        return string.Join("+", used);
    }

    private static decimal? Margin(decimal revenue, decimal profit) =>
        revenue == 0m ? null : decimal.Round(profit / revenue * 100m, 2, MidpointRounding.AwayFromZero);
}
