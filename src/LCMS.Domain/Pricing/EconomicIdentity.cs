namespace LCMS.Domain.Pricing;

/// <summary>
/// Stable economic-obligation key (BR-DF-004/005). Not a naïve charge_code+amount group.
/// </summary>
public static class EconomicIdentity
{
    public const string DirectionBuy = "BUY";
    public const string DirectionSell = "SELL";

    public static string Build(
        Guid tenantId,
        Guid? billId,
        string direction,
        string chargeCode,
        Guid? transportLegId,
        Guid? shipmentId,
        Guid? counterpartyId,
        string? serviceOccurrenceKey = null)
    {
        var dir = NormalizeDirection(direction);
        var charge = NormalizeToken(chargeCode, "UNKNOWN");
        var occurrence = !string.IsNullOrWhiteSpace(serviceOccurrenceKey)
            ? NormalizeToken(serviceOccurrenceKey, "occ")
            : transportLegId is Guid leg
                ? $"leg:{leg:N}"
                : shipmentId is Guid ship
                    ? $"shp:{ship:N}"
                    : billId is Guid bill
                        ? $"bill:{bill:N}"
                        : "scope:none";

        var counterparty = counterpartyId is Guid cp ? cp.ToString("N") : "cp:none";
        var anchor = billId is Guid b ? b.ToString("N") : "bill:none";

        return string.Join('|',
            tenantId.ToString("N"),
            anchor,
            dir,
            charge,
            occurrence,
            counterparty);
    }

    public static string NormalizeDirection(string? direction)
    {
        if (string.Equals(direction, DirectionSell, StringComparison.OrdinalIgnoreCase)
            || string.Equals(direction, "customer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(direction, "sell", StringComparison.OrdinalIgnoreCase))
        {
            return DirectionSell;
        }

        return DirectionBuy;
    }

    public static bool IsAmbiguous(
        string chargeCode,
        Guid? transportLegId,
        Guid? shipmentId,
        Guid? counterpartyId,
        string? serviceOccurrenceKey)
    {
        // Same charge without leg/shipment/occurrence/counterparty cannot assert uniqueness
        // when multiple candidates may exist — callers raise AMBIGUOUS_ECONOMIC_IDENTITY.
        return string.IsNullOrWhiteSpace(chargeCode)
               && transportLegId is null
               && shipmentId is null
               && counterpartyId is null
               && string.IsNullOrWhiteSpace(serviceOccurrenceKey);
    }

    private static string NormalizeToken(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToUpperInvariant();
    }
}

public static class EconomicRecordStatuses
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Superseded = "superseded";

    public static bool IsActive(string? status) =>
        string.Equals(status, Active, StringComparison.OrdinalIgnoreCase);

    public static bool IsExcludedFromTotals(string? status) =>
        !IsActive(status);
}

public static class EffectiveExclusionReasons
{
    public const string SupersededByNewerExpected = "SUPERSEDED_BY_NEWER_EXPECTED";
    public const string ShadowedByActual = "SHADOWED_BY_ACTUAL";
    public const string ShadowedByConfirmed = "SHADOWED_BY_CONFIRMED";
    public const string ComparisonOnlyUnapplied = "COMPARISON_ONLY_UNAPPLIED";
    public const string AmbiguousPendingResolution = "AMBIGUOUS_PENDING_RESOLUTION";
    public const string InactiveRecord = "INACTIVE_RECORD";
}

public static class RatingApplicationBuckets
{
    public const string Applied = "applied";
    public const string EligibleNotActivated = "eligible_not_activated";
    public const string Excluded = "excluded";

    /// <summary>Legacy base lines (null bucket) and explicit applied may seed. Others must not.</summary>
    public static bool MaySeedToFinancials(string? bucket) =>
        string.IsNullOrWhiteSpace(bucket)
        || string.Equals(bucket, Applied, StringComparison.OrdinalIgnoreCase);
}
