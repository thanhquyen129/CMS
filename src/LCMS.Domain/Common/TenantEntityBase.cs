namespace LCMS.Domain.Common;

/// <summary>
/// Tenant-scoped aggregate root / entity baseline (C-001).
/// </summary>
public abstract class TenantEntityBase : EntityBase, ITenantScoped
{
    public Guid TenantId { get; set; }
}
