using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: exceptions (D10) — ngoại lệ as managed work item (rule/severity/owner/SLA).
/// Class named FinancialException to avoid clash with System.Exception.
/// Variance ≠ Exception: opening Exception does not invent Variance and vice versa.
/// </summary>
public sealed class FinancialException : TenantEntityBase
{
    /// <summary>Business/rule key, e.g. RECON_VARIANCE_THRESHOLD.</summary>
    public string RuleCode { get; set; } = string.Empty;

    /// <summary>low | medium | high | critical</summary>
    public string Severity { get; set; } = ExceptionSeverities.Medium;

    public Guid? OwnerId { get; set; }

    /// <summary>open | in_progress | escalated | resolved | closed | cancelled</summary>
    public string Status { get; set; } = ExceptionStatuses.Open;

    /// <summary>SLA due (IDX-011).</summary>
    public DateTimeOffset? DueAt { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? BillId { get; set; }
    public Guid? ReconciliationId { get; set; }

    /// <summary>Optional link to a Variance control fact — independent lifecycle.</summary>
    public Guid? VarianceId { get; set; }

    /// <summary>
    /// Linked financial object (cost | revenue | document | payment | …).
    /// Used by confirm block-on-critical and inbox filters.
    /// </summary>
    public string? ObjectType { get; set; }

    public Guid? ObjectId { get; set; }

    public DateTimeOffset? EscalatedAt { get; set; }
    public Guid? EscalatedBy { get; set; }
    public string? EscalationReason { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }

    public Bill? Bill { get; set; }
    public Reconciliation? Reconciliation { get; set; }
    public Variance? Variance { get; set; }
}

public static class ExceptionSeverities
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";
    public const string Critical = "critical";
}

public static class ExceptionStatuses
{
    public const string Open = "open";
    public const string InProgress = "in_progress";
    public const string Escalated = "escalated";
    public const string Resolved = "resolved";
    public const string Closed = "closed";
    public const string Cancelled = "cancelled";
}
