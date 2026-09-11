using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Organizations.Queries;

public sealed record OrganizationDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record GetOrganizationByIdQuery(Guid Id) : IRequest<OrganizationDto>;

public sealed class GetOrganizationByIdQueryHandler : IRequestHandler<GetOrganizationByIdQuery, OrganizationDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetOrganizationByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<OrganizationDto> Handle(GetOrganizationByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var org = await _db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);

        if (org is null)
        {
            throw new NotFoundAppException("Không tìm thấy tổ chức.");
        }

        return new OrganizationDto(org.Id, org.Code, org.Name, org.ParentId, org.IsActive, org.CreatedAt);
    }
}

public sealed record ListOrganizationsQuery : IRequest<IReadOnlyList<OrganizationDto>>;

public sealed class ListOrganizationsQueryHandler : IRequestHandler<ListOrganizationsQuery, IReadOnlyList<OrganizationDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListOrganizationsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<OrganizationDto>> Handle(
        ListOrganizationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        return await _db.Organizations
            .AsNoTracking()
            .OrderBy(o => o.Code)
            .Select(o => new OrganizationDto(o.Id, o.Code, o.Name, o.ParentId, o.IsActive, o.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
