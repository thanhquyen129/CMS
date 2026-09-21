using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: in_app_notifications — operator inbox (ADR-0021).</summary>
public sealed class InAppNotification : TenantEntityBase
{
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Href { get; set; }
    public string? ObjectType { get; set; }
    public Guid? ObjectId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
