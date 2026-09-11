using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Users.Commands;

public sealed record CreateUserCommand(string Email, string DisplayName, Guid? OrganizationId) : IRequest<Guid>;

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
    }
}

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateUserCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

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

        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            OrganizationId = request.OrganizationId
        };

        _db.Users.Add(user);
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

    public UpdateUserCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

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

        user.DisplayName = request.DisplayName.Trim();
        user.IsActive = request.IsActive;
        user.OrganizationId = request.OrganizationId;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
