using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Catalog;
using LCMS.Application.Common;
using LCMS.Application.RateCards;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;
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
    string? CarrierName = null,
    Guid? SupplierPartyId = null,
    Guid? CustomerPartyId = null,
    string? CustomerGroupCode = null,
    Guid? RouteId = null,
    string? DeclarationMode = null) : IRequest<Guid>;

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
        RuleFor(x => x.CustomerGroupCode).MaximumLength(64).When(x => x.CustomerGroupCode is not null);
        RuleFor(x => x).Must(PricingPartnerRules.MatchesDirection)
            .WithMessage("Giá mua chỉ gắn nhà cung cấp. Giá bán gắn khách hàng hoặc nhóm khách hàng, không cả hai.");
        RuleFor(x => x.DeclarationMode)
            .Must(m => m is null || RateDeclarationModes.IsKnown(m))
            .WithMessage(RateDeclarationPolicy.UnknownModeMessage);
    }
}

public static class PricingPartnerRules
{
    public static bool MatchesDirection(string partyType, Guid? supplierPartyId, Guid? customerPartyId, string? customerGroupCode)
    {
        var sell = string.Equals(partyType, "customer", StringComparison.OrdinalIgnoreCase);
        var group = !string.IsNullOrWhiteSpace(customerGroupCode);
        if (!sell && (customerPartyId is not null || group))
        {
            return false;
        }

        if (sell && supplierPartyId is not null)
        {
            return false;
        }

        return !(sell && customerPartyId is not null && group);
    }

    public static bool MatchesDirection(CreateRateCardCommand command) =>
        MatchesDirection(command.PartyType, command.SupplierPartyId, command.CustomerPartyId, command.CustomerGroupCode);

    /// <summary>
    /// New partner ids must hold the role for that price direction.
    /// An id already stored on the card is left alone so a historical mismatch is not rewritten.
    /// </summary>
    public static async Task EnsureRoleOnChangeAsync(
        ILcmsDbContext db,
        Guid? supplierPartyId,
        Guid? customerPartyId,
        Guid? unchangedSupplierPartyId,
        Guid? unchangedCustomerPartyId,
        CancellationToken cancellationToken)
    {
        if (supplierPartyId is Guid supplier && supplier != unchangedSupplierPartyId)
        {
            var vendor = await db.PartyRoles.AnyAsync(
                r => r.PartyId == supplier && r.RoleCode == PartyRoleCodes.Vendor && r.IsActive,
                cancellationToken);
            if (!vendor)
            {
                throw new ConflictAppException(
                    "Đối tác chưa có vai trò Nhà cung cấp. Bổ sung vai trò tại Đối tác, không tạo đối tác trùng.");
            }
        }

        if (customerPartyId is Guid customer && customer != unchangedCustomerPartyId)
        {
            var isCustomer = await db.PartyRoles.AnyAsync(
                r => r.PartyId == customer && r.RoleCode == PartyRoleCodes.Customer && r.IsActive,
                cancellationToken);
            if (!isCustomer)
            {
                throw new ConflictAppException(
                    "Đối tác chưa có vai trò Khách hàng. Bổ sung vai trò tại Đối tác, không tạo đối tác trùng.");
            }
        }
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
            TransportMode = await TransportModeCatalog.ResolveAsync(
                _db, request.TransportMode, allowMultimodal: false, cancellationToken, forRateRule: true),
            RouteCode = RateCardText.Clean(request.RouteCode),
            CarrierName = RateCardText.Clean(request.CarrierName),
            SupplierPartyId = request.SupplierPartyId,
            CustomerPartyId = request.CustomerPartyId,
            CustomerGroupCode = RateCardText.Clean(request.CustomerGroupCode),
            DeclarationMode = string.IsNullOrWhiteSpace(request.DeclarationMode)
                ? null
                : request.DeclarationMode.Trim().ToLowerInvariant(),
            IsActive = true
        };
        await RateCardCatalog.EnsureCurrencyAsync(_db, card.CurrencyCode, null, cancellationToken);
        await RateCardCatalog.ApplyRouteAsync(_db, card, request.RouteId, tenantId, cancellationToken);
        await PricingPartnerRules.EnsureRoleOnChangeAsync(
            _db,
            card.SupplierPartyId,
            card.CustomerPartyId,
            null,
            null,
            cancellationToken);

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
    string? CarrierName = null,
    Guid? RouteId = null,
    string? RowVersion = null) : IRequest;

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
    private readonly IRowVersionGuard _versions;

    public UpdateRateCardCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _versions = versions;
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

        _versions.EnsureCurrent(card, request.RowVersion);
        var partyType = request.PartyType.Trim().ToLowerInvariant();
        var writeCode = partyType == "customer" ? PermissionCodes.RateSellWrite : PermissionCodes.RateBuyWrite;
        await _permissions.EnsureAsync(writeCode, "Bạn không có quyền sửa bảng giá này.", cancellationToken);
        await EnsurePartnerFitsNewDirectionAsync(card, partyType, cancellationToken);
        var nextCurrency = request.CurrencyCode.Trim().ToUpperInvariant();
        await RateCardCatalog.EnsureCurrencyAsync(_db, nextCurrency, card.CurrencyCode, cancellationToken);
        card.Name = request.Name.Trim();
        card.PartyType = partyType;
        card.CurrencyCode = nextCurrency;
        card.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (request.TransportMode is not null)
        {
            card.TransportMode = await TransportModeCatalog.ResolveAsync(
                _db, request.TransportMode, allowMultimodal: false, cancellationToken, card.TransportMode, forRateRule: true);
        }
        if (request.RouteCode is not null && request.RouteId is null) card.RouteCode = RateCardText.Clean(request.RouteCode);
        if (request.CarrierName is not null) card.CarrierName = RateCardText.Clean(request.CarrierName);
        await RateCardCatalog.ApplyRouteAsync(_db, card, request.RouteId, card.TenantId, cancellationToken);
        card.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePartnerFitsNewDirectionAsync(
        RateCard card,
        string partyType,
        CancellationToken cancellationToken)
    {
        if (string.Equals(card.PartyType, partyType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (partyType == "customer" && card.SupplierPartyId is Guid)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["partyType"] =
                [
                    "Đổi chiều giá sang bán không được giữ nhà cung cấp của chiều mua. Bỏ đối tác trước khi lưu."
                ]
            });
        }

        if (partyType == "vendor"
            && (card.CustomerPartyId is Guid || !string.IsNullOrWhiteSpace(card.CustomerGroupCode)))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["partyType"] =
                [
                    "Đổi chiều giá sang mua không được giữ khách hàng của chiều bán. Bỏ đối tác trước khi lưu."
                ]
            });
        }

        if (partyType == "customer" && card.CustomerPartyId is Guid customerId)
        {
            var isCustomer = await _db.PartyRoles.AnyAsync(
                r => r.PartyId == customerId && r.RoleCode == PartyRoleCodes.Customer && r.IsActive,
                cancellationToken);
            if (!isCustomer)
            {
                throw new ValidationAppException(new Dictionary<string, string[]>
                {
                    ["customerPartyId"] = ["Đối tác hiện tại không có vai trò Khách hàng của chiều bán."]
                });
            }
        }

        if (partyType == "vendor" && card.SupplierPartyId is Guid supplierId)
        {
            var isVendor = await _db.PartyRoles.AnyAsync(
                r => r.PartyId == supplierId && r.RoleCode == PartyRoleCodes.Vendor && r.IsActive,
                cancellationToken);
            if (!isVendor)
            {
                throw new ValidationAppException(new Dictionary<string, string[]>
                {
                    ["supplierPartyId"] = ["Đối tác hiện tại không có vai trò Nhà cung cấp của chiều mua."]
                });
            }
        }
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

        var drafts = await _db.RateVersions
            .Where(v => v.RateCardId == card.Id && v.Status == RateVersionStatuses.Draft)
            .ToListAsync(cancellationToken);
        foreach (var d in drafts)
        {
            d.SoftDelete(null);
        }

        card.SoftDelete(null);
        _audit.Append(AuditActions.RateCardDelete, AuditObjectTypes.RateCard, card.Id, "active", "deleted", null);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SetRateCardPartnerCommand(
    Guid Id,
    Guid? SupplierPartyId,
    Guid? CustomerPartyId,
    string? CustomerGroupCode) : IRequest;

public sealed class SetRateCardPartnerCommandHandler : IRequestHandler<SetRateCardPartnerCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public SetRateCardPartnerCommandHandler(ILcmsDbContext db, ITenantContext tenantContext, IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task Handle(SetRateCardPartnerCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var card = await _db.RateCards.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy bảng giá.");
        var writeCode = card.PartyType == "customer" ? PermissionCodes.RateSellWrite : PermissionCodes.RateBuyWrite;
        await _permissions.EnsureAsync(writeCode, "Bạn không có quyền gắn đối tác bảng giá.", cancellationToken);
        if (!PricingPartnerRules.MatchesDirection(card.PartyType, request.SupplierPartyId, request.CustomerPartyId, request.CustomerGroupCode))
        {
            throw new ConflictAppException("Giá mua chỉ gắn nhà cung cấp. Giá bán gắn khách hàng hoặc nhóm khách hàng, không cả hai.");
        }

        if (request.SupplierPartyId is Guid supplier && !await _db.BusinessParties.AnyAsync(p => p.Id == supplier, cancellationToken))
        {
            throw new NotFoundAppException("Không tìm thấy nhà cung cấp.");
        }

        if (request.CustomerPartyId is Guid customer && !await _db.BusinessParties.AnyAsync(p => p.Id == customer, cancellationToken))
        {
            throw new NotFoundAppException("Không tìm thấy khách hàng.");
        }

        await PricingPartnerRules.EnsureRoleOnChangeAsync(
            _db,
            request.SupplierPartyId,
            request.CustomerPartyId,
            card.SupplierPartyId,
            card.CustomerPartyId,
            cancellationToken);

        card.SupplierPartyId = request.SupplierPartyId;
        card.CustomerPartyId = request.CustomerPartyId;
        card.CustomerGroupCode = RateCardText.Clean(request.CustomerGroupCode);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

file static class RateCardText
{
    public static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
