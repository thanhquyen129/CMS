using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: users (D01) — tenant-bound identity; JWT <c>sub</c> is actor (Dev may use X-User-Id).</summary>
public sealed class User : TenantEntityBase
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>ASP.NET Identity password hash (nullable until set via bootstrap or admin).</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Home organization for Data Scope = organization.</summary>
    public Guid? OrganizationId { get; set; }
}
