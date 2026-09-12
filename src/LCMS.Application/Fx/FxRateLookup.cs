using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Fx;

public sealed record FxRateResolution(Guid FxRateId, decimal Rate, DateOnly RateDate, int Version, string Source);

/// <summary>
/// Dated fx_rates lookup: latest RateDate ≤ asOf for from→to (tenant-scoped).
/// </summary>
public interface IFxRateLookup
{
    Task<FxRateResolution?> ResolveAsync(
        string fromCurrencyCode,
        string toCurrencyCode,
        DateOnly asOf,
        CancellationToken cancellationToken = default);
}

public sealed class FxRateLookup : IFxRateLookup
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public FxRateLookup(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<FxRateResolution?> ResolveAsync(
        string fromCurrencyCode,
        string toCurrencyCode,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var from = fromCurrencyCode.Trim().ToUpperInvariant();
        var to = toCurrencyCode.Trim().ToUpperInvariant();
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var match = await _db.FxRates
            .AsNoTracking()
            .Where(r => r.FromCurrencyCode == from
                        && r.ToCurrencyCode == to
                        && r.RateDate <= asOf)
            .OrderByDescending(r => r.RateDate)
            .ThenByDescending(r => r.Version)
            .Select(r => new { r.Id, r.Rate, r.RateDate, r.Version, r.Source })
            .FirstOrDefaultAsync(cancellationToken);

        if (match is null || match.Rate <= 0)
        {
            return null;
        }

        return new FxRateResolution(match.Id, match.Rate, match.RateDate, match.Version, match.Source);
    }
}
