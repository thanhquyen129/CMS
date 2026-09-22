using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.ReferenceMasters;

/// <summary>Resolved canonical place. Code is the display key stored on the transaction.</summary>
public sealed record BoundPlace(Guid? LocationId, string? Code);

/// <summary>Resolves location codes and aliases. Free text is rejected once the tenant has locations.</summary>
public interface ICanonicalPlaceBinder
{
    /// <summary>Binds a code, alias, IATA, UN/LOCODE, or location id.</summary>
    Task<BoundPlace> BindAsync(string? raw, string fieldLabelVi, CancellationToken cancellationToken);

    /// <summary>Loads an active route and returns its canonical endpoints.</summary>
    Task<RouteMaster> RequireRouteAsync(Guid routeId, CancellationToken cancellationToken);
}

/// <summary>Tenant-scoped location and route resolution for Order and Bill.</summary>
public sealed class CanonicalPlaceBinder : ICanonicalPlaceBinder
{
    private readonly ILcmsDbContext _db;

    public CanonicalPlaceBinder(ILcmsDbContext db)
    {
        _db = db;
    }

    public async Task<BoundPlace> BindAsync(string? raw, string fieldLabelVi, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new BoundPlace(null, null);
        }

        var token = raw.Trim();
        var hasCatalog = await _db.Locations.AsNoTracking().AnyAsync(cancellationToken);
        if (Guid.TryParse(token, out var id))
        {
            var byId = await _db.Locations.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == id && l.IsActive, cancellationToken);
            if (byId is null)
            {
                throw new ConflictAppException($"{fieldLabelVi} không có trong danh mục địa điểm.");
            }

            return new BoundPlace(byId.Id, byId.Code);
        }

        var key = token.ToUpperInvariant();
        var match = await FindAsync(key, cancellationToken);
        if (match is not null)
        {
            return new BoundPlace(match.Id, match.Code);
        }

        if (hasCatalog)
        {
            throw new ConflictAppException(
                $"{fieldLabelVi} phải chọn từ danh mục địa điểm. Mã '{token}' không khớp mã, IATA, UN/LOCODE hay alias.");
        }

        return new BoundPlace(null, key);
    }

    public async Task<RouteMaster> RequireRouteAsync(Guid routeId, CancellationToken cancellationToken)
    {
        var route = await _db.Routes.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == routeId && r.IsActive, cancellationToken);
        if (route is null)
        {
            throw new ConflictAppException("Tuyến không có trong danh mục hoặc đã ngừng dùng.");
        }

        return route;
    }

    private async Task<Location?> FindAsync(string key, CancellationToken cancellationToken)
    {
        var direct = await _db.Locations.AsNoTracking()
            .Where(l => l.IsActive && (
                l.Code == key
                || l.IataCode == key
                || l.Unlocode == key))
            .ToListAsync(cancellationToken);
        if (direct.Count == 1)
        {
            return direct[0];
        }

        if (direct.Count > 1)
        {
            var exact = direct.FirstOrDefault(l => l.Code == key);
            if (exact is not null && direct.Count(l => l.Code == key) == 1)
            {
                return exact;
            }

            throw new ConflictAppException($"Mã địa điểm '{key}' khớp nhiều địa điểm. Chọn mã canonical.");
        }

        var alias = await _db.LocationAliases.AsNoTracking()
            .Where(a => a.AliasCode == key)
            .Select(a => a.LocationId)
            .ToListAsync(cancellationToken);
        if (alias.Count == 0)
        {
            return null;
        }

        if (alias.Count > 1)
        {
            throw new ConflictAppException($"Alias '{key}' khớp nhiều địa điểm.");
        }

        return await _db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == alias[0] && l.IsActive, cancellationToken);
    }
}
