using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: users (D01) — tenant-bound identity (JWT deferred; X-User-Id bootstrap).</summary>
public sealed class User : TenantEntityBase
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
