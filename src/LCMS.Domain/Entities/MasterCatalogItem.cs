using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: master_catalog_items (D02) — tenant taxonomy for types used on Cost/Revenue/Rate/Document.
/// </summary>
public sealed class MasterCatalogItem : TenantEntityBase
{
    /// <summary>See <see cref="MasterCatalogKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>Optional JSON extras (route origin/dest, location class).</summary>
    public string? AttributesJson { get; set; }
}

/// <summary>Stable kind codes for <see cref="MasterCatalogItem"/>.</summary>
public static class MasterCatalogKinds
{
    public const string CostType = "cost_type";
    public const string RevenueType = "revenue_type";
    public const string ServiceType = "service_type";
    public const string PricingComponent = "pricing_component";
    public const string DocumentType = "document_type";
    public const string PaymentTerm = "payment_term";
    public const string TransportRoute = "transport_route";
    public const string TransportMode = "transport_mode";
    public const string Location = "location";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CostType, RevenueType, ServiceType, PricingComponent, DocumentType, PaymentTerm,
        TransportRoute, TransportMode, Location
    };

    public static readonly IReadOnlySet<string> LocationClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "port", "airport", "border"
    };
}

/// <summary>Source-system value for Manual Reference Entry when LCMS is SoT (SCP-003).</summary>
public static class OperationalSourceSystems
{
    public const string LcmsManual = "lcms_manual";
}
