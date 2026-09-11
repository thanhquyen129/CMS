using System.Security.Claims;
using LCMS.Api.Auth;
using LCMS.Infrastructure.Tenancy;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace LCMS.Api.Middleware;

/// <summary>
/// Resolves tenant + actor from JWT claims (tenant_id, sub), optionally falling back to headers in Dev.
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
        HttpCurrentUserContext userContext,
        IOptions<AuthOptions> authOptions)
    {
        var auth = authOptions.Value;
        TryAssignFromJwt(context.User, tenantContext, userContext);

        if (auth.AllowHeaderBootstrap)
        {
            if (!tenantContext.TenantId.HasValue
                && context.Request.Headers.TryGetValue(TenantHeaderName, out var rawTenant)
                && Guid.TryParse(rawTenant.ToString(), out var tenantId)
                && tenantId != Guid.Empty)
            {
                tenantContext.TenantId = tenantId;
            }

            if (!userContext.UserId.HasValue
                && context.Request.Headers.TryGetValue(UserHeaderName, out var rawUser)
                && Guid.TryParse(rawUser.ToString(), out var userId)
                && userId != Guid.Empty)
            {
                userContext.UserId = userId;
            }
        }

        using (LogContext.PushProperty("TenantId", tenantContext.TenantId))
        using (LogContext.PushProperty("UserId", userContext.UserId))
        {
            await _next(context);
        }
    }

    private static void TryAssignFromJwt(
        ClaimsPrincipal user,
        HttpTenantContext tenantContext,
        HttpCurrentUserContext userContext)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var tenantRaw = user.FindFirstValue(JwtClaimNames.TenantId)
            ?? user.FindFirstValue("tenantId");
        if (Guid.TryParse(tenantRaw, out var tenantId) && tenantId != Guid.Empty)
        {
            tenantContext.TenantId = tenantId;
        }

        var subRaw = user.FindFirstValue("sub")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(subRaw, out var userId) && userId != Guid.Empty)
        {
            userContext.UserId = userId;
        }
    }
}
