using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: outbox_messages — optional local outbox stub (enqueue + process-once). No broker.
/// </summary>
public sealed class OutboxMessage : TenantEntityBase
{
    public string Topic { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";

    /// <summary>pending | processed | failed</summary>
    public string Status { get; set; } = OutboxMessageStatuses.Pending;

    public int AttemptNo { get; set; }
    public string? LastError { get; set; }

    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

public static class OutboxMessageStatuses
{
    public const string Pending = "pending";
    public const string Processed = "processed";
    public const string Failed = "failed";
}
