using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: transport_movements (D03) — chuyến vận chuyển (actual movement).
/// Ops SoT identity via source_system + external_id (C-002).
/// </summary>
public sealed class TransportMovement : TenantEntityBase
{
    public string MovementNo { get; set; } = string.Empty;

    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalVersion { get; set; }

    public string OperationalStatus { get; set; } = "active";
    public bool IsActive { get; set; } = true;

    public DateTimeOffset? MovementOn { get; set; }
    public Guid? CarrierPartyId { get; set; }
    public Guid? OriginLocationId { get; set; }
    public Guid? DestinationLocationId { get; set; }
    public string? TransportMode { get; set; }

    public Tenant? Tenant { get; set; }
}
