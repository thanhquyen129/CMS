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
                AuthTokenService tokens,
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

                var pair = await tokens.IssuePairAsync(matched.TenantId, matched.Id, ct);
                return Results.Ok(new
                {
                    accessToken = pair.AccessToken,
                    refreshToken = pair.RefreshToken,
                    tokenType = "Bearer",
                    expiresIn = pair.ExpiresInSeconds,
                    refreshExpiresIn = pair.RefreshExpiresInSeconds,
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
            .WithSummary("Email/password login → JWT + refresh (tenant_id + sub)");

        group.MapPost("/refresh", async (
                [FromBody] RefreshRequest? body,
                AuthTokenService tokens,
                CancellationToken ct) =>
            {
                var pair = await tokens.RefreshAsync(body?.RefreshToken ?? string.Empty, ct);
                if (pair is null)
                {
                    return Results.Json(new
                    {
                        code = "invalid_refresh",
                        message = "Refresh token không hợp lệ hoặc đã hết hạn."
                    }, statusCode: StatusCodes.Status401Unauthorized);
                }

                return Results.Ok(new
                {
                    accessToken = pair.AccessToken,
                    refreshToken = pair.RefreshToken,
                    tokenType = "Bearer",
                    expiresIn = pair.ExpiresInSeconds,
                    refreshExpiresIn = pair.RefreshExpiresInSeconds
                });
            })
            .AllowAnonymous()
            .WithSummary("Rotate refresh token → new access + refresh");

        group.MapPost("/logout", async (
                [FromBody] RefreshRequest? body,
                AuthTokenService tokens,
                CancellationToken ct) =>
            {
                await tokens.RevokeAsync(body?.RefreshToken, ct);
                return Results.Ok(new { ok = true });
            })
            .AllowAnonymous()
            .WithSummary("Revoke refresh token (P22)");

        return app;
    }
}

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string? RefreshToken);
