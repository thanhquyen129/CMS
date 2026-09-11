using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: bill_shipment_links (D03) — N:N Bill↔Shipment (TD1-DB-002).</summary>
public sealed class BillShipmentLink : TenantEntityBase
{
    public Guid BillId { get; set; }
    public Guid ShipmentId { get; set; }

    public Bill? Bill { get; set; }
    public Shipment? Shipment { get; set; }
}
