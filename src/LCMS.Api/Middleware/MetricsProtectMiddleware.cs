using LCMS.Domain.Entities;

namespace LCMS.Api.Middleware;

/// <summary>
/// Gates /metrics when Metrics:ScrapeToken is set (X-Metrics-Token header).
/// Development without token stays open for local scrape.
/// </summary>
public sealed class MetricsProtectMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _scrapeToken;
    private readonly bool _isDevelopment;

    public MetricsProtectMiddleware(RequestDelegate next, IConfiguration configuration, IHostEnvironment env)
    {
        _next = next;
        _scrapeToken = configuration["Metrics:ScrapeToken"];
        _isDevelopment = env.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(_scrapeToken))
            {
                if (!context.Request.Headers.TryGetValue("X-Metrics-Token", out var provided)
                    || !string.Equals(provided.ToString(), _scrapeToken, StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("metrics unauthorized");
                    return;
                }
            }
            else if (!_isDevelopment)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        await _next(context);
    }
}
