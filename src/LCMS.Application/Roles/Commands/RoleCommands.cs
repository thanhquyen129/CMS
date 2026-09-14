using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Roles.Commands;

public sealed record CreateRoleCommand(string Code, string Name) : IRequest<Guid>;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã vai trò không được để trống.")
            .MaximumLength(64).WithMessage("Mã vai trò không được vượt quá 64 ký tự.")
            .Matches(@"^[A-Za-z0-9_.-]+$").WithMessage("Mã vai trò chỉ gồm chữ, số, dấu chấm, gạch dưới hoặc gạch ngang.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên vai trò không được để trống.")
            .MaximumLength(256).WithMessage("Tên vai trò không được vượt quá 256 ký tự.");
    }
}

public sealed class CreateRoleCommandHandler : IRequestHandler<CreateRoleCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public CreateRoleCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RoleManage,
            "Bạn không có quyền quản lý vai trò.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var code = request.Code.Trim();

        if (await _db.Roles.AnyAsync(r => r.TenantId == tenantId && r.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã vai trò đã tồn tại trong thuê bao này.");
        }

        var role = new Role
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            IsSystem = false
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(cancellationToken);
        return role.Id;
    }
}

public sealed record AssignUserRoleCommand(Guid UserId, Guid RoleId) : IRequest;

public sealed class AssignUserRoleCommandValidator : AbstractValidator<AssignUserRoleCommand>
{
    public AssignUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Người dùng không hợp lệ.");
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("Vai trò không hợp lệ.");
    }
}

public sealed class AssignUserRoleCommandHandler : IRequestHandler<AssignUserRoleCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public AssignUserRoleCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.UserManage,
            "Bạn không có quyền gán vai trò cho người dùng.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundAppException("Không tìm thấy người dùng.");
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);
        if (role is null)
        {
            throw new NotFoundAppException("Không tìm thấy vai trò.");
        }

        var existing = await _db.UserRoles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                ur => ur.TenantId == tenantId && ur.UserId == request.UserId && ur.RoleId == request.RoleId,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.IsDeleted)
            {
                AccessAssignmentHelper.Restore(existing);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        _db.UserRoles.Add(new UserRole
        {
            TenantId = tenantId,
            UserId = request.UserId,
            RoleId = request.RoleId
        });
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record UnassignUserRoleCommand(Guid UserId, Guid RoleId) : IRequest;

public sealed class UnassignUserRoleCommandValidator : AbstractValidator<UnassignUserRoleCommand>
{
    public UnassignUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Người dùng không hợp lệ.");
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("Vai trò không hợp lệ.");
    }
}

public sealed class UnassignUserRoleCommandHandler : IRequestHandler<UnassignUserRoleCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _userContext;

    public UnassignUserRoleCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        ICurrentUserContext userContext)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _userContext = userContext;
    }

    public async Task Handle(UnassignUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.UserManage,
            "Bạn không có quyền gỡ vai trò của người dùng.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var row = await _db.UserRoles.FirstOrDefaultAsync(
            ur => ur.TenantId == tenantId && ur.UserId == request.UserId && ur.RoleId == request.RoleId,
            cancellationToken);

        if (row is null)
        {
            return;
        }

        row.SoftDelete(_userContext.UserId);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record AssignRolePermissionCommand(Guid RoleId, string ActionCode, string DataScope) : IRequest<Guid>;

public sealed class AssignRolePermissionCommandValidator : AbstractValidator<AssignRolePermissionCommand>
{
    public AssignRolePermissionCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("Vai trò không hợp lệ.");
        RuleFor(x => x.ActionCode)
            .NotEmpty().WithMessage("Mã quyền không được để trống.")
            .MaximumLength(128).WithMessage("Mã quyền không được vượt quá 128 ký tự.");
        RuleFor(x => x.DataScope)
            .NotEmpty().WithMessage("Phạm vi dữ liệu không được để trống.")
            .Must(DataScopes.IsValid)
            .WithMessage("Phạm vi dữ liệu phải là all, organization hoặc own.");
    }
}

public sealed class AssignRolePermissionCommandHandler : IRequestHandler<AssignRolePermissionCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public AssignRolePermissionCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(AssignRolePermissionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RoleManage,
            "Bạn không có quyền gán quyền cho vai trò.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var actionCode = request.ActionCode.Trim();
        var dataScope = request.DataScope.Trim().ToLowerInvariant();

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy vai trò.");

        await TenantAccessSeeder.EnsurePermissionCatalogAsync(_db, cancellationToken);

        var permission = await _db.Permissions.FirstOrDefaultAsync(p => p.ActionCode == actionCode, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy quyền hành động.");

        var existing = await _db.RolePermissions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                rp => rp.TenantId == tenantId && rp.RoleId == role.Id && rp.PermissionId == permission.Id,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.IsDeleted)
            {
                AccessAssignmentHelper.Restore(existing);
            }

            existing.DataScope = dataScope;
            await _db.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var row = new RolePermission
        {
            TenantId = tenantId,
            RoleId = role.Id,
            PermissionId = permission.Id,
            DataScope = dataScope
        };
        _db.RolePermissions.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}

/// <summary>Admin on/off: enable or disable an Action on a role (optional Data Scope when enabling).</summary>
public sealed record SetRolePermissionCommand(
    Guid RoleId,
    string ActionCode,
    bool Enabled,
    string? DataScope) : IRequest;

public sealed class SetRolePermissionCommandValidator : AbstractValidator<SetRolePermissionCommand>
{
    public SetRolePermissionCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("Vai trò không hợp lệ.");
        RuleFor(x => x.ActionCode)
            .NotEmpty().WithMessage("Mã quyền không được để trống.")
            .MaximumLength(128).WithMessage("Mã quyền không được vượt quá 128 ký tự.");
        RuleFor(x => x.DataScope)
            .Must(s => s is null || DataScopes.IsValid(s))
            .WithMessage("Phạm vi dữ liệu phải là all, organization hoặc own.");
    }
}

public sealed class SetRolePermissionCommandHandler : IRequestHandler<SetRolePermissionCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _userContext;

    public SetRolePermissionCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        ICurrentUserContext userContext)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _userContext = userContext;
    }

    public async Task Handle(SetRolePermissionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RoleManage,
            "Bạn không có quyền bật/tắt quyền của vai trò.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var actionCode = request.ActionCode.Trim();

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy vai trò.");

        if (role.Code == SystemRoleCatalog.Admin
            && !request.Enabled
            && (string.Equals(actionCode, PermissionCodes.UserManage, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionCode, PermissionCodes.RoleManage, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictAppException(
                "Không thể tắt user.manage hoặc role.manage trên vai trò Quản trị — thuê bao sẽ tự khóa.");
        }

        await TenantAccessSeeder.EnsurePermissionCatalogAsync(_db, cancellationToken);

        var permission = await _db.Permissions.FirstOrDefaultAsync(p => p.ActionCode == actionCode, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy quyền hành động.");

        var existing = await _db.RolePermissions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                rp => rp.TenantId == tenantId && rp.RoleId == role.Id && rp.PermissionId == permission.Id,
                cancellationToken);

        if (!request.Enabled)
        {
            if (existing is not null && !existing.IsDeleted)
            {
                existing.SoftDelete(_userContext.UserId);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var dataScope = string.IsNullOrWhiteSpace(request.DataScope)
            ? (existing is { IsDeleted: false } ? existing.DataScope : DataScopes.All)
            : request.DataScope.Trim().ToLowerInvariant();

        if (existing is not null)
        {
            if (existing.IsDeleted)
            {
                AccessAssignmentHelper.Restore(existing);
            }

            existing.DataScope = dataScope;
        }
        else
        {
            _db.RolePermissions.Add(new RolePermission
            {
                TenantId = tenantId,
                RoleId = role.Id,
                PermissionId = permission.Id,
                DataScope = dataScope
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

internal static class AccessAssignmentHelper
{
    public static void Restore(LCMS.Domain.Common.EntityBase entity)
    {
        entity.DeletedAt = null;
        entity.DeletedBy = null;
        entity.TouchRowVersion();
    }
}
