using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: roles (D01) — tenant-scoped role for Action permissions.</summary>
public sealed class Role : TenantEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
}
