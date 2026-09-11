using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Tenants.Commands;

public sealed record CreateTenantCommand(string Code, string Name) : IRequest<Guid>;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã thuê bao không được để trống.")
            .MaximumLength(64).WithMessage("Mã thuê bao không được vượt quá 64 ký tự.")
            .Matches(@"^[A-Za-z0-9_-]+$").WithMessage("Mã thuê bao chỉ gồm chữ, số, gạch dưới hoặc gạch ngang.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên thuê bao không được để trống.")
            .MaximumLength(256).WithMessage("Tên thuê bao không được vượt quá 256 ký tự.");
    }
}

public sealed class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, Guid>
{
    private readonly ILcmsDbContext _db;

    public CreateTenantCommandHandler(ILcmsDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var exists = await _db.Tenants.AnyAsync(t => t.Code == code, cancellationToken);
        if (exists)
        {
            throw new ConflictAppException("Mã thuê bao đã tồn tại.");
        }

        var tenant = new Tenant
        {
            Code = code,
            Name = request.Name.Trim(),
            IsActive = true
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);
        return tenant.Id;
    }
}
