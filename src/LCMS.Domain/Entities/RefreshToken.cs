using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: refresh_tokens — opaque refresh tokens (hashed). ADR-0002 follow-up / P22.
/// </summary>
public sealed class RefreshToken : TenantEntityBase
{
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hex of the opaque refresh token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}
