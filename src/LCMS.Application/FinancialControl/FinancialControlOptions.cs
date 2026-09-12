namespace LCMS.Application.FinancialControl;

/// <summary>
/// Financial control options — variance severity thresholds, exception SLA, confirm block (Pass 2 Sprint 9 FULL).
/// </summary>
public sealed class FinancialControlOptions
{
    public const string SectionName = "FinancialControl";

    /// <summary>
    /// Absolute variance amount thresholds (txn currency). Below Medium ⇒ low.
    /// Amounts compared as Math.Abs(varianceAmount).
    /// </summary>
    public VarianceSeverityThresholdOptions VarianceSeverityThresholds { get; set; } = new();

    /// <summary>
    /// Default SLA hours by severity when OpenException omits DueAt.
    /// </summary>
    public Dictionary<string, int> DefaultExceptionSlaHours { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["low"] = 168,
        ["medium"] = 72,
        ["high"] = 24,
        ["critical"] = 8
    };

    /// <summary>
    /// When true (default), Cost/Revenue confirm is blocked if an open critical exception
    /// is linked to the same object (object_type + object_id).
    /// </summary>
    public bool BlockConfirmOnCriticalException { get; set; } = true;
}

public sealed class VarianceSeverityThresholdOptions
{
    /// <summary>Inclusive lower bound for medium (default 100).</summary>
    public decimal Medium { get; set; } = 100m;

    /// <summary>Inclusive lower bound for high (default 1_000).</summary>
    public decimal High { get; set; } = 1_000m;

    /// <summary>Inclusive lower bound for critical (default 10_000).</summary>
    public decimal Critical { get; set; } = 10_000m;
}
