using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: party_roles (D02) — Business Party capability roles (customer/vendor/payer/payee).</summary>
public sealed class PartyRole : TenantEntityBase
{
    public Guid PartyId { get; set; }

    /// <summary>Canonical role code: customer | vendor | payer | payee (extensible).</summary>
    public string RoleCode { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public static class PartyRoleCodes
{
    public const string Customer = "customer";
    public const string Vendor = "vendor";
    public const string Payer = "payer";
    public const string Payee = "payee";
    public const string BillTo = "bill_to";
    public const string Shipper = "shipper";
    public const string Consignee = "consignee";
    public const string Carrier = "carrier";
    public const string Agent = "agent";

    public static readonly IReadOnlyList<string> Catalog =
    [
        Customer,
        Vendor,
        Payer,
        Payee,
        BillTo,
        Shipper,
        Consignee,
        Carrier,
        Agent
    ];

    public static bool IsKnown(string? code) =>
        !string.IsNullOrWhiteSpace(code)
        && Catalog.Contains(code.Trim().ToLowerInvariant());

    public static readonly IReadOnlyList<string> CustomerSide =
    [
        Customer,
        Payer,
        BillTo,
        Shipper,
        Consignee
    ];

    public static readonly IReadOnlyList<string> VendorSide =
    [
        Vendor,
        Payee,
        Carrier,
        Agent
    ];
}
