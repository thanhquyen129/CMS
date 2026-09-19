using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.TransportLegs.Queries;

public sealed record TransportLegDto(
    Guid Id,
    Guid TenantId,
    Guid ShipmentId,
    string LegNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record TransportLegMovementRef(Guid Id, string MovementNo, string OperationalStatus);

public sealed record TransportLegDetailDto(
    Guid Id,
    Guid TenantId,
    Guid ShipmentId,
    string? ShipmentNo,
    string LegNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OperationalBillRef> RelatedBills,
    IReadOnlyList<TransportLegMovementRef> Movements);

public sealed record ListTransportLegsQuery(string? Q = null, Guid? ShipmentId = null)
    : IRequest<IReadOnlyList<TransportLegDto>>;

/// <summary>Lists chặng vận chuyển (reference only — not dispatch).</summary>
public sealed class ListTransportLegsQueryHandler
    : IRequestHandler<ListTransportLegsQuery, IReadOnlyList<TransportLegDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListTransportLegsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<TransportLegDto>> Handle(
        ListTransportLegsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.TransportLegs.AsNoTracking().AsQueryable();
        if (request.ShipmentId.HasValue)
        {
            query = query.Where(l => l.ShipmentId == request.ShipmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var pattern = request.Q.Trim().ToLowerInvariant();
            query = query.Where(l =>
                l.LegNo.ToLower().Contains(pattern)
                || l.ExternalId.ToLower().Contains(pattern));
        }

        return await query
            .OrderBy(l => l.LegNo)
            .Select(l => new TransportLegDto(
                l.Id,
                l.TenantId,
                l.ShipmentId,
                l.LegNo,
                l.SourceSystem,
                l.ExternalId,
                l.ExternalVersion,
                l.OperationalStatus,
                l.IsActive,
                l.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetTransportLegByIdQuery(Guid Id) : IRequest<TransportLegDetailDto>;

/// <summary>Chặng DETAIL + shipment, Bills, chuyến.</summary>
public sealed class GetTransportLegByIdQueryHandler
    : IRequestHandler<GetTransportLegByIdQuery, TransportLegDetailDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetTransportLegByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<TransportLegDetailDto> Handle(
        GetTransportLegByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var leg = await _db.TransportLegs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chặng vận chuyển.");

        var shipmentNo = await _db.Shipments.AsNoTracking()
            .Where(s => s.Id == leg.ShipmentId)
            .Select(s => s.ShipmentNo)
            .FirstOrDefaultAsync(cancellationToken);

        var related = await (
            from l in _db.BillLegLinks.AsNoTracking()
            join b in _db.Bills.AsNoTracking() on l.BillId equals b.Id
            where l.TransportLegId == leg.Id
            orderby b.BillNo
            select new OperationalBillRef(b.Id, b.BillNo, b.OperationalStatus))
            .ToListAsync(cancellationToken);

        var movements = await (
            from lm in _db.LegMovementLinks.AsNoTracking()
            join m in _db.TransportMovements.AsNoTracking() on lm.TransportMovementId equals m.Id
            where lm.TransportLegId == leg.Id
            orderby m.MovementNo
            select new TransportLegMovementRef(m.Id, m.MovementNo, m.OperationalStatus))
            .ToListAsync(cancellationToken);

        return new TransportLegDetailDto(
            leg.Id,
            leg.TenantId,
            leg.ShipmentId,
            shipmentNo,
            leg.LegNo,
            leg.SourceSystem,
            leg.ExternalId,
            leg.ExternalVersion,
            leg.OperationalStatus,
            leg.IsActive,
            leg.CreatedAt,
            related,
            movements);
    }
}
