using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LCMS.Application.Users.Commands;

public sealed record CreateUserCommand(
    string Email,
    string DisplayName,
    Guid? OrganizationId,
    string? Password = null) : IRequest<Guid>;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .MaximumLength(320).WithMessage("Email không được vượt quá 320 ký tự.")
            .EmailAddress().WithMessage("Email không hợp lệ.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Tên hiển thị không được để trống.")
            .MaximumLength(256).WithMessage("Tên hiển thị không được vượt quá 256 ký tự.");

        RuleFor(x => x.Password)
            .Must(p => p is null || PasswordRules.Validate(p) is null)
            .WithMessage(x => PasswordRules.Validate(x.Password) ?? "Mật khẩu không hợp lệ.");
    }
}

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditWriter _audit;

    public CreateUserCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IPasswordHasher passwordHasher,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _passwordHasher = passwordHasher;
        _audit = audit;
    }

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.UserManage,
            "Bạn không có quyền tạo người dùng.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var email = request.Email.Trim().ToLowerInvariant();

        if (request.OrganizationId is Guid orgId)
        {
            var orgExists = await _db.Organizations.AnyAsync(o => o.Id == orgId, cancellationToken);
            if (!orgExists)
            {
                throw new NotFoundAppException("Không tìm thấy tổ chức.");
            }
        }

        var exists = await _db.Users.AnyAsync(
            u => u.TenantId == tenantId && u.Email == email,
            cancellationToken);
        if (exists)
        {
            throw new ConflictAppException("Email đã tồn tại trong thuê bao này.");
        }

        var activeCount = await _db.Users.CountAsync(u => u.IsActive, cancellationToken);
        var licenses = await _db.TenantLicenses.AsNoTracking()
            .Where(l => l.Status == TenantLicenseStatuses.Active)
            .ToListAsync(cancellationToken);
        var seatLimit = licenses
            .OrderByDescending(l => l.ValidUntil)
            .Select(l => (int?)l.SeatLimit)
            .FirstOrDefault();
        if (seatLimit is int limit && activeCount >= limit)
        {
            throw new ConflictAppException(
                $"Đã hết chỗ người dùng ({limit} chỗ theo license). Ngừng một tài khoản hoặc nâng gói.");
        }

        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            OrganizationId = request.OrganizationId
        };

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        }

        _db.Users.Add(user);
        _audit.Append(
            AuditActions.UserCreate,
            AuditObjectTypes.User,
            user.Id,
            afterJson: JsonSerializer.Serialize(new { user.Email, user.DisplayName, passwordSet = user.PasswordHash is not null }));
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Email đã tồn tại trong thuê bao này.");
        }

        return user.Id;
    }
}


public sealed record UpdateUserCommand(
    Guid Id,
    string DisplayName,
    bool IsActive,
    Guid? OrganizationId) : IRequest;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Người dùng không hợp lệ.");
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Tên hiển thị không được để trống.")
            .MaximumLength(256).WithMessage("Tên hiển thị không được vượt quá 256 ký tự.");
    }
}

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public UpdateUserCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.UserManage,
            "Bạn không có quyền cập nhật người dùng.",
            cancellationToken);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy người dùng.");

        if (request.OrganizationId is Guid orgId)
        {
            var orgExists = await _db.Organizations.AnyAsync(o => o.Id == orgId, cancellationToken);
            if (!orgExists)
            {
                throw new NotFoundAppException("Không tìm thấy tổ chức.");
            }
        }

        if (user.IsActive && !request.IsActive)
        {
            await EnsureNotLastAdminAsync(user.Id, cancellationToken);
        }

        var before = JsonSerializer.Serialize(new { user.DisplayName, user.IsActive, user.OrganizationId });
        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        user.OrganizationId = request.OrganizationId;
        _audit.Append(
            AuditActions.UserUpdate,
            AuditObjectTypes.User,
            user.Id,
            beforeJson: before,
            afterJson: JsonSerializer.Serialize(new { user.DisplayName, user.IsActive, user.OrganizationId }));
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNotLastAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        var isAdmin = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where ur.UserId == userId && r.Code == SystemRoleCatalog.Admin
            select ur.Id).AnyAsync(cancellationToken);
        if (!isAdmin)
        {
            return;
        }

        var otherActiveAdmins = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            join u in _db.Users on ur.UserId equals u.Id
            where r.Code == SystemRoleCatalog.Admin && u.Id != userId && u.IsActive
            select u.Id).AnyAsync(cancellationToken);
        if (!otherActiveAdmins)
        {
            throw new ConflictAppException("Không thể ngừng tài khoản Quản trị cuối cùng của thuê bao.");
        }
    }
}

public sealed record SetUserPasswordCommand(Guid UserId, string Password) : IRequest;

public sealed class SetUserPasswordCommandValidator : AbstractValidator<SetUserPasswordCommand>
{
    public SetUserPasswordCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Người dùng không hợp lệ.");
        RuleFor(x => x.Password)
            .Must(p => PasswordRules.Validate(p) is null)
            .WithMessage(x => PasswordRules.Validate(x.Password) ?? "Mật khẩu không hợp lệ.");
    }
}

public sealed class SetUserPasswordCommandHandler : IRequestHandler<SetUserPasswordCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenRevoker _refreshTokens;
    private readonly IAuditWriter _audit;
    private readonly IOperatorNotificationPublisher _notifications;

    public SetUserPasswordCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IPasswordHasher passwordHasher,
        IRefreshTokenRevoker refreshTokens,
        IAuditWriter audit,
        IOperatorNotificationPublisher notifications)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _passwordHasher = passwordHasher;
        _refreshTokens = refreshTokens;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task Handle(SetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.UserManage,
            "Bạn không có quyền đặt mật khẩu người dùng.",
            cancellationToken);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy người dùng.");

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        _audit.Append(
            AuditActions.UserPasswordSet,
            AuditObjectTypes.User,
            user.Id,
            afterJson: JsonSerializer.Serialize(new { user.Email, passwordSet = true }));
        await _db.SaveChangesAsync(cancellationToken);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        await _notifications.PublishAsync(
            new NotificationPublishRequest(
                NotificationEventTypes.UserPasswordSet,
                "Mật khẩu đã được đặt lại",
                $"Tài khoản {user.Email} đã được đặt mật khẩu mới. Phiên đăng nhập cũ hết hiệu lực.",
                "/settings/users",
                AuditObjectTypes.User,
                user.Id),
            cancellationToken);
    }
}

