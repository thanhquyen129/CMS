using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: surcharges — independent additional charge, not a rate-card child.</summary>
public sealed class Surcharge : TenantEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>buy | sell | both</summary>
    public string Direction { get; set; } = SurchargeDirections.Buy;

    /// <summary>active | retired</summary>
    public string Status { get; set; } = SurchargeStatuses.Active;

    /// <summary>Draft pricing-rule component this row was copied from. Published history is not copied.</summary>
    public Guid? SourceLegacyComponentId { get; set; }
}

public sealed class SurchargeVersion : TenantEntityBase
{
    public Guid SurchargeId { get; set; }
    public int VersionNo { get; set; }

    /// <summary>draft | published | retired</summary>
    public string PublishStatus { get; set; } = SurchargeVersionStatuses.Draft;

    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Declared VAT percent. Null means undeclared, not 0%.</summary>
    public decimal? VatRate { get; set; }

    /// <summary>Snapshot of the economic charge. Null on legacy rows means unmapped, not a guess from the surcharge code.</summary>
    public Guid? EconomicChargeTypeId { get; set; }

    public Surcharge? Surcharge { get; set; }

    public bool IsPublished =>
        string.Equals(PublishStatus, SurchargeVersionStatuses.Published, StringComparison.OrdinalIgnoreCase);
}

public sealed class SurchargeRule : TenantEntityBase
{
    public Guid SurchargeVersionId { get; set; }
    public string CalculationMode { get; set; } = SurchargeCalcModes.FixedRate;
    public string? Basis { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public decimal RateAmountPercent { get; set; }
    public int Priority { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? ContainerType { get; set; }

    public SurchargeVersion? SurchargeVersion { get; set; }
}

public sealed class SurchargeCondition : TenantEntityBase
{
    public Guid SurchargeRuleId { get; set; }
    public string Dimension { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq";
    public string? ValueText { get; set; }
    public decimal? ValueFrom { get; set; }
    public decimal? ValueTo { get; set; }

    public SurchargeRule? SurchargeRule { get; set; }
}

public sealed class SurchargeScope : TenantEntityBase
{
    public Guid SurchargeRuleId { get; set; }
    public Guid? RateCardId { get; set; }
    public Guid? RateVersionId { get; set; }
    public Guid? VendorPartyId { get; set; }
    public Guid? CustomerPartyId { get; set; }

    /// <summary>Optional SELL group scope. Empty does not filter.</summary>
    public string? CustomerGroupCode { get; set; }

    public string? ServiceTypeCode { get; set; }
    public string? RouteCode { get; set; }
    public string? TransportMode { get; set; }

    public SurchargeRule? SurchargeRule { get; set; }
}

public sealed class SurchargeBreak : TenantEntityBase
{
    public Guid SurchargeRuleId { get; set; }
    public int SequenceNo { get; set; }
    public decimal MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public decimal UnitAmount { get; set; }

    public SurchargeRule? SurchargeRule { get; set; }
}

/// <summary>Idempotent classification log. A second run must not insert another surcharge.</summary>
public sealed class SurchargeMigrationLog : TenantEntityBase
{
    public Guid PricingRuleComponentId { get; set; }
    public string Classification { get; set; } = LegacyComponentClasses.Unknown;
    public string Outcome { get; set; } = SurchargeMigrationOutcomes.SkippedUnknown;
    public Guid? SurchargeId { get; set; }
}

public static class SurchargeDirections
{
    public const string Buy = "buy";
    public const string Sell = "sell";
    public const string Both = "both";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase) { Buy, Sell, Both };
}

public static class SurchargeStatuses
{
    public const string Active = "active";
    public const string Retired = "retired";
}

public static class SurchargeVersionStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Retired = "retired";
}

public static class SurchargeCalcModes
{
    public const string UnitRate = "unit_rate";
    public const string FixedRate = "fixed_rate";
    public const string ContainerRate = "container_rate";
    public const string WeightBreak = "weight_break";
    public const string Percentage = "percentage";
    public const string Composite = "composite";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        UnitRate, FixedRate, ContainerRate, WeightBreak, Percentage, Composite
    };
}

public static class RatingSourceTypes
{
    public const string BaseRate = "base_rate";
    public const string Surcharge = "surcharge";
}

public static class LegacyComponentClasses
{
    public const string Base = "base";
    public const string Surcharge = "surcharge";
    public const string Unknown = "unknown";
}

public static class SurchargeMigrationOutcomes
{
    public const string Migrated = "migrated";
    public const string SkippedBase = "skipped_base";
    public const string SkippedUnknown = "skipped_unknown";
    public const string SkippedPublished = "skipped_published";
    public const string AlreadyMapped = "already_mapped";
}
