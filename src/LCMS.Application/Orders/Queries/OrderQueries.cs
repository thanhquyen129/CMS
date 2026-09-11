using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
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

public sealed record ListOrdersQuery : IRequest<IReadOnlyList<OrderDto>>;

public sealed class ListOrdersQueryHandler : IRequestHandler<ListOrdersQuery, IReadOnlyList<OrderDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListOrdersQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<OrderDto>> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        return await _db.Orders
            .AsNoTracking()
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

public sealed record GetOrderByIdQuery(Guid Id) : IRequest<OrderDto>;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetOrderByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
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

        return new OrderDto(
            order.Id,
            order.TenantId,
            order.OrderNo,
            order.SourceSystem,
            order.ExternalId,
            order.ExternalVersion,
            order.OperationalStatus,
            order.IsActive,
            order.CreatedAt);
    }
}
