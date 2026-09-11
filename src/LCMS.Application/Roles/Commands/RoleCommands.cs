using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
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

    public CreateRoleCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

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

    public AssignUserRoleCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

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

        var exists = await _db.UserRoles.AnyAsync(
            ur => ur.TenantId == tenantId && ur.UserId == request.UserId && ur.RoleId == request.RoleId,
            cancellationToken);
        if (exists)
        {
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

    public AssignRolePermissionCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(AssignRolePermissionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var actionCode = request.ActionCode.Trim();
        var dataScope = request.DataScope.Trim().ToLowerInvariant();

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy vai trò.");

        await TenantAccessSeeder.EnsurePermissionCatalogAsync(_db, cancellationToken);

        var permission = await _db.Permissions.FirstOrDefaultAsync(p => p.ActionCode == actionCode, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy quyền hành động.");

        var existing = await _db.RolePermissions.FirstOrDefaultAsync(
            rp => rp.TenantId == tenantId && rp.RoleId == role.Id && rp.PermissionId == permission.Id,
            cancellationToken);

        if (existing is not null)
        {
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
