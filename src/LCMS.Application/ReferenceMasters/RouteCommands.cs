using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.ReferenceMasters;

public sealed record UpsertRouteCommand(
    string Code,
    string Name,
    Guid OriginLocationId,
    Guid DestinationLocationId,
    string? TransportModeCode,
    string? ServiceTypeCode,
    bool IsActive,
    IReadOnlyList<Guid>? IntermediateLocationIds) : IRequest<Guid>;

public sealed class UpsertRouteCommandValidator : AbstractValidator<UpsertRouteCommand>
{
    public UpsertRouteCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.OriginLocationId).NotEmpty();
        RuleFor(x => x.DestinationLocationId).NotEmpty();
    }
}

/// <summary>Creates or updates a route. Origin and destination must be distinct active locations.</summary>
public sealed class UpsertRouteCommandHandler : IRequestHandler<UpsertRouteCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public UpsertRouteCommandHandler(
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

    public async Task<Guid> Handle(UpsertRouteCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(PermissionCodes.MasterCatalogManage, "Bạn không có quyền quản lý danh mục.", cancellationToken);
        if (request.OriginLocationId == request.DestinationLocationId)
        {
            throw new ConflictAppException("Điểm đi và điểm đến của tuyến phải khác nhau.");
        }

        var stops = (request.IntermediateLocationIds ?? []).Where(id => id != Guid.Empty).Distinct().ToList();
        if (stops.Contains(request.OriginLocationId) || stops.Contains(request.DestinationLocationId))
        {
            throw new ConflictAppException("Điểm trung gian không được trùng điểm đi hoặc điểm đến.");
        }

        var needed = stops.Append(request.OriginLocationId).Append(request.DestinationLocationId).Distinct().ToList();
        var found = await _db.Locations.AsNoTracking()
            .Where(l => needed.Contains(l.Id) && l.IsActive)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);
        if (found.Count != needed.Count)
        {
            throw new ConflictAppException("Tuyến chỉ được gắn địa điểm đang dùng.");
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var row = await _db.Routes.FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (row is null)
        {
            row = new RouteMaster { TenantId = _tenant.TenantId!.Value, Code = code };
            _db.Routes.Add(row);
        }

        row.Name = request.Name.Trim();
        row.OriginLocationId = request.OriginLocationId;
        row.DestinationLocationId = request.DestinationLocationId;
        row.TransportModeCode = string.IsNullOrWhiteSpace(request.TransportModeCode) ? null : request.TransportModeCode.Trim().ToLowerInvariant();
        row.ServiceTypeCode = string.IsNullOrWhiteSpace(request.ServiceTypeCode) ? null : request.ServiceTypeCode.Trim();
        row.IsActive = request.IsActive;

        var existing = await _db.RouteStops.Where(s => s.RouteId == row.Id).ToListAsync(cancellationToken);
        foreach (var stop in existing)
        {
            stop.SoftDelete(null);
        }

        var seq = 1;
        foreach (var locationId in stops)
        {
            _db.RouteStops.Add(new RouteStop
            {
                TenantId = _tenant.TenantId!.Value,
                RouteId = row.Id,
                SequenceNo = seq++,
                LocationId = locationId
            });
        }

        _audit.Append(AuditActions.RouteUpsert, AuditObjectTypes.Route, row.Id, afterJson: code);
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}

public sealed record RouteStopDto(int SequenceNo, Guid LocationId, string LocationCode);

public sealed record RouteDto(
    Guid Id,
    string Code,
    string Name,
    Guid OriginLocationId,
    string OriginCode,
    Guid DestinationLocationId,
    string DestinationCode,
    string? TransportModeCode,
    string? ServiceTypeCode,
    bool IsActive,
    IReadOnlyList<RouteStopDto> Stops);

public sealed record ListRoutesQuery(bool ActiveOnly) : IRequest<IReadOnlyList<RouteDto>>;

/// <summary>Lists routes with endpoint codes and intermediate stops.</summary>
public sealed class ListRoutesQueryHandler : IRequestHandler<ListRoutesQuery, IReadOnlyList<RouteDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListRoutesQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<RouteDto>> Handle(ListRoutesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var routes = _db.Routes.AsNoTracking();
        if (request.ActiveOnly)
        {
            routes = routes.Where(r => r.IsActive);
        }

        var rows = await routes.OrderBy(r => r.Code).Take(500).ToListAsync(cancellationToken);
        var routeIds = rows.Select(r => r.Id).ToList();
        var locationIds = rows.Select(r => r.OriginLocationId).Concat(rows.Select(r => r.DestinationLocationId)).Distinct().ToList();
        var stops = await _db.RouteStops.AsNoTracking().Where(s => routeIds.Contains(s.RouteId)).ToListAsync(cancellationToken);
        locationIds.AddRange(stops.Select(s => s.LocationId));
        var codes = await _db.Locations.AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Code })
            .ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);

        return rows.Select(r => new RouteDto(
            r.Id,
            r.Code,
            r.Name,
            r.OriginLocationId,
            codes.GetValueOrDefault(r.OriginLocationId, ""),
            r.DestinationLocationId,
            codes.GetValueOrDefault(r.DestinationLocationId, ""),
            r.TransportModeCode,
            r.ServiceTypeCode,
            r.IsActive,
            stops.Where(s => s.RouteId == r.Id)
                .OrderBy(s => s.SequenceNo)
                .Select(s => new RouteStopDto(s.SequenceNo, s.LocationId, codes.GetValueOrDefault(s.LocationId, "")))
                .ToList())).ToList();
    }
}
