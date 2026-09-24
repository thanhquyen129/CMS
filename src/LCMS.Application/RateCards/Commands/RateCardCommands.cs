using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateCards.Commands;

public sealed record CreateRateCardCommand(
    string Code,
    string Name,
    string PartyType,
    string CurrencyCode,
    string? Description,
    string? TransportMode = null,
    string? RouteCode = null,
    string? CarrierName = null) : IRequest<Guid>;

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
    private readonly IPermissionService _permissions;

    public CreateRateCardCommandHandler(ILcmsDbContext db, ITenantContext tenantContext, IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateRateCardCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var partyType = request.PartyType.Trim().ToLowerInvariant();
        var writeCode = partyType == "customer" ? PermissionCodes.RateSellWrite : PermissionCodes.RateBuyWrite;
        await _permissions.EnsureAsync(writeCode, "Bạn không có quyền tạo bảng giá này.", cancellationToken);
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
            PartyType = partyType,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            TransportMode = RateCardText.Clean(request.TransportMode),
            RouteCode = RateCardText.Clean(request.RouteCode),
            CarrierName = RateCardText.Clean(request.CarrierName),
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
    bool IsActive,
    string? TransportMode = null,
    string? RouteCode = null,
    string? CarrierName = null) : IRequest;

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
    private readonly IPermissionService _permissions;

    public UpdateRateCardCommandHandler(ILcmsDbContext db, ITenantContext tenantContext, IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
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

        var partyType = request.PartyType.Trim().ToLowerInvariant();
        var writeCode = partyType == "customer" ? PermissionCodes.RateSellWrite : PermissionCodes.RateBuyWrite;
        await _permissions.EnsureAsync(writeCode, "Bạn không có quyền sửa bảng giá này.", cancellationToken);
        card.Name = request.Name.Trim();
        card.PartyType = partyType;
        card.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        card.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (request.TransportMode is not null) card.TransportMode = RateCardText.Clean(request.TransportMode);
        if (request.RouteCode is not null) card.RouteCode = RateCardText.Clean(request.RouteCode);
        if (request.CarrierName is not null) card.CarrierName = RateCardText.Clean(request.CarrierName);
        card.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SoftDeleteRateCardCommand(Guid Id) : IRequest;

public sealed class SoftDeleteRateCardCommandHandler : IRequestHandler<SoftDeleteRateCardCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public SoftDeleteRateCardCommandHandler(ILcmsDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
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

        var published = await _db.RateVersions.AnyAsync(
            v => v.RateCardId == card.Id && v.Status == RateVersionStatuses.Published,
            cancellationToken);
        if (published)
        {
            throw new ConflictAppException("Bảng giá đã có phiên bản phát hành — không ngừng. Lập phiên bản mới.");
        }

        card.SoftDelete(null);
        _audit.Append(AuditActions.RateCardDelete, AuditObjectTypes.RateCard, card.Id, "active", "deleted", null);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

file static class RateCardText
{
    public static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
