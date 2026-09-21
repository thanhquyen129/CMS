using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: tenant_notification_settings — one row per tenant (ADR-0021).</summary>
public sealed class TenantNotificationSetting : TenantEntityBase
{
    public bool InAppEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; }

    /// <summary>
    /// Per-event JSON: { "approval.pending": { "inApp": true, "email": false }, ... }.
    /// </summary>
    public string EventsJson { get; set; } = "{}";
}

public static class NotificationEventTypes
{
    public const string ApprovalPending = "approval.pending";
    public const string ExceptionOpened = "exception.opened";
    public const string IntegrationError = "integration.error";
    public const string CloseCompleted = "close.completed";
    public const string UserPasswordSet = "user.password_set";

    public static readonly IReadOnlyList<(string Code, string NameVi)> Catalog =
    [
        (ApprovalPending, "Yêu cầu phê duyệt mới"),
        (ExceptionOpened, "Ngoại lệ tài chính mở"),
        (IntegrationError, "Lỗi tích hợp"),
        (CloseCompleted, "Chốt tài chính hoàn tất"),
        (UserPasswordSet, "Mật khẩu người dùng được đặt lại")
    ];
}
