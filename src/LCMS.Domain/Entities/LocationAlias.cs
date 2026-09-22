using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: location_aliases — external or legacy codes that resolve to one canonical location.
/// </summary>
public sealed class LocationAlias : TenantEntityBase
{
    public Guid LocationId { get; set; }
    public string AliasCode { get; set; } = string.Empty;
    public string? SourceSystem { get; set; }

    public Location? Location { get; set; }
}
