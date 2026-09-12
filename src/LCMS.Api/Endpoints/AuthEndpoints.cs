using LCMS.Api.Auth;
using LCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (
                [FromBody] LoginRequest? body,
                LcmsDbContext db,
                IPasswordHasherService passwordHasher,
                JwtTokenIssuer issuer,
                CancellationToken ct) =>
            {
                if (body is null
                    || string.IsNullOrWhiteSpace(body.Email)
                    || string.IsNullOrWhiteSpace(body.Password))
                {
                    return Results.Json(new
                    {
                        code = "validation_error",
                        message = "Email và mật khẩu là bắt buộc."
                    }, statusCode: StatusCodes.Status400BadRequest);
                }

                var email = body.Email.Trim().ToLowerInvariant();
                // Login is pre-tenant: ignore tenant filter; still exclude soft-deleted.
                var candidates = await db.Users
                    .IgnoreQueryFilters()
                    .Where(u => u.Email == email && u.IsActive && u.DeletedAt == null)
                    .ToListAsync(ct);

                var matched = candidates
                    .OrderBy(u => u.CreatedAt)
                    .FirstOrDefault(u => passwordHasher.VerifyPassword(u, body.Password));
                if (matched is null)
                {
                    return Results.Json(new
                    {
                        code = "invalid_credentials",
                        message = "Email hoặc mật khẩu không đúng."
                    }, statusCode: StatusCodes.Status401Unauthorized);
                }

                var (accessToken, expiresIn) = issuer.Issue(matched.TenantId, matched.Id);
                return Results.Ok(new
                {
                    accessToken,
                    tokenType = "Bearer",
                    expiresIn,
                    user = new
                    {
                        id = matched.Id,
                        email = matched.Email,
                        displayName = matched.DisplayName,
                        tenantId = matched.TenantId
                    }
                });
            })
            .AllowAnonymous()
            .WithSummary("Email/password login → JWT (tenant_id + sub)");

        return app;
    }
}

public sealed record LoginRequest(string Email, string Password);
