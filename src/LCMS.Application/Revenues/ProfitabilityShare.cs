namespace LCMS.Application.Revenues;

public static class ProfitabilityShare
{
    public static decimal Layer(string view, decimal? actual, decimal? confirmed, decimal expected)
    {
        return view switch
        {
            "expected" => expected,
            "confirmed" => confirmed ?? 0m,
            "actual" => actual ?? 0m,
            _ => actual ?? confirmed ?? expected
        };
    }

    /// <summary>
    /// A finalized mapping replaces the parent amount. Only the locked maturity (and best) receives the share.
    /// </summary>
    public static decimal Amount(
        string view,
        decimal? actual,
        decimal? confirmed,
        decimal expected,
        bool hasMapping,
        string? mappedMaturity,
        decimal mappedAmount)
    {
        if (!hasMapping)
        {
            return Layer(view, actual, confirmed, expected);
        }

        var maturity = mappedMaturity?.Trim().ToLowerInvariant();
        if (view == "best" || view == maturity)
        {
            return mappedAmount;
        }

        return 0m;
    }

    public static decimal? MarginPercent(decimal revenue, decimal profit)
    {
        if (revenue == 0m)
        {
            return null;
        }

        return decimal.Round(profit / revenue * 100m, 2, MidpointRounding.AwayFromZero);
    }
}
