using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: master_catalog_items (D02) — tenant taxonomy for types used on Cost/Revenue/Rate/Document.
/// </summary>
public sealed class MasterCatalogItem : TenantEntityBase
{
    /// <summary>cost_type | revenue_type | service_type | pricing_component | document_type | payment_term</summary>
    public string Kind { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
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

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CostType, RevenueType, ServiceType, PricingComponent, DocumentType, PaymentTerm
    };
}

/// <summary>Source-system value for Manual Reference Entry when LCMS is SoT (SCP-003).</summary>
public static class OperationalSourceSystems
{
    public const string LcmsManual = "lcms_manual";
}
