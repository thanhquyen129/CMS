using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: order_bill_links (D03) — N:N Order↔Bill (TD1-DB-002).</summary>
public sealed class OrderBillLink : TenantEntityBase
{
    public Guid OrderId { get; set; }
    public Guid BillId { get; set; }

    public Order? Order { get; set; }
    public Bill? Bill { get; set; }
}
