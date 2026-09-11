namespace LCMS.Application.Revenues;

/// <summary>
/// Revenue module options — FX stub + optional confirm approval threshold (Pass 2 Sprint 5 FULL).
/// Same ADR-0004 pattern as Cost.
/// </summary>
public sealed class RevenueOptions
{
    public const string SectionName = "Revenue";

    /// <summary>Tenant reporting/base currency for stub FX (default VND).</summary>
    public string BaseCurrency { get; set; } = "VND";

    /// <summary>
    /// Stub FX rates: 1 unit of key currency → BaseCurrency.
    /// Missing key ⇒ reject cross-currency convert (C-014 stub).
    /// </summary>
    public Dictionary<string, decimal> StubFxRatesToBase { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 25_000m,
        ["EUR"] = 27_000m
    };

    /// <summary>
    /// When set, confirm requires ApprovalStatus=approved if BaseAmount exceeds this (in base currency).
    /// Null = threshold disabled.
    /// </summary>
    public decimal? ConfirmApprovalThresholdBase { get; set; }
}
