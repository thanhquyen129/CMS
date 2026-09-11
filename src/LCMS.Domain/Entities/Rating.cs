using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: ratings (D04) — Một lần tính giá (context + version snapshot).</summary>
public sealed class Rating : TenantEntityBase
{
    public Guid BillId { get; set; }
    public Guid RateVersionId { get; set; }
    public DateTimeOffset RatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CurrencyCode { get; set; } = "VND";
    public decimal TotalAmount { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public string Status { get; set; } = "completed";

    public Bill? Bill { get; set; }
    public RateVersion? RateVersion { get; set; }
}
