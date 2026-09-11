using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: pricing_rules (D04) — Quy tắc tính giá trên một rate_version.</summary>
public sealed class PricingRule : TenantEntityBase
{
    public Guid RateVersionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>fixed | unit_rate — thin Expected seed math (no full formula engine).</summary>
    public string CalcMethod { get; set; } = PricingCalcMethods.Fixed;

    /// <summary>Fixed amount or unit rate depending on <see cref="CalcMethod"/>.</summary>
    public decimal UnitAmount { get; set; }

    public string CurrencyCode { get; set; } = "VND";
    public string? Applicability { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public RateVersion? RateVersion { get; set; }
}

public static class PricingCalcMethods
{
    public const string Fixed = "fixed";
    public const string UnitRate = "unit_rate";
}
