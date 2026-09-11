using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Tenants.Queries;

public sealed record TenantDto(Guid Id, string Code, string Name, bool IsActive, DateTimeOffset CreatedAt);

public sealed record GetTenantByIdQuery(Guid Id) : IRequest<TenantDto>;

public sealed class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, TenantDto>
{
    private readonly ILcmsDbContext _db;

    public GetTenantByIdQueryHandler(ILcmsDbContext db)
    {
        _db = db;
    }

    public async Task<TenantDto> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (tenant is null)
        {
            throw new NotFoundAppException("Không tìm thấy thuê bao.");
        }

        return new TenantDto(tenant.Id, tenant.Code, tenant.Name, tenant.IsActive, tenant.CreatedAt);
    }
}
