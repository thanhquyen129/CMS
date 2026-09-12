using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: integration_errors (D12) — retry/reprocess trace for a failed integration record.
/// Pass 2: mark-retried / dead-letter recovery stub (no broker).
/// </summary>
public sealed class IntegrationError : TenantEntityBase
{
    public Guid IntegrationRecordId { get; set; }

    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }

    public int AttemptNo { get; set; } = 1;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? NextRetryAt { get; set; }

    /// <summary>pending | retried | dead_letter</summary>
    public string RecoveryStatus { get; set; } = IntegrationErrorRecoveryStatuses.Pending;

    public DateTimeOffset? RecoveredAt { get; set; }
    public Guid? RecoveredBy { get; set; }
    public string? RecoveryNote { get; set; }

    public IntegrationRecord? IntegrationRecord { get; set; }
}

public static class IntegrationErrorRecoveryStatuses
{
    public const string Pending = "pending";
    public const string Retried = "retried";
    public const string DeadLetter = "dead_letter";
}
