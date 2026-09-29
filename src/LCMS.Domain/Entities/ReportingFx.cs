namespace LCMS.Domain.Entities;

/// <summary>
/// FX snapshot to the tenant Reporting Currency (ADR-0040). Original amount/currency stay on the record;
/// the reporting amount is derived once with this snapshot and never re-converted with a later rate.
/// </summary>
public interface IReportingFx
{
    string CurrencyCode { get; }
    string? ReportingCurrencyCode { get; set; }
    decimal? FxRate { get; set; }
    string? FxSourceType { get; set; }
    string? FxSourceName { get; set; }
    DateOnly? FxRateDate { get; set; }
    Guid? FxRateId { get; set; }
    string? FxOverrideReason { get; set; }
    Guid? FxAppliedBy { get; set; }
    DateTimeOffset? FxAppliedAt { get; set; }
    string FxStatus { get; set; }
}

public static class FxSourceTypes
{
    public const string Identity = "identity";
    public const string AutoProvider = "auto_provider";
    public const string Manual = "manual";
    public const string Override = "override";
    public const string Policy = "policy";
}

public static class FxStatuses
{
    /// <summary>Reporting amount is valid and may be aggregated.</summary>
    public const string Converted = "converted";

    /// <summary>No FX snapshot — excluded from reporting totals (FX-MISS-01).</summary>
    public const string Missing = "missing";

    /// <summary>Legacy cross-currency row without a trusted historical rate (MIG-03).</summary>
    public const string RequiresReview = "requires_review";
}
