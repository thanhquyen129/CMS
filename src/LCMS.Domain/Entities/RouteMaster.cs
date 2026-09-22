using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: routes (D02) — reusable origin–destination for rate applicability.
/// A transaction may still store origin and destination without picking a route.
/// </summary>
public sealed class RouteMaster : TenantEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid OriginLocationId { get; set; }
    public Guid DestinationLocationId { get; set; }
    public string? TransportModeCode { get; set; }
    public string? ServiceTypeCode { get; set; }
    public bool IsActive { get; set; } = true;

    public Location? OriginLocation { get; set; }
    public Location? DestinationLocation { get; set; }
}

/// <summary>Table: route_stops — intermediate locations between origin and destination.</summary>
public sealed class RouteStop : TenantEntityBase
{
    public Guid RouteId { get; set; }
    public int SequenceNo { get; set; }
    public Guid LocationId { get; set; }

    public RouteMaster? Route { get; set; }
    public Location? Location { get; set; }
}
