namespace LCMS.Application.Costs;

/// <summary>
/// Cost module options — FX stub + optional confirm approval threshold (Pass 2 Sprint 4 FULL).
/// </summary>
public sealed class CostOptions
{
    public const string SectionName = "Cost";

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
