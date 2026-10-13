namespace LCMS.Domain.Entities;

/// <summary>
/// Shipment classification versus the physical mode of a leg (SHIPMENT-MODE-01).
/// MULTIMODAL describes the shipment only. SEA and OCEAN share one sea family.
/// WATER is inland water and stays its own family. Stored leg codes are not rewritten.
/// </summary>
public static class ShipmentModePolicy
{
    public const string Multimodal = "multimodal";
    public const string SeaFamily = "sea";

    public static string? Normalize(string? mode) =>
        string.IsNullOrWhiteSpace(mode) ? null : mode.Trim().ToLowerInvariant();

    public static bool IsMultimodal(string? mode) => Normalize(mode) == Multimodal;

    public static bool IsSeaFamily(string? mode) =>
        Normalize(mode) is "sea" or "ocean";

    /// <summary>Null for blank or MULTIMODAL. OCEAN collapses to <see cref="SeaFamily"/>. WATER stays water.</summary>
    public static string? PhysicalFamily(string? mode)
    {
        var normalized = Normalize(mode);
        if (normalized is null || normalized == Multimodal)
        {
            return null;
        }

        return IsSeaFamily(normalized) ? SeaFamily : normalized;
    }

    public static bool IsPhysical(string? mode) => PhysicalFamily(mode) is not null;

    /// <summary>Actual classification from leg modes. Null when no leg has a physical mode.</summary>
    public static string? Derive(IEnumerable<string?> legModes)
    {
        var families = new List<string>();
        foreach (var mode in legModes)
        {
            var family = PhysicalFamily(mode);
            if (family is null || families.Contains(family))
            {
                continue;
            }

            families.Add(family);
        }

        return families.Count switch
        {
            0 => null,
            1 => families[0],
            _ => Multimodal
        };
    }

    /// <summary>True when a stored source/planned mode disagrees with the derived classification.</summary>
    public static bool IsMismatch(string? planned, string? derived)
    {
        if (derived is null)
        {
            return false;
        }

        var plannedKey = IsMultimodal(planned) ? Multimodal : PhysicalFamily(planned);
        return plannedKey is not null
            && !string.Equals(plannedKey, derived, StringComparison.Ordinal);
    }

    public static (string? Derived, bool Mismatch) Classify(string? planned, IEnumerable<string?> legModes)
    {
        var derived = Derive(legModes);
        return (derived, IsMismatch(planned, derived));
    }

    /// <summary>
    /// Leg rating uses a physical mode. MULTIMODAL on the shipment or the request is ignored.
    /// A single-mode shipment still fills a leg that has no mode of its own.
    /// </summary>
    public static string? LegRatingMode(string? legMode, string? shipmentMode, string? requestedMode)
    {
        if (IsPhysical(requestedMode))
        {
            return Normalize(requestedMode);
        }

        if (IsPhysical(legMode))
        {
            return Normalize(legMode);
        }

        if (IsPhysical(shipmentMode))
        {
            return Normalize(shipmentMode);
        }

        return null;
    }
}
