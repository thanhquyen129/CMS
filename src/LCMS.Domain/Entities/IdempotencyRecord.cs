using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: idempotency_records — one API event creates one financial row.</summary>
public sealed class IdempotencyRecord : TenantEntityBase
{
    public string Scope { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
}
