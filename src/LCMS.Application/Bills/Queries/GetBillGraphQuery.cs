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

/// <summary>Thin drill-down: Bill → linked orders/shipments (E03/E14).</summary>
public sealed record BillGraphDto(
    Guid BillId,
    string BillNo,
    string BillType,
    string OperationalStatus,
    IReadOnlyList<BillGraphOrderRef> Orders,
    IReadOnlyList<BillGraphShipmentRef> Shipments);

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

        return new BillGraphDto(
            bill.Id,
            bill.BillNo,
            bill.BillType,
            bill.OperationalStatus,
            orders,
            shipments);
    }
}
