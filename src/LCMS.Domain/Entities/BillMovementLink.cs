using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: bill_movement_links (D03) — N:N Bill↔TransportMovement.</summary>
public sealed class BillMovementLink : TenantEntityBase
{
    public Guid BillId { get; set; }
    public Guid TransportMovementId { get; set; }

    public Bill? Bill { get; set; }
    public TransportMovement? TransportMovement { get; set; }
}
