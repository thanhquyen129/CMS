using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Orders.Queries;

public sealed record OrderDto(
    Guid Id,
    Guid TenantId,
    string OrderNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt,
    Guid? CustomerPartyId = null,
    string? CustomerName = null,
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
    OperationalContextDocument? Context = null);

public sealed record OrderDetailDto(
    Guid Id,
    Guid TenantId,
    string OrderNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OperationalBillRef> RelatedBills,
    Guid? CustomerPartyId = null,
    string? CustomerName = null,
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

public sealed record ListOrdersQuery(string? Q = null) : IRequest<IReadOnlyList<OrderDto>>;

public sealed class ListOrdersQueryHandler : IRequestHandler<ListOrdersQuery, IReadOnlyList<OrderDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListOrdersQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    /// <summary>Lists tenant orders, optionally filtered by number / external id / customer / route.</summary>
    public async Task<IReadOnlyList<OrderDto>> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.Orders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var pattern = request.Q.Trim().ToLowerInvariant();
            query = query.Where(o =>
                o.OrderNo.ToLower().Contains(pattern)
                || o.ExternalId.ToLower().Contains(pattern)
                || (o.CustomerReference != null && o.CustomerReference.ToLower().Contains(pattern))
                || (o.RouteCode != null && o.RouteCode.ToLower().Contains(pattern)));
        }

        // ORDER BY DateTimeOffset is rejected by SQLite tests; Postgres accepts in-memory sort equally.
        var rows = (await query.ToListAsync(cancellationToken))
            .OrderByDescending(o => o.CreatedAt)
            .ThenBy(o => o.OrderNo)
            .ToList();

        var partyIds = rows
            .Select(o => o.CustomerPartyId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        Dictionary<Guid, string> names = [];
        if (partyIds.Count > 0)
        {
            var parties = await _db.BusinessParties.AsNoTracking()
                .Where(p => partyIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(cancellationToken);
            names = parties.ToDictionary(p => p.Id, p => p.Name);
        }

        var orderIds = rows.Select(o => o.Id).ToList();
        Dictionary<Guid, int> counts = [];
        if (orderIds.Count > 0)
        {
            var links = await _db.OrderBillLinks.AsNoTracking()
                .Where(l => orderIds.Contains(l.OrderId))
                .Select(l => l.OrderId)
                .ToListAsync(cancellationToken);
            counts = links.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        }

        return rows.Select(o => new OrderDto(
            o.Id,
            o.TenantId,
            o.OrderNo,
            o.SourceSystem,
            o.ExternalId,
            o.ExternalVersion,
            o.OperationalStatus,
            o.IsActive,
            o.CreatedAt,
            o.CustomerPartyId,
            o.CustomerPartyId is Guid pid && names.TryGetValue(pid, out var n) ? n : null,
            o.AssignedUserId,
            o.TransportMode,
            o.OriginCode,
            o.DestinationCode,
            o.RouteCode,
            o.EtdAt,
            o.EtaAt,
            o.CustomerReference,
            o.Description,
            counts.GetValueOrDefault(o.Id),
            OperationalContextJson.Deserialize(o.ContextJson))).ToList();
    }
}

public sealed record GetOrderByIdQuery(Guid Id) : IRequest<OrderDetailDto>;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDetailDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetOrderByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    /// <summary>Returns order plus linked Bills for CROSS-NAV.</summary>
    public async Task<OrderDetailDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);

        if (order is null)
        {
            throw new NotFoundAppException("Không tìm thấy đơn hàng.");
        }

        var related = await (
            from l in _db.OrderBillLinks.AsNoTracking()
            join b in _db.Bills.AsNoTracking() on l.BillId equals b.Id
            where l.OrderId == order.Id
            orderby b.BillNo
            select new OperationalBillRef(b.Id, b.BillNo, b.OperationalStatus))
            .ToListAsync(cancellationToken);

        string? customerName = null;
        if (order.CustomerPartyId is Guid pid)
        {
            customerName = await _db.BusinessParties.AsNoTracking()
                .Where(p => p.Id == pid)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        string? assignedName = null;
        if (order.AssignedUserId is Guid uid)
        {
            assignedName = await _db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => u.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new OrderDetailDto(
            order.Id,
            order.TenantId,
            order.OrderNo,
            order.SourceSystem,
            order.ExternalId,
            order.ExternalVersion,
            order.OperationalStatus,
            order.IsActive,
            order.CreatedAt,
            related,
            order.CustomerPartyId,
            customerName,
            order.AssignedUserId,
            assignedName,
            order.TransportMode,
            order.OriginCode,
            order.DestinationCode,
            order.RouteCode,
            order.EtdAt,
            order.EtaAt,
            order.CustomerReference,
            order.Description,
            OperationalContextJson.Deserialize(order.ContextJson));
    }
}
