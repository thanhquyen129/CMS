using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: user_roles — User ↔ Role assignment (tenant-scoped).</summary>
public sealed class UserRole : TenantEntityBase
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
}
