using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace LCMS.Api.Middleware;

/// <summary>
/// Fixed-window rate limit for /api/* only (health/ready/metrics exempt). Config: RateLimiting section.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitingOptions _options;
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> _limiters = new();

    public RateLimitingMiddleware(RequestDelegate next, IOptions<RateLimitingOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || !IsApiPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var key = ResolvePartitionKey(context);
        var limiter = _limiters.GetOrAdd(key, _ => CreateLimiter());

        using var lease = await limiter.AcquireAsync(1, context.RequestAborted);
        if (!lease.IsAcquired)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = Math.Max(1, (int)_options.Window.TotalSeconds).ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                code = "rate_limited",
                message = "Quá nhiều yêu cầu. Vui lòng thử lại sau."
            });
            return;
        }

        await _next(context);
    }

    private FixedWindowRateLimiter CreateLimiter() =>
        new(new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, _options.PermitLimit),
            Window = _options.Window <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : _options.Window,
            QueueLimit = 0,
            AutoReplenishment = true
        });

    private static bool IsApiPath(PathString path) =>
        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    private static string ResolvePartitionKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(TenantResolutionMiddleware.TenantHeaderName, out var tenant)
            && !string.IsNullOrWhiteSpace(tenant))
        {
            return $"tenant:{tenant}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    /// <summary>Max requests per window per partition (tenant or IP).</summary>
    public int PermitLimit { get; set; } = 300;

    /// <summary>Fixed window length (e.g. 00:01:00).</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
