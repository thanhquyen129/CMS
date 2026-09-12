using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: variances (D10) — chênh lệch as a derived/control fact.
/// Variance ≠ Exception: this is quantitative control, not an inbox/SLA work item.
/// Optional link to Exception only when escalated; never auto-opens Exception.
/// </summary>
public sealed class Variance : TenantEntityBase
{
    public Guid? ReconciliationId { get; set; }
    public Guid? ReconciliationDetailId { get; set; }

    /// <summary>amount | quantity | rate | other</summary>
    public string VarianceType { get; set; } = VarianceTypes.Amount;

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "VND";

    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }

    /// <summary>open | accepted | written_off | cleared</summary>
    public string Status { get; set; } = VarianceStatuses.Open;

    /// <summary>
    /// low | medium | high | critical — derived from absolute amount vs config thresholds.
    /// Control severity only; does not open Exception (Variance ≠ Exception).
    /// </summary>
    public string Severity { get; set; } = VarianceSeverities.Low;

    public string? Explanation { get; set; }

    /// <summary>Optional escalation link — Exception is a separate entity/workflow.</summary>
    public Guid? ExceptionId { get; set; }

    public Reconciliation? Reconciliation { get; set; }
    public ReconciliationDetail? ReconciliationDetail { get; set; }
    public FinancialException? Exception { get; set; }
}

public static class VarianceTypes
{
    public const string Amount = "amount";
    public const string Quantity = "quantity";
    public const string Rate = "rate";
    public const string Other = "other";
}

public static class VarianceStatuses
{
    public const string Open = "open";
    public const string Accepted = "accepted";
    public const string WrittenOff = "written_off";
    public const string Cleared = "cleared";
}

public static class VarianceSeverities
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";
}
