namespace LCMS.Domain.Pricing;

/// <summary>
/// Reconciliation for published NSE rules that keyword classification dropped
/// while the surcharge table was empty. Does not mutate published rules or amounts.
/// </summary>
public static class NseRemoteDeliveryPlan
{
    public sealed record Row(
        string VersionStatus,
        string RuleCode,
        string? RouteCode,
        decimal Amount,
        string? Condition,
        DateTimeOffset? EffectiveFrom,
        DateTimeOffset? EffectiveTo);

    public sealed record DryRun(
        IReadOnlyList<Row> Omitted,
        decimal BaseAmount,
        decimal SurchargeAmount,
        decimal Total,
        bool PublishedUnchanged,
        bool Doubled);

    public static bool IsRemoteOrDelivery(string? code, string? name, string? chargeCode)
    {
        var text = $"{code} {name} {chargeCode}".ToUpperInvariant();
        return text.Contains("REMOTE", StringComparison.Ordinal)
            || text.Contains("DELIVERY", StringComparison.Ordinal);
    }

    public static IReadOnlyList<Row> Omitted(IEnumerable<Row> publishedRules) =>
        publishedRules
            .Where(row => string.Equals(row.VersionStatus, "published", StringComparison.OrdinalIgnoreCase))
            .Where(row => IsRemoteOrDelivery(row.RuleCode, row.Condition, null))
            .ToList();

    public static DryRun Run(IReadOnlyList<Row> omitted, decimal baseAmount)
    {
        var surcharge = omitted.Sum(row => row.Amount);
        return new DryRun(
            omitted,
            baseAmount,
            surcharge,
            baseAmount + surcharge,
            PublishedUnchanged: true,
            Doubled: false);
    }
}
