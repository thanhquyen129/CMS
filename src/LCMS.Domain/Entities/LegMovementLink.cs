using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: leg_movement_links (D03) — N:N TransportLeg↔TransportMovement.</summary>
public sealed class LegMovementLink : TenantEntityBase
{
    public Guid TransportLegId { get; set; }
    public Guid TransportMovementId { get; set; }

    public TransportLeg? TransportLeg { get; set; }
    public TransportMovement? TransportMovement { get; set; }
}
