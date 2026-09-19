using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: revenues (D06) — Single Economic Revenue, Bill-attributable (C-004).
/// Maturity layers keep separate amounts (C-009); no shared revenue allocation in Core V1.
/// </summary>
public sealed class Revenue : TenantEntityBase
{
    /// <summary>Required — revenues are Bill-attributable only.</summary>
    public Guid BillId { get; set; }

    public Guid? RevenueTypeId { get; set; }
    public string? RevenueTypeCode { get; set; }
    public Guid? CustomerPartyId { get; set; }

    /// <summary>expected | confirmed | actual — current peak maturity.</summary>
    public string FinancialMaturity { get; set; } = RevenueMaturities.Expected;

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

    /// <summary>Optional recognition policy version string (stub).</summary>
    public string? RecognitionPolicyVersion { get; set; }

    public string RecordStatus { get; set; } = "active";

    /// <summary>Approval stub (full workflow deferred).</summary>
    public string ApprovalStatus { get; set; } = "not_required";

    public DateOnly EffectiveDate { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ActualizedAt { get; set; }
    public Guid? ActualizedBy { get; set; }

    public Bill? Bill { get; set; }
}

public static class RevenueMaturities
{
    public const string Expected = "expected";
    public const string Confirmed = "confirmed";
    public const string Actual = "actual";
}

public static class RevenueSourceTypes
{
    public const string Manual = "manual";
    /// <summary>Reserved — documents/AR must not invent a second economic revenue (C-004).</summary>
    public const string Document = "document";
    public const string AccountsReceivable = "accounts_receivable";
    public const string RatingDetail = "rating_detail";
    /// <summary>Postage line seeded from bill_waybills when economic role is revenue (ADR-0018).</summary>
    public const string WaybillPrefix = "waybill.";
}
