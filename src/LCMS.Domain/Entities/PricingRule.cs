using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: pricing_rules (D04) — Quy tắc tính giá trên một rate_version.</summary>
public sealed class PricingRule : TenantEntityBase
{
    public Guid RateVersionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>fixed | unit_rate | percent_of_base | min_max_clamp.</summary>
    public string CalcMethod { get; set; } = PricingCalcMethods.Fixed;

    /// <summary>Fixed amount, unit rate, or percent depending on <see cref="CalcMethod"/>.</summary>
    public decimal UnitAmount { get; set; }

    public string CurrencyCode { get; set; } = "VND";

    /// <summary>Free-form note (legacy). Prefer typed filter fields below.</summary>
    public string? Applicability { get; set; }

    /// <summary>Null = any service type (e.g. freight / customs).</summary>
    public string? ServiceTypeCode { get; set; }

    /// <summary>Null = any party type (customer | vendor).</summary>
    public string? PartyTypeCode { get; set; }

    /// <summary>Null = any route (stub code for Pass 2).</summary>
    public string? RouteCode { get; set; }

    /// <summary>Lower bound for <see cref="PricingCalcMethods.MinMaxClamp"/> (and optional post-clamp).</summary>
    public decimal? MinAmount { get; set; }

    /// <summary>Upper bound for <see cref="PricingCalcMethods.MinMaxClamp"/>.</summary>
    public decimal? MaxAmount { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public RateVersion? RateVersion { get; set; }
}

public static class PricingCalcMethods
{
    public const string Fixed = "fixed";
    public const string UnitRate = "unit_rate";
    public const string PercentOfBase = "percent_of_base";
    public const string MinMaxClamp = "min_max_clamp";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Fixed,
        UnitRate,
        PercentOfBase,
        MinMaxClamp
    };
}
