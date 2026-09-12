using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace LCMS.Api.Middleware;

/// <summary>
/// Fixed-window rate limit for /api/* only (health/ready/metrics exempt). Config: RateLimiting section.
/// Money paths can use a stricter permit limit (Pass 2 Sprint 12 FULL).
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

        var moneyPath = IsMoneyPath(context.Request.Path);
        var key = $"{ResolvePartitionKey(context)}:{(moneyPath ? "money" : "api")}";
        var limiter = _limiters.GetOrAdd(key, _ => CreateLimiter(moneyPath));

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

    private FixedWindowRateLimiter CreateLimiter(bool moneyPath)
    {
        var permit = moneyPath
            ? Math.Max(1, _options.MoneyPathPermitLimit)
            : Math.Max(1, _options.PermitLimit);

        return new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = permit,
            Window = _options.Window <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : _options.Window,
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    private bool IsMoneyPath(PathString path)
    {
        var prefixes = _options.MoneyPathPrefixes;
        if (prefixes is null || prefixes.Length == 0)
        {
            return false;
        }

        foreach (var prefix in prefixes)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                continue;
            }

            if (path.StartsWithSegments(prefix.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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

    /// <summary>Max requests per window per partition (tenant or IP) for general /api/*.</summary>
    public int PermitLimit { get; set; } = 300;

    /// <summary>Stricter max for money-critical paths (costs, settlements, close, …).</summary>
    public int MoneyPathPermitLimit { get; set; } = 60;

    /// <summary>Path prefixes treated as money paths (case-insensitive StartsWithSegments).</summary>
    public string[] MoneyPathPrefixes { get; set; } =
    [
        "/api/costs",
        "/api/revenues",
        "/api/payments",
        "/api/payment-allocations",
        "/api/collections",
        "/api/collection-allocations",
        "/api/accounts-payable",
        "/api/accounts-receivable",
        "/api/payable-exposures",
        "/api/receivable-exposures",
        "/api/financial-closes",
        "/api/financial-documents",
        "/api/document-matches"
    ];

    /// <summary>Fixed window length (e.g. 00:01:00).</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
