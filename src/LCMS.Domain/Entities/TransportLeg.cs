using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: transport_legs (D03) — chặng vận chuyển; Shipment 1:N Leg (TD1).
/// Ops SoT identity via source_system + external_id (C-002).
/// </summary>
public sealed class TransportLeg : TenantEntityBase
{
    public string LegNo { get; set; } = string.Empty;

    /// <summary>Owning shipment (CONFIRMED 1:N).</summary>
    public Guid ShipmentId { get; set; }

    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalVersion { get; set; }

    public string OperationalStatus { get; set; } = "active";
    public bool IsActive { get; set; } = true;

    public int SequenceNo { get; set; }
    public Guid? OriginLocationId { get; set; }
    public Guid? DestinationLocationId { get; set; }
    public string? OriginCode { get; set; }
    public string? DestinationCode { get; set; }

    public Tenant? Tenant { get; set; }
    public Shipment? Shipment { get; set; }
}
