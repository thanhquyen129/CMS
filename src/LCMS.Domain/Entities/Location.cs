using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: locations (D02) — canonical place used by rating, import, and search.
/// Free-text is not a business key once any location exists for the tenant.
/// </summary>
public sealed class Location : TenantEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>airport | port | city | depot | border | other</summary>
    public string LocationType { get; set; } = LocationTypes.Other;

    public string? CountryCode { get; set; }
    public string? Subdivision { get; set; }
    public string? City { get; set; }
    public string? IataCode { get; set; }
    public string? Unlocode { get; set; }
    public string? TerminalCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class LocationTypes
{
    public const string Airport = "airport";
    public const string Port = "port";
    public const string City = "city";
    public const string Depot = "depot";
    public const string Border = "border";
    public const string Other = "other";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Airport, Port, City, Depot, Border, Other
    };

    public static bool IsKnown(string? code) =>
        !string.IsNullOrWhiteSpace(code) && All.Contains(code.Trim());
}
