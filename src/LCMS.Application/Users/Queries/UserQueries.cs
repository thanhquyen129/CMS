using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Users.Queries;

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsActive,
    Guid? OrganizationId,
    DateTimeOffset CreatedAt);

public sealed record GetUserByIdQuery(Guid Id) : IRequest<UserDto>;

public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public GetUserByIdQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.UserManage,
            "Bạn không có quyền xem người dùng.",
            cancellationToken);

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        if (user is null)
        {
            throw new NotFoundAppException("Không tìm thấy người dùng.");
        }

        return new UserDto(user.Id, user.Email, user.DisplayName, user.IsActive, user.OrganizationId, user.CreatedAt);
    }
}

public sealed record ListUsersQuery : IRequest<IReadOnlyList<UserDto>>;

public sealed class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ListUsersQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<UserDto>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.UserManage,
            "Bạn không có quyền xem danh sách người dùng.",
            cancellationToken);

        return await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new UserDto(u.Id, u.Email, u.DisplayName, u.IsActive, u.OrganizationId, u.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed record UserRoleDto(Guid RoleId, string Code, string Name, bool IsSystem);

public sealed record ListUserRolesQuery(Guid UserId) : IRequest<IReadOnlyList<UserRoleDto>>;

public sealed class ListUserRolesQueryHandler : IRequestHandler<ListUserRolesQuery, IReadOnlyList<UserRoleDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ListUserRolesQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<UserRoleDto>> Handle(
        ListUserRolesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.UserManage,
            "Bạn không có quyền xem vai trò người dùng.",
            cancellationToken);

        var userExists = await _db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundAppException("Không tìm thấy người dùng.");
        }

        return await (
            from ur in _db.UserRoles.AsNoTracking()
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where ur.UserId == request.UserId
            orderby r.Code
            select new UserRoleDto(r.Id, r.Code, r.Name, r.IsSystem)
        ).ToListAsync(cancellationToken);
    }
}
