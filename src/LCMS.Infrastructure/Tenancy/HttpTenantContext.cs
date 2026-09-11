using LCMS.Application.Abstractions;

namespace LCMS.Infrastructure.Tenancy;

/// <summary>
/// Resolves tenant from JWT claim tenant_id (preferred) or Dev header X-Tenant-Id.
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
}
