using LCMS.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace LCMS.Api.Endpoints;

public static class DevAuthEndpoints
{
    public static IEndpointRouteBuilder MapDevAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dev").WithTags("Dev");

        group.MapPost("/token", (
                [FromBody] DevTokenRequest? body,
                JwtTokenIssuer issuer,
                IHostEnvironment env) =>
            {
                if (!env.IsDevelopment())
                {
                    return Results.Json(new
                    {
                        code = "not_found",
                        message = "Không tìm thấy tài nguyên."
                    }, statusCode: StatusCodes.Status404NotFound);
                }

                if (body is null || body.TenantId == Guid.Empty || body.UserId == Guid.Empty)
                {
                    return Results.Json(new
                    {
                        code = "validation_error",
                        message = "tenantId và userId là bắt buộc (Guid khác rỗng)."
                    }, statusCode: StatusCodes.Status400BadRequest);
                }

                var (accessToken, expiresIn) = issuer.Issue(body.TenantId, body.UserId);
                return Results.Ok(new
                {
                    accessToken,
                    tokenType = "Bearer",
                    expiresIn
                });
            })
            .AllowAnonymous()
            .WithSummary("Dev-only JWT helper (tenant_id + sub claims)");

        return app;
    }
}

public sealed record DevTokenRequest(Guid TenantId, Guid UserId);
