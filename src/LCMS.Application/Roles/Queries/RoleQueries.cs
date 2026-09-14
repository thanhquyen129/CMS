using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Roles.Queries;

public sealed record RoleDto(
    Guid Id,
    string Code,
    string Name,
    bool IsSystem,
    string? SummaryVi,
    DateTimeOffset CreatedAt);

public sealed record GetRoleByIdQuery(Guid Id) : IRequest<RoleDto>;

public sealed class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public GetRoleByIdQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<RoleDto> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RoleManage,
            "Bạn không có quyền xem vai trò.",
            cancellationToken);

        var role = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (role is null)
        {
            throw new NotFoundAppException("Không tìm thấy vai trò.");
        }

        return ToDto(role.Id, role.Code, role.Name, role.IsSystem, role.CreatedAt);
    }

    internal static RoleDto ToDto(Guid id, string code, string name, bool isSystem, DateTimeOffset createdAt)
    {
        var summary = SystemRoleCatalog.All
            .FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase))
            ?.SummaryVi;
        return new RoleDto(id, code, name, isSystem, summary, createdAt);
    }
}

public sealed record ListRolesQuery : IRequest<IReadOnlyList<RoleDto>>;

public sealed class ListRolesQueryHandler : IRequestHandler<ListRolesQuery, IReadOnlyList<RoleDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ListRolesQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<RoleDto>> Handle(ListRolesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RoleManage,
            "Bạn không có quyền xem danh sách vai trò.",
            cancellationToken);

        var rows = await _db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Code)
            .Select(r => new { r.Id, r.Code, r.Name, r.IsSystem, r.CreatedAt })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => GetRoleByIdQueryHandler.ToDto(r.Id, r.Code, r.Name, r.IsSystem, r.CreatedAt))
            .ToList();
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
    private readonly IPermissionService _permissions;

    public ListRolePermissionsQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<RolePermissionDto>> Handle(
        ListRolePermissionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RoleManage,
            "Bạn không có quyền xem quyền của vai trò.",
            cancellationToken);

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

public sealed record RolePermissionMatrixItemDto(
    string ActionCode,
    string PermissionName,
    bool Enabled,
    string? DataScope,
    bool Locked);

public sealed record GetRolePermissionMatrixQuery(Guid RoleId)
    : IRequest<IReadOnlyList<RolePermissionMatrixItemDto>>;

public sealed class GetRolePermissionMatrixQueryHandler
    : IRequestHandler<GetRolePermissionMatrixQuery, IReadOnlyList<RolePermissionMatrixItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public GetRolePermissionMatrixQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<RolePermissionMatrixItemDto>> Handle(
        GetRolePermissionMatrixQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RoleManage,
            "Bạn không có quyền xem ma trận quyền.",
            cancellationToken);

        var role = await _db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy vai trò.");

        await TenantAccessSeeder.EnsurePermissionCatalogAsync(_db, cancellationToken);

        var granted = await (
            from rp in _db.RolePermissions.AsNoTracking()
            join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            where rp.RoleId == request.RoleId
            select new { p.ActionCode, rp.DataScope }
        ).ToDictionaryAsync(x => x.ActionCode, x => x.DataScope, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var isAdmin = string.Equals(role.Code, SystemRoleCatalog.Admin, StringComparison.OrdinalIgnoreCase);

        return PermissionCodes.CoreCatalog
            .Select(c =>
            {
                var enabled = granted.TryGetValue(c.Code, out var scope);
                var locked = isAdmin
                    && (c.Code is PermissionCodes.UserManage or PermissionCodes.RoleManage);
                return new RolePermissionMatrixItemDto(
                    c.Code,
                    c.Name,
                    enabled,
                    enabled ? scope : null,
                    locked);
            })
            .ToList();
    }
}

public sealed record PermissionCatalogItemDto(string ActionCode, string Name);

public sealed record ListPermissionCatalogQuery : IRequest<IReadOnlyList<PermissionCatalogItemDto>>;

public sealed class ListPermissionCatalogQueryHandler
    : IRequestHandler<ListPermissionCatalogQuery, IReadOnlyList<PermissionCatalogItemDto>>
{
    private readonly IPermissionService _permissions;

    public ListPermissionCatalogQueryHandler(IPermissionService permissions)
    {
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<PermissionCatalogItemDto>> Handle(
        ListPermissionCatalogQuery request,
        CancellationToken cancellationToken)
    {
        await _permissions.EnsureAsync(
            PermissionCodes.RoleManage,
            "Bạn không có quyền xem danh mục quyền.",
            cancellationToken);

        return PermissionCodes.CoreCatalog
            .Select(c => new PermissionCatalogItemDto(c.Code, c.Name))
            .ToList();
    }
}
