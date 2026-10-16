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

    /// <summary>Canonical directed route. Null with <see cref="AppliesAnyRoute"/> true means every route.</summary>
    public Guid? RouteId { get; set; }

    /// <summary>
    /// True = explicit every-route. False = a route is required. Null = legacy row that still matches any route.
    /// </summary>
    public bool? AppliesAnyRoute { get; set; }

    /// <summary>Display code of <see cref="RouteId"/>. Not a free-text route.</summary>
    public string? RouteCode { get; set; }

    /// <summary>Operating carrier. Not the vendor on the rate-card header.</summary>
    public Guid? CarrierPartyId { get; set; }

    public string? CarrierName { get; set; }

    /// <summary>gross | chargeable. Null does not filter by weight source.</summary>
    public string? WeightBasis { get; set; }

    /// <summary>Inclusive lower bound of [min, max). Null with <see cref="WeightMax"/> null means no weight tier.</summary>
    public decimal? WeightMin { get; set; }

    /// <summary>Exclusive upper bound. Null means no upper bound.</summary>
    public decimal? WeightMax { get; set; }

    /// <summary>Groups advanced matrix columns that share a route and carrier.</summary>
    public string? MatrixGroupCode { get; set; }

    /// <summary>Lower bound for <see cref="PricingCalcMethods.MinMaxClamp"/> (and optional post-clamp).</summary>
    public decimal? MinAmount { get; set; }

    /// <summary>Upper bound for <see cref="PricingCalcMethods.MinMaxClamp"/>.</summary>
    public decimal? MaxAmount { get; set; }

    /// <summary>Rules that share a charge code compete. Null keeps legacy “apply every match”.</summary>
    public string? ChargeCode { get; set; }

    /// <summary>base_rate | independent. Null is a legacy row. Surcharges are not stored here.</summary>
    public string? LineRole { get; set; }

    public string? TransportMode { get; set; }
    public string? OriginCode { get; set; }
    public string? DestinationCode { get; set; }
    public string? CommodityCode { get; set; }

    /// <summary>kg per CBM for air volumetric weight. Null uses 167.</summary>
    public decimal? VolumetricFactor { get; set; }

    /// <summary>Round chargeable quantity up to this step. Null keeps the raw value.</summary>
    public decimal? RoundingStep { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public RateVersion? RateVersion { get; set; }
}

public static class RateLineRoles
{
    public const string BaseRate = "base_rate";
    public const string Independent = "independent";
    public const string Surcharge = "surcharge";

    public static bool IsBaseFamily(string? role) =>
        string.Equals(role, BaseRate, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, Independent, StringComparison.OrdinalIgnoreCase);

    public static bool IsSurcharge(string? role) =>
        string.Equals(role, Surcharge, StringComparison.OrdinalIgnoreCase);
}

public static class PricingCalcMethods
{
    public const string Fixed = "fixed";
    public const string UnitRate = "unit_rate";
    public const string PercentOfBase = "percent_of_base";
    public const string MinMaxClamp = "min_max_clamp";
    public const string WeightBreakPivot = "weight_break_pivot";
    public const string WeightStep = "weight_step";
    public const string ContainerRate = "container_rate";
    public const string Composite = "composite";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Fixed,
        UnitRate,
        PercentOfBase,
        MinMaxClamp,
        WeightBreakPivot,
        WeightStep,
        ContainerRate,
        Composite
    };
}
