using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Roles.Queries;

public sealed record RoleDto(Guid Id, string Code, string Name, bool IsSystem, DateTimeOffset CreatedAt);

public sealed record GetRoleByIdQuery(Guid Id) : IRequest<RoleDto>;

public sealed class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetRoleByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<RoleDto> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var role = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role is null)
        {
            throw new NotFoundAppException("Không tìm thấy vai trò.");
        }

        return new RoleDto(role.Id, role.Code, role.Name, role.IsSystem, role.CreatedAt);
    }
}

public sealed record ListRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public sealed class ListRolesQueryHandler : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListRolesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        return await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Code)
            .Select(r => new RoleDto(r.Id, r.Code, r.Name, r.IsSystem, r.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed record RolePermissionDto(
    Guid Id,
    Guid RoleId,
    string ActionCode,
    string PermissionName,
    string DataScope);

public sealed record ListRolePermissionsQuery(Guid RoleId) : IRequest<IReadOnlyList<RolePermissionDto>>;

public sealed class ListRolePermissionsQueryHandler
    : IRequestHandler<ListRolePermissionsQuery, IReadOnlyList<RolePermissionDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListRolePermissionsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RolePermissionDto>> Handle(
        ListRolePermissionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var roleExists = await _db.Roles.AnyAsync(r => r.Id == request.RoleId, cancellationToken);
        if (!roleExists)
        {
            throw new NotFoundAppException("Không tìm thấy vai trò.");
        }

        return await (
            from rp in _db.RolePermissions.AsNoTracking()
            join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            where rp.RoleId == request.RoleId
            orderby p.ActionCode
            select new RolePermissionDto(rp.Id, rp.RoleId, p.ActionCode, p.Name, rp.DataScope)
        ).ToListAsync(cancellationToken);
    }
}
