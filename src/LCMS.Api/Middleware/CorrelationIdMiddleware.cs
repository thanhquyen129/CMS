using LCMS.Infrastructure.Tenancy;
using Microsoft.Extensions.Primitives;

namespace LCMS.Api.Middleware;

/// <summary>
/// Generates or propagates <c>X-Correlation-Id</c> for request tracing.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILoggerFactory _loggerFactory;

    public CorrelationIdMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
    {
        _next = next;
        _loggerFactory = loggerFactory;
    }

    public async Task InvokeAsync(HttpContext context, HttpCorrelationContext correlationContext)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;
        correlationContext.CorrelationId = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        var logger = _loggerFactory.CreateLogger("Correlation");
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }

    public static string GetCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var value) && value is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        return context.TraceIdentifier;
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out StringValues incoming)
            && !StringValues.IsNullOrEmpty(incoming))
        {
            var candidate = incoming.ToString().Trim();
            if (candidate.Length is > 0 and <= 128
                && candidate.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.'))
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N");
    }
}
