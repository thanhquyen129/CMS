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
    DateTimeOffset CreatedAt);

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
    IReadOnlyList<ShipmentLegRef> Legs);

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
                || s.ExternalId.ToLower().Contains(pattern));
        }

        return await query
            .OrderBy(s => s.ShipmentNo)
            .Select(s => new ShipmentDto(
                s.Id,
                s.TenantId,
                s.ShipmentNo,
                s.SourceSystem,
                s.ExternalId,
                s.ExternalVersion,
                s.OperationalStatus,
                s.IsActive,
                s.CreatedAt))
            .ToListAsync(cancellationToken);
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
            legs);
    }
}
