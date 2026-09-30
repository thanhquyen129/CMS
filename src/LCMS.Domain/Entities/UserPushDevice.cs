using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: user_push_devices — mobile push notification device tokens per operator (iOS/Android/Expo).
/// Tenant-scoped and user-scoped; revoked/deactivated on logout or account switch.
/// </summary>
public sealed class UserPushDevice : TenantEntityBase
{
    public Guid UserId { get; set; }

    /// <summary>Expo push token or FCM/APNs registration token.</summary>
    public string DeviceToken { get; set; } = string.Empty;

    /// <summary>ios | android | web</summary>
    public string Platform { get; set; } = PushDevicePlatforms.Android;

    public string? DeviceName { get; set; }
    public string? AppVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class PushDevicePlatforms
{
    public const string Ios = "ios";
    public const string Android = "android";
    public const string Web = "web";

    public static bool IsValid(string? platform) =>
        platform is Ios or Android or Web;
}

