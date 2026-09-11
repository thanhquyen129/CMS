using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: costs (D05) — Single Economic Cost.
/// Maturity layers keep separate amounts (C-009); allocation does not create a new Cost (C-003).
/// </summary>
public sealed class Cost : TenantEntityBase
{
    /// <summary>Nullable when attribution_type = shared.</summary>
    public Guid? BillId { get; set; }

    public Guid? CostTypeId { get; set; }
    public string? CostTypeCode { get; set; }
    public Guid? CostCategoryId { get; set; }
    public Guid? VendorPartyId { get; set; }

    /// <summary>expected | confirmed | actual — current peak maturity.</summary>
    public string FinancialMaturity { get; set; } = CostMaturities.Expected;

    /// <summary>direct | shared</summary>
    public string AttributionType { get; set; } = CostAttributionTypes.Direct;

    /// <summary>Expected layer amount — never overwritten by confirm/actualize.</summary>
    public decimal ExpectedAmount { get; set; }

    /// <summary>Confirmed layer amount — set on confirm; preserved on actualize.</summary>
    public decimal? ConfirmedAmount { get; set; }

    /// <summary>Actual layer amount — set on actualize.</summary>
    public decimal? ActualAmount { get; set; }

    /// <summary>TD1 amount: mirrors the current maturity layer.</summary>
    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "VND";
    public decimal? BaseAmount { get; set; }
    public Guid? FxRateId { get; set; }

    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }

    public string RecordStatus { get; set; } = "active";

    /// <summary>Approval stub (full workflow deferred).</summary>
    public string ApprovalStatus { get; set; } = "not_required";

    public DateOnly EffectiveDate { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ActualizedAt { get; set; }
    public Guid? ActualizedBy { get; set; }

    /// <summary>Owning organization for Data Scope = organization (usually from Bill).</summary>
    public Guid? OrganizationId { get; set; }

    public Bill? Bill { get; set; }
}

public static class CostMaturities
{
    public const string Expected = "expected";
    public const string Confirmed = "confirmed";
    public const string Actual = "actual";
}

public static class CostAttributionTypes
{
    public const string Direct = "direct";
    public const string Shared = "shared";
}

public static class CostSourceTypes
{
    public const string RatingDetail = "rating_detail";
    public const string Manual = "manual";
}
