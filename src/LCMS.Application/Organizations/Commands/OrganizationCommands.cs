using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Organizations.Commands;

public sealed record CreateOrganizationCommand(string Code, string Name, Guid? ParentId) : IRequest<Guid>;

public sealed class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã tổ chức không được để trống.")
            .MaximumLength(64).WithMessage("Mã tổ chức không được vượt quá 64 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên tổ chức không được để trống.")
            .MaximumLength(256).WithMessage("Tên tổ chức không được vượt quá 256 ký tự.");
    }
}

public sealed class CreateOrganizationCommandHandler : IRequestHandler<CreateOrganizationCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateOrganizationCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateOrganizationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var code = request.Code.Trim();

        if (request.ParentId is Guid parentId)
        {
            var parentExists = await _db.Organizations.AnyAsync(o => o.Id == parentId, cancellationToken);
            if (!parentExists)
            {
                throw new NotFoundAppException("Không tìm thấy tổ chức cha.");
            }
        }

        if (await _db.Organizations.AnyAsync(o => o.TenantId == tenantId && o.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã tổ chức đã tồn tại trong thuê bao này.");
        }

        var org = new Organization
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            ParentId = request.ParentId,
            IsActive = true
        };

        _db.Organizations.Add(org);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Mã tổ chức đã tồn tại trong thuê bao này.");
        }

        return org.Id;
    }
}

public sealed record UpdateOrganizationCommand(Guid Id, string Name, Guid? ParentId, bool IsActive) : IRequest;

public sealed class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Tổ chức không hợp lệ.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên tổ chức không được để trống.")
            .MaximumLength(256).WithMessage("Tên tổ chức không được vượt quá 256 ký tự.");
    }
}

public sealed class UpdateOrganizationCommandHandler : IRequestHandler<UpdateOrganizationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpdateOrganizationCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(UpdateOrganizationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
        if (org is null)
        {
            throw new NotFoundAppException("Không tìm thấy tổ chức.");
        }

        if (request.ParentId == request.Id)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["ParentId"] = ["Tổ chức cha không được trùng với chính tổ chức."]
            });
        }

        if (request.ParentId is Guid parentId)
        {
            var parentExists = await _db.Organizations.AnyAsync(o => o.Id == parentId, cancellationToken);
            if (!parentExists)
            {
                throw new NotFoundAppException("Không tìm thấy tổ chức cha.");
            }
        }

        org.Name = request.Name.Trim();
        org.ParentId = request.ParentId;
        org.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SoftDeleteOrganizationCommand(Guid Id) : IRequest;

public sealed class SoftDeleteOrganizationCommandHandler : IRequestHandler<SoftDeleteOrganizationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public SoftDeleteOrganizationCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(SoftDeleteOrganizationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken);
        if (org is null)
        {
            throw new NotFoundAppException("Không tìm thấy tổ chức.");
        }

        org.SoftDelete(null);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
