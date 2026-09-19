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
    DateTimeOffset CreatedAt);

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
    IReadOnlyList<OperationalBillRef> RelatedBills);

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

    /// <summary>Lists tenant orders, optionally filtered by number / external id.</summary>
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
                || o.ExternalId.ToLower().Contains(pattern));
        }

        return await query
            .OrderBy(o => o.OrderNo)
            .Select(o => new OrderDto(
                o.Id,
                o.TenantId,
                o.OrderNo,
                o.SourceSystem,
                o.ExternalId,
                o.ExternalVersion,
                o.OperationalStatus,
                o.IsActive,
                o.CreatedAt))
            .ToListAsync(cancellationToken);
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
            related);
    }
}
