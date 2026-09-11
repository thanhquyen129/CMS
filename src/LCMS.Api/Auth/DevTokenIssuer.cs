using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LCMS.Api.Auth;

public sealed class DevTokenIssuer
{
    private readonly AuthOptions _options;

    public DevTokenIssuer(IOptions<AuthOptions> options)
    {
        _options = options.Value;
    }

    public (string AccessToken, int ExpiresInSeconds) Issue(Guid tenantId, Guid userId)
    {
        var jwt = _options.Jwt;
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException("Auth:Jwt:SigningKey must be at least 32 characters.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(Math.Clamp(jwt.ExpiryMinutes, 1, 24 * 60));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new(JwtClaimNames.TenantId, tenantId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: expires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var expiresIn = (int)Math.Max(1, (expires - DateTime.UtcNow).TotalSeconds);
        return (accessToken, expiresIn);
    }
}
