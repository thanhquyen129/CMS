using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Shipments.Queries;

public sealed record ShipmentDto(
    Guid Id,
    Guid TenantId,
    string ShipmentNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt,
    Guid? AssignedUserId = null,
    string? TransportMode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? RouteCode = null,
    DateTimeOffset? EtdAt = null,
    DateTimeOffset? EtaAt = null,
    string? CustomerReference = null,
    string? Description = null,
    int RelatedBillCount = 0,
    int LegCount = 0,
    OperationalContextDocument? Context = null);

public sealed record ShipmentLegRef(Guid Id, string LegNo, string OperationalStatus);

public sealed record ShipmentDetailDto(
    Guid Id,
    Guid TenantId,
    string ShipmentNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OperationalBillRef> RelatedBills,
    IReadOnlyList<ShipmentLegRef> Legs,
    Guid? AssignedUserId = null,
    string? AssignedUserName = null,
    string? TransportMode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? RouteCode = null,
    DateTimeOffset? EtdAt = null,
    DateTimeOffset? EtaAt = null,
    string? CustomerReference = null,
    string? Description = null,
    OperationalContextDocument? Context = null);

public sealed record ListShipmentsQuery(string? Q = null) : IRequest<IReadOnlyList<ShipmentDto>>;

/// <summary>Lists tenant shipments for Manual Reference Entry workspace.</summary>
public sealed class ListShipmentsQueryHandler : IRequestHandler<ListShipmentsQuery, IReadOnlyList<ShipmentDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListShipmentsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ShipmentDto>> Handle(
        ListShipmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.Shipments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var pattern = request.Q.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.ShipmentNo.ToLower().Contains(pattern)
                || s.ExternalId.ToLower().Contains(pattern)
                || (s.CustomerReference != null && s.CustomerReference.ToLower().Contains(pattern))
                || (s.RouteCode != null && s.RouteCode.ToLower().Contains(pattern)));
        }

        // ORDER BY DateTimeOffset is rejected by SQLite tests; Postgres accepts in-memory sort equally.
        var rows = (await query.ToListAsync(cancellationToken))
            .OrderByDescending(s => s.CreatedAt)
            .ThenBy(s => s.ShipmentNo)
            .ToList();

        var ids = rows.Select(s => s.Id).ToList();
        Dictionary<Guid, int> billCounts = [];
        Dictionary<Guid, int> legCounts = [];
        if (ids.Count > 0)
        {
            var billLinks = await _db.BillShipmentLinks.AsNoTracking()
                .Where(l => ids.Contains(l.ShipmentId))
                .Select(l => l.ShipmentId)
                .ToListAsync(cancellationToken);
            billCounts = billLinks.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());

            var legs = await _db.TransportLegs.AsNoTracking()
                .Where(l => ids.Contains(l.ShipmentId))
                .Select(l => l.ShipmentId)
                .ToListAsync(cancellationToken);
            legCounts = legs.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        }

        return rows.Select(s => new ShipmentDto(
            s.Id,
            s.TenantId,
            s.ShipmentNo,
            s.SourceSystem,
            s.ExternalId,
            s.ExternalVersion,
            s.OperationalStatus,
            s.IsActive,
            s.CreatedAt,
            s.AssignedUserId,
            s.TransportMode,
            s.OriginCode,
            s.DestinationCode,
            s.RouteCode,
            s.EtdAt,
            s.EtaAt,
            s.CustomerReference,
            s.Description,
            billCounts.GetValueOrDefault(s.Id),
            legCounts.GetValueOrDefault(s.Id),
            OperationalContextJson.Deserialize(s.ContextJson))).ToList();
    }
}

public sealed record GetShipmentByIdQuery(Guid Id) : IRequest<ShipmentDetailDto>;

/// <summary>Shipment DETAIL + related Bills and legs (AC-SCP-06).</summary>
public sealed class GetShipmentByIdQueryHandler : IRequestHandler<GetShipmentByIdQuery, ShipmentDetailDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetShipmentByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ShipmentDetailDto> Handle(GetShipmentByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var shipment = await _db.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lô hàng.");

        var related = await (
            from l in _db.BillShipmentLinks.AsNoTracking()
            join b in _db.Bills.AsNoTracking() on l.BillId equals b.Id
            where l.ShipmentId == shipment.Id
            orderby b.BillNo
            select new OperationalBillRef(b.Id, b.BillNo, b.OperationalStatus))
            .ToListAsync(cancellationToken);

        var legs = await _db.TransportLegs.AsNoTracking()
            .Where(l => l.ShipmentId == shipment.Id)
            .OrderBy(l => l.LegNo)
            .Select(l => new ShipmentLegRef(l.Id, l.LegNo, l.OperationalStatus))
            .ToListAsync(cancellationToken);

        string? assignedName = null;
        if (shipment.AssignedUserId is Guid uid)
        {
            assignedName = await _db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => u.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new ShipmentDetailDto(
            shipment.Id,
            shipment.TenantId,
            shipment.ShipmentNo,
            shipment.SourceSystem,
            shipment.ExternalId,
            shipment.ExternalVersion,
            shipment.OperationalStatus,
            shipment.IsActive,
            shipment.CreatedAt,
            related,
            legs,
            shipment.AssignedUserId,
            assignedName,
            shipment.TransportMode,
            shipment.OriginCode,
            shipment.DestinationCode,
            shipment.RouteCode,
            shipment.EtdAt,
            shipment.EtaAt,
            shipment.CustomerReference,
            shipment.Description,
            OperationalContextJson.Deserialize(shipment.ContextJson));
    }
}
