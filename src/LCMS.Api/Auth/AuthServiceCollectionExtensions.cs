using System.Text;
using System.Text.Json;
using LCMS.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace LCMS.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    public const string DevFallbackSigningKey = "lcms-dev-only-signing-key-min-32-chars!";

    public static IServiceCollection AddLcmsAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment env)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        var section = configuration.GetSection(AuthOptions.SectionName);
        var requireJwtConfigured = section.GetSection(nameof(AuthOptions.RequireJwt)).Exists();
        var headerBootstrapConfigured = section.GetSection(nameof(AuthOptions.AllowHeaderBootstrap)).Exists();

        var requireJwt = requireJwtConfigured
            ? section.GetValue<bool>(nameof(AuthOptions.RequireJwt))
            : env.IsProduction();

        var allowHeaderBootstrap = headerBootstrapConfigured
            ? section.GetValue<bool>(nameof(AuthOptions.AllowHeaderBootstrap))
            : !env.IsProduction();

        var jwtSection = section.GetSection("Jwt");
        var issuer = jwtSection.GetValue<string>("Issuer") ?? "lcms-api";
        var audience = jwtSection.GetValue<string>("Audience") ?? "lcms-api";
        var expiryMinutes = jwtSection.GetValue("ExpiryMinutes", 60);
        var signingKey = jwtSection.GetValue<string>("SigningKey");
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            signingKey = env.IsProduction() ? string.Empty : DevFallbackSigningKey;
        }

        if (env.IsProduction() && requireJwt && signingKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Production RequireJwt=true needs Auth:Jwt:SigningKey (env Auth__Jwt__SigningKey) ≥ 32 characters.");
        }

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            signingKey = DevFallbackSigningKey;
        }

        services.PostConfigure<AuthOptions>(opts =>
        {
            opts.RequireJwt = requireJwt;
            opts.AllowHeaderBootstrap = allowHeaderBootstrap;
            opts.Jwt.Issuer = issuer;
            opts.Jwt.Audience = audience;
            opts.Jwt.ExpiryMinutes = expiryMinutes;
            opts.Jwt.SigningKey = signingKey;
        });

        services.AddSingleton<JwtTokenIssuer>();
        services.AddScoped<AuthTokenService>();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();

        var keyBytes = Encoding.UTF8.GetBytes(signingKey);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json; charset=utf-8";
                        context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
                        var payload = new Dictionary<string, object?>
                        {
                            ["correlationId"] = correlationId,
                            ["code"] = "unauthorized",
                            ["message"] = "Yêu cầu xác thực. Vui lòng gửi Bearer token hợp lệ."
                        };
                        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
                    },
                    OnForbidden = async context =>
                    {
                        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json; charset=utf-8";
                        context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
                        var payload = new Dictionary<string, object?>
                        {
                            ["correlationId"] = correlationId,
                            ["code"] = "forbidden",
                            ["message"] = "Bạn không có quyền thực hiện thao tác này."
                        };
                        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            if (requireJwt)
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            }
        });

        return services;
    }
}
