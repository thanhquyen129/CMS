using System.Security.Cryptography;
using System.Text;
using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Api.Auth;

public sealed record TokenPair(string AccessToken, int ExpiresInSeconds, string RefreshToken, int RefreshExpiresInSeconds);

/// <summary>Issues access JWT + stores hashed refresh tokens (P22).</summary>
public sealed class AuthTokenService
{
    private readonly JwtTokenIssuer _issuer;
    private readonly AuthOptions _options;
    private readonly ILcmsDbContext _db;

    public AuthTokenService(JwtTokenIssuer issuer, IOptions<AuthOptions> options, ILcmsDbContext db)
    {
        _issuer = issuer;
        _options = options.Value;
        _db = db;
    }

    public async Task<TokenPair> IssuePairAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        var (access, expiresIn) = _issuer.Issue(tenantId, userId);
        var refreshDays = Math.Clamp(_options.Jwt.RefreshExpiryDays, 1, 90);
        var refreshPlain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var hash = Hash(refreshPlain);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(refreshDays);

        _db.RefreshTokens.Add(new RefreshToken
        {
            TenantId = tenantId,
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = expiresAt
        });
        await _db.SaveChangesAsync(ct);

        return new TokenPair(access, expiresIn, refreshPlain, (int)TimeSpan.FromDays(refreshDays).TotalSeconds);
    }

    public async Task<TokenPair?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var hash = Hash(refreshToken.Trim());
        var row = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.DeletedAt == null, ct);

        if (row is null
            || row.RevokedAt.HasValue
            || row.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        row.RevokedAt = DateTimeOffset.UtcNow;
        var pair = await IssuePairAsync(row.TenantId, row.UserId, ct);
        row.ReplacedByTokenHash = Hash(pair.RefreshToken);
        await _db.SaveChangesAsync(ct);
        return pair;
    }

    public async Task RevokeAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = Hash(refreshToken.Trim());
        var row = await _db.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.DeletedAt == null, ct);
        if (row is null || row.RevokedAt.HasValue)
        {
            return;
        }

        row.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static string Hash(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
