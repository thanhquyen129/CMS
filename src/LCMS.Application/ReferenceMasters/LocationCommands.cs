using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.ReferenceMasters;

public sealed record LocationAliasInput(string AliasCode, string? SourceSystem);

public sealed record UpsertLocationCommand(
    string Code,
    string Name,
    string LocationType,
    string? CountryCode,
    string? Subdivision,
    string? City,
    string? IataCode,
    string? Unlocode,
    string? TerminalCode,
    bool IsActive,
    IReadOnlyList<LocationAliasInput>? Aliases) : IRequest<Guid>;

public sealed class UpsertLocationCommandValidator : AbstractValidator<UpsertLocationCommand>
{
    public UpsertLocationCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.LocationType).Must(LocationTypes.IsKnown).WithMessage("Loại địa điểm không hợp lệ.");
        RuleFor(x => x.CountryCode).MaximumLength(2);
        RuleFor(x => x.IataCode).MaximumLength(3);
        RuleFor(x => x.Unlocode).MaximumLength(5);
        RuleFor(x => x.TerminalCode).MaximumLength(32);
    }
}

/// <summary>Creates or updates a location and its aliases by tenant code.</summary>
public sealed class UpsertLocationCommandHandler : IRequestHandler<UpsertLocationCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public UpsertLocationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        IPermissionService permissions,
        IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task<Guid> Handle(UpsertLocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(PermissionCodes.MasterCatalogManage, "Bạn không có quyền quản lý danh mục.", cancellationToken);
        var tenantId = _tenant.TenantId!.Value;
        var code = request.Code.Trim().ToUpperInvariant();
        var iata = Norm(request.IataCode);
        var unlocode = Norm(request.Unlocode);
        if (iata is not null && iata.Length != 3)
        {
            throw new ConflictAppException("Mã IATA phải đủ 3 ký tự.");
        }

        if (unlocode is not null && unlocode.Length != 5)
        {
            throw new ConflictAppException("UN/LOCODE phải đủ 5 ký tự.");
        }

        var row = await _db.Locations.FirstOrDefaultAsync(l => l.Code == code, cancellationToken);
        if (row is null)
        {
            row = new Location { TenantId = tenantId, Code = code };
            _db.Locations.Add(row);
        }

        row.Name = request.Name.Trim();
        row.LocationType = request.LocationType.Trim().ToLowerInvariant();
        row.CountryCode = Norm(request.CountryCode);
        row.Subdivision = Trim(request.Subdivision);
        row.City = Trim(request.City);
        row.IataCode = iata;
        row.Unlocode = unlocode;
        row.TerminalCode = Trim(request.TerminalCode);
        row.IsActive = request.IsActive;

        await ReplaceAliasesAsync(row, request.Aliases, tenantId, cancellationToken);
        _audit.Append(AuditActions.LocationUpsert, AuditObjectTypes.Location, row.Id, afterJson: code);
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    private async Task ReplaceAliasesAsync(
        Location location,
        IReadOnlyList<LocationAliasInput>? aliases,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var desired = (aliases ?? [])
            .Select(a => (Code: a.AliasCode.Trim().ToUpperInvariant(), Source: Trim(a.SourceSystem)))
            .Where(a => a.Code.Length > 0 && a.Code != location.Code)
            .DistinctBy(a => a.Code)
            .ToList();
        var existing = await _db.LocationAliases.Where(a => a.LocationId == location.Id).ToListAsync(cancellationToken);
        foreach (var gone in existing.Where(a => desired.All(d => d.Code != a.AliasCode)))
        {
            gone.SoftDelete(null);
        }

        foreach (var alias in desired)
        {
            var owner = await _db.LocationAliases.AsNoTracking()
                .FirstOrDefaultAsync(a => a.AliasCode == alias.Code, cancellationToken);
            if (owner is not null && owner.LocationId != location.Id)
            {
                throw new ConflictAppException($"Alias {alias.Code} đã gắn địa điểm khác.");
            }

            if (existing.Any(a => a.AliasCode == alias.Code && a.DeletedAt is null))
            {
                var current = existing.First(a => a.AliasCode == alias.Code && a.DeletedAt is null);
                current.SourceSystem = alias.Source;
                continue;
            }

            _db.LocationAliases.Add(new LocationAlias
            {
                TenantId = tenantId,
                LocationId = location.Id,
                AliasCode = alias.Code,
                SourceSystem = alias.Source
            });
        }
    }

    private static string? Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record LocationAliasDto(string AliasCode, string? SourceSystem);

public sealed record LocationDto(
    Guid Id,
    string Code,
    string Name,
    string LocationType,
    string? CountryCode,
    string? Subdivision,
    string? City,
    string? IataCode,
    string? Unlocode,
    string? TerminalCode,
    bool IsActive,
    IReadOnlyList<LocationAliasDto> Aliases);

public sealed record ListLocationsQuery(string? Q, string? LocationType, bool ActiveOnly) : IRequest<IReadOnlyList<LocationDto>>;

/// <summary>Lists canonical locations for admin and pickers.</summary>
public sealed class ListLocationsQueryHandler : IRequestHandler<ListLocationsQuery, IReadOnlyList<LocationDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListLocationsQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<LocationDto>> Handle(ListLocationsQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.Locations.AsNoTracking().AsQueryable();
        if (request.ActiveOnly)
        {
            query = query.Where(l => l.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.LocationType))
        {
            var type = request.LocationType.Trim().ToLowerInvariant();
            query = query.Where(l => l.LocationType == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var q = request.Q.Trim().ToUpperInvariant();
            var aliasIds = _db.LocationAliases.AsNoTracking()
                .Where(a => a.AliasCode.Contains(q))
                .Select(a => a.LocationId);
            query = query.Where(l =>
                l.Code.Contains(q)
                || l.Name.ToUpper().Contains(q)
                || (l.IataCode != null && l.IataCode.Contains(q))
                || (l.Unlocode != null && l.Unlocode.Contains(q))
                || aliasIds.Contains(l.Id));
        }

        var rows = await query.OrderBy(l => l.Code).Take(500).ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();
        var aliases = await _db.LocationAliases.AsNoTracking()
            .Where(a => ids.Contains(a.LocationId))
            .ToListAsync(cancellationToken);
        return rows.Select(l => Map(l, aliases.Where(a => a.LocationId == l.Id))).ToList();
    }

    internal static LocationDto Map(Location l, IEnumerable<LocationAlias> aliases) => new(
        l.Id,
        l.Code,
        l.Name,
        l.LocationType,
        l.CountryCode,
        l.Subdivision,
        l.City,
        l.IataCode,
        l.Unlocode,
        l.TerminalCode,
        l.IsActive,
        aliases.Select(a => new LocationAliasDto(a.AliasCode, a.SourceSystem)).ToList());
}

public sealed record ResolveLocationQuery(string Code) : IRequest<LocationDto>;

/// <summary>Resolves a canonical code, IATA, UN/LOCODE, or alias to one location.</summary>
public sealed class ResolveLocationQueryHandler : IRequestHandler<ResolveLocationQuery, LocationDto>
{
    private readonly ICanonicalPlaceBinder _binder;
    private readonly ILcmsDbContext _db;

    public ResolveLocationQueryHandler(ICanonicalPlaceBinder binder, ILcmsDbContext db)
    {
        _binder = binder;
        _db = db;
    }

    public async Task<LocationDto> Handle(ResolveLocationQuery request, CancellationToken cancellationToken)
    {
        var bound = await _binder.BindAsync(request.Code, "Địa điểm", cancellationToken);
        if (bound.LocationId is not Guid id)
        {
            throw new NotFoundAppException("Không tìm thấy địa điểm.");
        }

        var row = await _db.Locations.AsNoTracking().FirstAsync(l => l.Id == id, cancellationToken);
        var aliases = await _db.LocationAliases.AsNoTracking().Where(a => a.LocationId == id).ToListAsync(cancellationToken);
        return ListLocationsQueryHandler.Map(row, aliases);
    }
}
