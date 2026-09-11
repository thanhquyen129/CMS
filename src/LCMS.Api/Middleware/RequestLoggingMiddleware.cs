using System.Diagnostics;
using LCMS.Infrastructure.Tenancy;

namespace LCMS.Api.Middleware;

/// <summary>
/// Structured request log: method, path, status, elapsed, correlationId, tenant, user.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var correlationId = CorrelationIdMiddleware.GetCorrelationId(context);
            var tenantId = context.RequestServices.GetService<HttpTenantContext>()?.TenantId;
            var userId = context.RequestServices.GetService<HttpCurrentUserContext>()?.UserId;
            _logger.LogInformation(
                "HTTP {Method} {Path} => {StatusCode} in {ElapsedMs}ms CorrelationId={CorrelationId} TenantId={TenantId} UserId={UserId}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                correlationId,
                tenantId,
                userId);
        }
    }
}
