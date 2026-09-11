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

    public Tenant? Tenant { get; set; }
}
