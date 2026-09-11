using System.Diagnostics;
using System.Text.Json;
using LCMS.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.TraceIdentifier;
        if (Activity.Current?.Id is { } activityId)
        {
            correlationId = activityId;
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        int statusCode;
        string code;
        string message;
        object? errors = null;

        switch (exception)
        {
            case ValidationAppException validation:
                statusCode = validation.StatusCode;
                code = validation.ErrorCode;
                message = validation.Message;
                errors = validation.Errors;
                break;
            case AppException app:
                statusCode = app.StatusCode;
                code = app.ErrorCode;
                message = app.Message;
                break;
            case DbUpdateConcurrencyException:
                statusCode = StatusCodes.Status409Conflict;
                code = "concurrency_conflict";
                message = "Dữ liệu đã bị thay đổi bởi người khác. Vui lòng tải lại và thử lại.";
                break;
            default:
                statusCode = StatusCodes.Status500InternalServerError;
                code = "internal_error";
                message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.";
                _logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);
                break;
        }

        context.Response.StatusCode = statusCode;

        var payload = new Dictionary<string, object?>
        {
            ["correlationId"] = correlationId,
            ["code"] = code,
            ["message"] = message
        };

        if (errors is not null)
        {
            payload["errors"] = errors;
        }

        if (_env.IsDevelopment() && statusCode >= 500)
        {
            payload["detail"] = exception.ToString();
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
