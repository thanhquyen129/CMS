using LCMS.Infrastructure.Tenancy;

namespace LCMS.Api.Middleware;

/// <summary>
/// Temporary tenant resolution via X-Tenant-Id until JWT claims (Phase 1 identity).
/// </summary>
public sealed class TenantResolutionMiddleware
{
    public const string TenantHeaderName = "X-Tenant-Id";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, HttpTenantContext tenantContext)
    {
        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var raw)
            && Guid.TryParse(raw.ToString(), out var tenantId)
            && tenantId != Guid.Empty)
        {
            tenantContext.TenantId = tenantId;
        }

        await _next(context);
    }
}
