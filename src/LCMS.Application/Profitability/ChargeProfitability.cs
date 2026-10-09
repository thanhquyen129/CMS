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
    Guid? BillId);

public sealed record ChargeProfitSourceDto(
    Guid SourceId,
    string SourceKind,
    string Side,
    Guid? BillId,
    Guid? PartnerId,
    string PeakMaturity);

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
            l.SourceId, l.SourceKind, l.Side, l.BillId, l.PartnerId, l.PeakMaturity)).ToList();

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

    private static decimal? Sum(IReadOnlyList<ChargeProfitSlice> lines, string view)
    {
        if (lines.Count == 0)
        {
            return null;
        }

        decimal total = 0m;
        foreach (var line in lines)
        {
            var amount = Layer(line, view);
            if (amount is null)
            {
                return null;
            }

            total += amount.Value;
        }

        return decimal.Round(total, 4, MidpointRounding.AwayFromZero);
    }

    private static decimal? Layer(ChargeProfitSlice line, string view)
    {
        if (view == "expected")
        {
            return line.ExpectedReporting;
        }

        if (view == "confirmed")
        {
            return line.ConfirmedReporting;
        }

        if (view == "actual")
        {
            return line.ActualReporting;
        }

        return line.ActualReporting ?? line.ConfirmedReporting ?? line.ExpectedReporting;
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
