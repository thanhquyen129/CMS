using LCMS.Infrastructure.Tenancy;

namespace LCMS.Api.Middleware;

/// <summary>
/// Temporary tenant + actor resolution via headers until JWT claims.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    public const string TenantHeaderName = "X-Tenant-Id";
    public const string UserHeaderName = "X-User-Id";

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        HttpTenantContext tenantContext,
        HttpCurrentUserContext userContext)
    {
        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var rawTenant)
            && Guid.TryParse(rawTenant.ToString(), out var tenantId)
            && tenantId != Guid.Empty)
        {
            tenantContext.TenantId = tenantId;
        }

        if (context.Request.Headers.TryGetValue(UserHeaderName, out var rawUser)
            && Guid.TryParse(rawUser.ToString(), out var userId)
            && userId != Guid.Empty)
        {
            userContext.UserId = userId;
        }

        await _next(context);
    }
}
