namespace LCMS.Domain.Common;

/// <summary>
/// C-001: business/financial rows carry tenant_id (or safely derive via parent).
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
