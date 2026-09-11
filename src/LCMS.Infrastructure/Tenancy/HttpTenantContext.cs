using LCMS.Application.Abstractions;

namespace LCMS.Infrastructure.Tenancy;

/// <summary>
/// Resolves tenant from HTTP header X-Tenant-Id until auth claims land (Phase 1).
/// </summary>
public sealed class HttpTenantContext : ITenantContext
{
    public Guid? TenantId { get; set; }
}
