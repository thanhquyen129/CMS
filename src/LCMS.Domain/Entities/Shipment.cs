using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: shipments (D03) — optional operational depth under Bill (N:N via bill_shipment_links).
/// </summary>
public sealed class Shipment : TenantEntityBase
{
    public string ShipmentNo { get; set; } = string.Empty;

    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalVersion { get; set; }

    public string OperationalStatus { get; set; } = "active";
    public bool IsActive { get; set; } = true;

    public Guid? AssignedUserId { get; set; }

    /// <summary>air / sea / road / rail — rating/allocation context, not dispatch.</summary>
    public string? TransportMode { get; set; }

    public string? OriginCode { get; set; }
    public string? DestinationCode { get; set; }
    public string? RouteCode { get; set; }

    public DateTimeOffset? EtdAt { get; set; }
    public DateTimeOffset? EtaAt { get; set; }

    public string? CustomerReference { get; set; }
    public string? Description { get; set; }

    /// <summary>Remaining create-form fields (cargo totals, rating notes) as JSON.</summary>
    public string? ContextJson { get; set; }

    public Tenant? Tenant { get; set; }
}
