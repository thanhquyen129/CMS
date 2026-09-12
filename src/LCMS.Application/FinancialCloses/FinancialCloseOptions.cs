namespace LCMS.Application.FinancialCloses;

/// <summary>
/// Financial close options — eligibility checklist flags + period lock (Pass 2 Sprint 10 FULL).
/// Strict policy on a close record forces eligibility + period lock regardless of these flags.
/// </summary>
public sealed class FinancialCloseOptions
{
    public const string SectionName = "FinancialClose";

    /// <summary>
    /// When true (default), Locked closes reject Cost/Revenue confirm and Payment/Collection
    /// allocate/finalize in the locked period/scope. Strict policy always enforces.
    /// </summary>
    public bool EnforcePeriodLock { get; set; } = true;

    public CloseEligibilityOptions Eligibility { get; set; } = new();
}

/// <summary>Per-gate switches for close snapshot eligibility (default all on).</summary>
public sealed class CloseEligibilityOptions
{
    /// <summary>Block when open/in_progress/escalated critical exceptions exist in scope.</summary>
    public bool BlockOnCriticalExceptions { get; set; } = true;

    /// <summary>Block when accepted documents remain unmatched (or partially matched) in scope.</summary>
    public bool BlockOnUnmatchedAcceptedDocuments { get; set; } = true;

    /// <summary>
    /// Block when unsettled AP/AR open balance (derived outstanding) exceeds threshold in scope.
    /// </summary>
    public bool BlockOnUnsettledApArAboveThreshold { get; set; } = true;

    /// <summary>
    /// Outstanding above this amount blocks close (default 0 ⇒ any positive open balance).
    /// </summary>
    public decimal UnsettledApArOpenBalanceThreshold { get; set; }
}
