using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: rate_cards (D04) — Bảng giá (Customer/Vendor type).</summary>
public sealed class RateCard : TenantEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>customer | vendor</summary>
    public string PartyType { get; set; } = "vendor";

    public string CurrencyCode { get; set; } = "VND";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
