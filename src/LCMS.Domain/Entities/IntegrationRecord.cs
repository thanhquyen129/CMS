using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: integration_records (D12) — inbound/outbound integration identity + status.
/// C-002 / IDX-012 unique: tenant_id, source_system, external_object_type, external_id.
/// </summary>
public sealed class IntegrationRecord : TenantEntityBase
{
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>External object type, e.g. order, invoice, payment_advice.</summary>
    public string ExternalObjectType { get; set; } = string.Empty;

    public string ExternalId { get; set; } = string.Empty;

    public string? ExternalVersion { get; set; }

    /// <summary>received | accepted | rejected | processed | failed</summary>
    public string Status { get; set; } = IntegrationRecordStatuses.Received;

    /// <summary>Optional local CMS object created/linked from this integration.</summary>
    public string? LocalObjectType { get; set; }

    public Guid? LocalObjectId { get; set; }

    public string? PayloadHash { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }

    public ICollection<IntegrationError> Errors { get; set; } = new List<IntegrationError>();
}

public static class IntegrationRecordStatuses
{
    public const string Received = "received";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string Processed = "processed";
    public const string Failed = "failed";
}
