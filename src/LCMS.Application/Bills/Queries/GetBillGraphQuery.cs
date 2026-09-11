using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

public sealed record BillGraphOrderRef(
    Guid Id,
    string OrderNo,
    string SourceSystem,
    string ExternalId,
    string OperationalStatus);

public sealed record BillGraphShipmentRef(
    Guid Id,
    string ShipmentNo,
    string SourceSystem,
    string ExternalId,
    string OperationalStatus);

public sealed record BillGraphLegRef(
    Guid Id,
    string LegNo,
    Guid ShipmentId,
    string SourceSystem,
    string ExternalId,
    string OperationalStatus);

public sealed record BillGraphMovementRef(
    Guid Id,
    string MovementNo,
    string SourceSystem,
    string ExternalId,
    string OperationalStatus);

/// <summary>Bill-centric operational graph: orders/shipments/legs/movements (E03).</summary>
public sealed record BillGraphDto(
    Guid BillId,
    string BillNo,
    string BillType,
    string OperationalStatus,
    IReadOnlyList<BillGraphOrderRef> Orders,
    IReadOnlyList<BillGraphShipmentRef> Shipments,
    IReadOnlyList<BillGraphLegRef> Legs,
    IReadOnlyList<BillGraphMovementRef> Movements);

public sealed record GetBillGraphQuery(Guid BillId) : IRequest<BillGraphDto>;

public sealed class GetBillGraphQueryHandler : IRequestHandler<GetBillGraphQuery, BillGraphDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetBillGraphQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BillGraphDto> Handle(GetBillGraphQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var bill = await _db.Bills
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken);

        if (bill is null)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        var orderIds = await _db.OrderBillLinks
            .AsNoTracking()
            .Where(l => l.BillId == request.BillId)
            .Select(l => l.OrderId)
            .ToListAsync(cancellationToken);

        var orders = await _db.Orders
            .AsNoTracking()
            .Where(o => orderIds.Contains(o.Id))
            .OrderBy(o => o.OrderNo)
            .Select(o => new BillGraphOrderRef(
                o.Id,
                o.OrderNo,
                o.SourceSystem,
                o.ExternalId,
                o.OperationalStatus))
            .ToListAsync(cancellationToken);

        var shipmentIds = await _db.BillShipmentLinks
            .AsNoTracking()
            .Where(l => l.BillId == request.BillId)
            .Select(l => l.ShipmentId)
            .ToListAsync(cancellationToken);

        var shipments = await _db.Shipments
            .AsNoTracking()
            .Where(s => shipmentIds.Contains(s.Id))
            .OrderBy(s => s.ShipmentNo)
            .Select(s => new BillGraphShipmentRef(
                s.Id,
                s.ShipmentNo,
                s.SourceSystem,
                s.ExternalId,
                s.OperationalStatus))
            .ToListAsync(cancellationToken);

        var linkedLegIds = await _db.BillLegLinks
            .AsNoTracking()
            .Where(l => l.BillId == request.BillId)
            .Select(l => l.TransportLegId)
            .ToListAsync(cancellationToken);

        var shipmentLegIds = await _db.TransportLegs
            .AsNoTracking()
            .Where(l => shipmentIds.Contains(l.ShipmentId))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        var legIdSet = linkedLegIds.Concat(shipmentLegIds).Distinct().ToList();

        var legs = await _db.TransportLegs
            .AsNoTracking()
            .Where(l => legIdSet.Contains(l.Id))
            .OrderBy(l => l.LegNo)
            .Select(l => new BillGraphLegRef(
                l.Id,
                l.LegNo,
                l.ShipmentId,
                l.SourceSystem,
                l.ExternalId,
                l.OperationalStatus))
            .ToListAsync(cancellationToken);

        var linkedMovementIds = await _db.BillMovementLinks
            .AsNoTracking()
            .Where(l => l.BillId == request.BillId)
            .Select(l => l.TransportMovementId)
            .ToListAsync(cancellationToken);

        var viaLegMovementIds = await _db.LegMovementLinks
            .AsNoTracking()
            .Where(l => legIdSet.Contains(l.TransportLegId))
            .Select(l => l.TransportMovementId)
            .ToListAsync(cancellationToken);

        var movementIdSet = linkedMovementIds.Concat(viaLegMovementIds).Distinct().ToList();

        var movements = await _db.TransportMovements
            .AsNoTracking()
            .Where(m => movementIdSet.Contains(m.Id))
            .OrderBy(m => m.MovementNo)
            .Select(m => new BillGraphMovementRef(
                m.Id,
                m.MovementNo,
                m.SourceSystem,
                m.ExternalId,
                m.OperationalStatus))
            .ToListAsync(cancellationToken);

        return new BillGraphDto(
            bill.Id,
            bill.BillNo,
            bill.BillType,
            bill.OperationalStatus,
            orders,
            shipments,
            legs,
            movements);
    }
}
