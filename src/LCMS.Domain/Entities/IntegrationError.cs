using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: integration_errors (D12) — retry/reprocess trace for a failed integration record.
/// Thin stub for Pass 1 (full outbox/retry = Pass 2).
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

    public IntegrationRecord? IntegrationRecord { get; set; }
}
