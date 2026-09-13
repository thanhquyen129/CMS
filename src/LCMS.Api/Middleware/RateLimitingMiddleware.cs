using System.Collections.Concurrent;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LCMS.Api.Middleware;

/// <summary>
/// Fixed-window rate limit for /api/* (health/ready/metrics exempt).
/// Uses Redis when RateLimiting:RedisConnection / ConnectionStrings:Redis is set (P23); else in-process.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitingOptions _options;
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> _limiters = new();
    private readonly IConnectionMultiplexer? _redis;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IOptions<RateLimitingOptions> options,
        IServiceProvider services)
    {
        _next = next;
        _options = options.Value;
        _redis = services.GetService<IConnectionMultiplexer>();
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

        var allowed = _redis is not null
            ? await TryAcquireRedisAsync(key, moneyPath, context.RequestAborted)
            : await TryAcquireLocalAsync(key, moneyPath, context.RequestAborted);

        if (!allowed)
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

    private async Task<bool> TryAcquireLocalAsync(string key, bool moneyPath, CancellationToken ct)
    {
        var limiter = _limiters.GetOrAdd(key, _ => CreateLimiter(moneyPath));
        using var lease = await limiter.AcquireAsync(1, ct);
        return lease.IsAcquired;
    }

    private async Task<bool> TryAcquireRedisAsync(string key, bool moneyPath, CancellationToken ct)
    {
        try
        {
            var db = _redis!.GetDatabase();
            var permit = moneyPath
                ? Math.Max(1, _options.MoneyPathPermitLimit)
                : Math.Max(1, _options.PermitLimit);
            var windowSec = Math.Max(1, (int)_options.Window.TotalSeconds);
            var redisKey = $"rl:{key}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds() / windowSec}";
            var count = await db.StringIncrementAsync(redisKey);
            if (count == 1)
            {
                await db.KeyExpireAsync(redisKey, TimeSpan.FromSeconds(windowSec + 1));
            }

            return count <= permit;
        }
        catch
        {
            return await TryAcquireLocalAsync(key, moneyPath, ct);
        }
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
    public int PermitLimit { get; set; } = 300;
    public int MoneyPathPermitLimit { get; set; } = 60;
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
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
    public string? RedisConnection { get; set; }
}
