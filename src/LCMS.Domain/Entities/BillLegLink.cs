using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: bill_leg_links (D03) — N:N Bill↔TransportLeg.</summary>
public sealed class BillLegLink : TenantEntityBase
{
    public Guid BillId { get; set; }
    public Guid TransportLegId { get; set; }

    public Bill? Bill { get; set; }
    public TransportLeg? TransportLeg { get; set; }
}
