using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.TransportMovements.Queries;

public sealed record TransportMovementDto(
    Guid Id,
    Guid TenantId,
    string MovementNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record TransportMovementLegRef(Guid Id, string LegNo, Guid ShipmentId, string OperationalStatus);

public sealed record TransportMovementDetailDto(
    Guid Id,
    Guid TenantId,
    string MovementNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OperationalBillRef> RelatedBills,
    IReadOnlyList<TransportMovementLegRef> Legs);

public sealed record ListTransportMovementsQuery(string? Q = null)
    : IRequest<IReadOnlyList<TransportMovementDto>>;

/// <summary>Lists chuyến vận chuyển as financial references (not tracking).</summary>
public sealed class ListTransportMovementsQueryHandler
    : IRequestHandler<ListTransportMovementsQuery, IReadOnlyList<TransportMovementDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListTransportMovementsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<TransportMovementDto>> Handle(
        ListTransportMovementsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.TransportMovements.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var pattern = request.Q.Trim().ToLowerInvariant();
            query = query.Where(m =>
                m.MovementNo.ToLower().Contains(pattern)
                || m.ExternalId.ToLower().Contains(pattern));
        }

        return await query
            .OrderBy(m => m.MovementNo)
            .Select(m => new TransportMovementDto(
                m.Id,
                m.TenantId,
                m.MovementNo,
                m.SourceSystem,
                m.ExternalId,
                m.ExternalVersion,
                m.OperationalStatus,
                m.IsActive,
                m.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetTransportMovementByIdQuery(Guid Id) : IRequest<TransportMovementDetailDto>;

/// <summary>Chuyến DETAIL + Bills and chặng.</summary>
public sealed class GetTransportMovementByIdQueryHandler
    : IRequestHandler<GetTransportMovementByIdQuery, TransportMovementDetailDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetTransportMovementByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<TransportMovementDetailDto> Handle(
        GetTransportMovementByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var movement = await _db.TransportMovements
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chuyến vận chuyển.");

        var related = await (
            from l in _db.BillMovementLinks.AsNoTracking()
            join b in _db.Bills.AsNoTracking() on l.BillId equals b.Id
            where l.TransportMovementId == movement.Id
            orderby b.BillNo
            select new OperationalBillRef(b.Id, b.BillNo, b.OperationalStatus))
            .ToListAsync(cancellationToken);

        var legs = await (
            from lm in _db.LegMovementLinks.AsNoTracking()
            join l in _db.TransportLegs.AsNoTracking() on lm.TransportLegId equals l.Id
            where lm.TransportMovementId == movement.Id
            orderby l.LegNo
            select new TransportMovementLegRef(l.Id, l.LegNo, l.ShipmentId, l.OperationalStatus))
            .ToListAsync(cancellationToken);

        return new TransportMovementDetailDto(
            movement.Id,
            movement.TenantId,
            movement.MovementNo,
            movement.SourceSystem,
            movement.ExternalId,
            movement.ExternalVersion,
            movement.OperationalStatus,
            movement.IsActive,
            movement.CreatedAt,
            related,
            legs);
    }
}
