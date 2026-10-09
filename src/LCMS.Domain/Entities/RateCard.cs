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
    public string? TransportMode { get; set; }
    public string? RouteCode { get; set; }
    public string? CarrierName { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>BUY price source. Empty means a general buy card.</summary>
    public Guid? SupplierPartyId { get; set; }

    /// <summary>SELL price source. Mutually exclusive with <see cref="CustomerGroupCode"/>.</summary>
    public Guid? CustomerPartyId { get; set; }

    /// <summary>SELL group, matched to business_parties.group_code. Not a second party identity.</summary>
    public string? CustomerGroupCode { get; set; }
}
