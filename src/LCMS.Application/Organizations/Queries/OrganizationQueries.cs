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

public sealed record OrganizationTreeNodeDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId,
    bool IsActive,
    IReadOnlyList<OrganizationTreeNodeDto> Children);

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

public sealed record ListOrganizationChildrenQuery(Guid ParentId) : IRequest<IReadOnlyList<OrganizationDto>>;

public sealed class ListOrganizationChildrenQueryHandler
    : IRequestHandler<ListOrganizationChildrenQuery, IReadOnlyList<OrganizationDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListOrganizationChildrenQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<OrganizationDto>> Handle(
        ListOrganizationChildrenQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var parentExists = await _db.Organizations.AnyAsync(o => o.Id == request.ParentId, cancellationToken);
        if (!parentExists)
        {
            throw new NotFoundAppException("Không tìm thấy tổ chức.");
        }

        return await _db.Organizations.AsNoTracking()
            .Where(o => o.ParentId == request.ParentId)
            .OrderBy(o => o.Code)
            .Select(o => new OrganizationDto(o.Id, o.Code, o.Name, o.ParentId, o.IsActive, o.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetOrganizationTreeQuery : IRequest<IReadOnlyList<OrganizationTreeNodeDto>>;

public sealed class GetOrganizationTreeQueryHandler
    : IRequestHandler<GetOrganizationTreeQuery, IReadOnlyList<OrganizationTreeNodeDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetOrganizationTreeQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<OrganizationTreeNodeDto>> Handle(
        GetOrganizationTreeQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var flat = await _db.Organizations.AsNoTracking()
            .OrderBy(o => o.Code)
            .Select(o => new { o.Id, o.Code, o.Name, o.ParentId, o.IsActive })
            .ToListAsync(cancellationToken);

        var byParent = flat
            .GroupBy(o => o.ParentId)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.ToList());

        OrganizationTreeNodeDto Build(Guid id, string code, string name, Guid? parentId, bool isActive)
        {
            byParent.TryGetValue(id, out var kids);
            kids ??= [];
            return new OrganizationTreeNodeDto(
                id,
                code,
                name,
                parentId,
                isActive,
                kids.Select(k => Build(k.Id, k.Code, k.Name, k.ParentId, k.IsActive)).ToList());
        }

        byParent.TryGetValue(Guid.Empty, out var roots);
        roots ??= [];
        return roots.Select(r => Build(r.Id, r.Code, r.Name, r.ParentId, r.IsActive)).ToList();
    }
}
