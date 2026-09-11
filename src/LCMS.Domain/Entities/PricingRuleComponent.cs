using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: pricing_rule_components (D04) — Map Cost Type / Revenue Type.</summary>
public sealed class PricingRuleComponent : TenantEntityBase
{
    public Guid PricingRuleId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>cost | revenue</summary>
    public string FinancialNature { get; set; } = "cost";

    public string? CostTypeCode { get; set; }
    public string? RevenueTypeCode { get; set; }

    /// <summary>Component amount (or unit rate share) used when rating.</summary>
    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "VND";
    public int SortOrder { get; set; }

    public PricingRule? PricingRule { get; set; }
}
