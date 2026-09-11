using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: organizations (D01) — stub skeleton for Phase 1 master data.</summary>
public sealed class Organization : TenantEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}
