using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: economic_charge_types — pairs buy and sell lines of one economic charge.</summary>
public sealed class EconomicChargeType : TenantEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Table: charge_type_mappings — cost/revenue/component code to an economic charge type.
/// </summary>
public sealed class ChargeTypeMapping : TenantEntityBase
{
    /// <summary>cost_type | revenue_type | component</summary>
    public string SourceKind { get; set; } = ChargeTypeMappingKinds.Component;

    public string SourceCode { get; set; } = string.Empty;
    public Guid EconomicChargeTypeId { get; set; }

    public EconomicChargeType? EconomicChargeType { get; set; }
}

public static class ChargeTypeMappingKinds
{
    public const string CostType = "cost_type";
    public const string RevenueType = "revenue_type";
    public const string Component = "component";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        CostType, RevenueType, Component
    };
}
