namespace LCMS.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>When true, protected APIs require a valid JWT Bearer token.</summary>
    public bool RequireJwt { get; set; }

    /// <summary>When true, X-Tenant-Id / X-User-Id may populate context if JWT claims are absent.</summary>
    public bool AllowHeaderBootstrap { get; set; }

    public JwtOptions Jwt { get; set; } = new();

    /// <summary>Optional operator bootstrap user (env on host — never commit secrets).</summary>
    public BootstrapOptions Bootstrap { get; set; } = new();
}

public sealed class BootstrapOptions
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string DisplayName { get; set; } = "Operator";
    public Guid? TenantId { get; set; }
    public string TenantCode { get; set; } = "ops";
    public string TenantName { get; set; } = "Vận hành";
}

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "lcms-api";
    public string Audience { get; set; } = "lcms-api";

    /// <summary>HMAC signing key. Production: set via env Auth__Jwt__SigningKey. Never commit real secrets.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 60;
}
