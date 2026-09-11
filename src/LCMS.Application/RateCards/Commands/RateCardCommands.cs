using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateCards.Commands;

public sealed record CreateRateCardCommand(
    string Code,
    string Name,
    string PartyType,
    string CurrencyCode,
    string? Description) : IRequest<Guid>;

public sealed class CreateRateCardCommandValidator : AbstractValidator<CreateRateCardCommand>
{
    public CreateRateCardCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã bảng giá không được để trống.")
            .MaximumLength(64).WithMessage("Mã bảng giá không được vượt quá 64 ký tự.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên bảng giá không được để trống.")
            .MaximumLength(256).WithMessage("Tên bảng giá không được vượt quá 256 ký tự.");
        RuleFor(x => x.PartyType)
            .NotEmpty().WithMessage("Loại đối tác không được để trống.")
            .Must(p => p is "customer" or "vendor")
            .WithMessage("Loại đối tác phải là customer hoặc vendor.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Description)
            .MaximumLength(1024)
            .When(x => x.Description is not null);
    }
}

public sealed class CreateRateCardCommandHandler : IRequestHandler<CreateRateCardCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateRateCardCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateRateCardCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var code = request.Code.Trim();

        if (await _db.RateCards.AnyAsync(r => r.TenantId == tenantId && r.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã bảng giá đã tồn tại trong thuê bao này.");
        }

        var card = new RateCard
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            PartyType = request.PartyType.Trim().ToLowerInvariant(),
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true
        };

        _db.RateCards.Add(card);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Mã bảng giá đã tồn tại trong thuê bao này.");
        }

        return card.Id;
    }
}

public sealed record UpdateRateCardCommand(
    Guid Id,
    string Name,
    string PartyType,
    string CurrencyCode,
    string? Description,
    bool IsActive) : IRequest;

public sealed class UpdateRateCardCommandValidator : AbstractValidator<UpdateRateCardCommand>
{
    public UpdateRateCardCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Bảng giá không hợp lệ.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên bảng giá không được để trống.")
            .MaximumLength(256).WithMessage("Tên bảng giá không được vượt quá 256 ký tự.");
        RuleFor(x => x.PartyType)
            .NotEmpty().WithMessage("Loại đối tác không được để trống.")
            .Must(p => p is "customer" or "vendor")
            .WithMessage("Loại đối tác phải là customer hoặc vendor.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Description)
            .MaximumLength(1024)
            .When(x => x.Description is not null);
    }
}

public sealed class UpdateRateCardCommandHandler : IRequestHandler<UpdateRateCardCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpdateRateCardCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(UpdateRateCardCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var card = await _db.RateCards.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (card is null)
        {
            throw new NotFoundAppException("Không tìm thấy bảng giá.");
        }

        card.Name = request.Name.Trim();
        card.PartyType = request.PartyType.Trim().ToLowerInvariant();
        card.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        card.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        card.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SoftDeleteRateCardCommand(Guid Id) : IRequest;

public sealed class SoftDeleteRateCardCommandHandler : IRequestHandler<SoftDeleteRateCardCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public SoftDeleteRateCardCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(SoftDeleteRateCardCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var card = await _db.RateCards.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
        if (card is null)
        {
            throw new NotFoundAppException("Không tìm thấy bảng giá.");
        }

        card.SoftDelete(null);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
