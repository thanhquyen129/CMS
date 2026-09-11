using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BusinessParties.Commands;

public sealed record CreateBusinessPartyCommand(string Code, string Name) : IRequest<Guid>;

public sealed class CreateBusinessPartyCommandValidator : AbstractValidator<CreateBusinessPartyCommand>
{
    public CreateBusinessPartyCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã đối tác không được để trống.")
            .MaximumLength(64).WithMessage("Mã đối tác không được vượt quá 64 ký tự.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên đối tác không được để trống.")
            .MaximumLength(256).WithMessage("Tên đối tác không được vượt quá 256 ký tự.");
    }
}

public sealed class CreateBusinessPartyCommandHandler : IRequestHandler<CreateBusinessPartyCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateBusinessPartyCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateBusinessPartyCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var code = request.Code.Trim();

        if (await _db.BusinessParties.AnyAsync(p => p.TenantId == tenantId && p.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã đối tác đã tồn tại trong thuê bao này.");
        }

        var party = new BusinessParty
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            IsActive = true
        };

        _db.BusinessParties.Add(party);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Mã đối tác đã tồn tại trong thuê bao này.");
        }

        return party.Id;
    }
}

public sealed record UpdateBusinessPartyCommand(Guid Id, string Name, bool IsActive) : IRequest;

public sealed class UpdateBusinessPartyCommandValidator : AbstractValidator<UpdateBusinessPartyCommand>
{
    public UpdateBusinessPartyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Đối tác không hợp lệ.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên đối tác không được để trống.")
            .MaximumLength(256).WithMessage("Tên đối tác không được vượt quá 256 ký tự.");
    }
}

public sealed class UpdateBusinessPartyCommandHandler : IRequestHandler<UpdateBusinessPartyCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpdateBusinessPartyCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(UpdateBusinessPartyCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var party = await _db.BusinessParties.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (party is null)
        {
            throw new NotFoundAppException("Không tìm thấy đối tác.");
        }

        party.Name = request.Name.Trim();
        party.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SoftDeleteBusinessPartyCommand(Guid Id) : IRequest;

public sealed class SoftDeleteBusinessPartyCommandHandler : IRequestHandler<SoftDeleteBusinessPartyCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public SoftDeleteBusinessPartyCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(SoftDeleteBusinessPartyCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var party = await _db.BusinessParties.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);
        if (party is null)
        {
            throw new NotFoundAppException("Không tìm thấy đối tác.");
        }

        party.SoftDelete(null);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
