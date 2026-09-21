using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LCMS.Application.Tenants.Commands;

public sealed record TenantProfileDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    string? LegalName,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Website,
    string? AddressLine1,
    string? AddressLine2,
    string? Ward,
    string? District,
    string? City,
    string? Province,
    string? CountryCode,
    string? PostalCode,
    string TimeZoneId,
    string DateFormat,
    string DefaultCurrencyCode,
    bool HasLogo);

public sealed record GetTenantProfileQuery : IRequest<TenantProfileDto>;

public sealed class GetTenantProfileQueryHandler : IRequestHandler<GetTenantProfileQuery, TenantProfileDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetTenantProfileQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<TenantProfileDto> Handle(GetTenantProfileQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thông tin doanh nghiệp.");

        return Map(tenant);
    }

    internal static TenantProfileDto Map(Tenant tenant) => new(
        tenant.Id,
        tenant.Code,
        tenant.Name,
        tenant.IsActive,
        tenant.LegalName,
        tenant.TaxId,
        tenant.Phone,
        tenant.Email,
        tenant.Website,
        tenant.AddressLine1,
        tenant.AddressLine2,
        tenant.Ward,
        tenant.District,
        tenant.City,
        tenant.Province,
        tenant.CountryCode,
        tenant.PostalCode,
        string.IsNullOrWhiteSpace(tenant.TimeZoneId) ? TenantDefaults.TimeZoneId : tenant.TimeZoneId,
        string.IsNullOrWhiteSpace(tenant.DateFormat) ? TenantDefaults.DateFormat : tenant.DateFormat,
        string.IsNullOrWhiteSpace(tenant.DefaultCurrencyCode) ? TenantDefaults.CurrencyCode : tenant.DefaultCurrencyCode,
        tenant.LogoBytes is { Length: > 0 });
}

public sealed record UpdateTenantProfileCommand(
    string Name,
    string? LegalName,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Website,
    string? AddressLine1,
    string? AddressLine2,
    string? Ward,
    string? District,
    string? City,
    string? Province,
    string? CountryCode,
    string? PostalCode,
    string TimeZoneId,
    string DateFormat,
    string DefaultCurrencyCode) : IRequest<TenantProfileDto>;

public sealed class UpdateTenantProfileCommandValidator : AbstractValidator<UpdateTenantProfileCommand>
{
    private static readonly HashSet<string> TimeZones = new(StringComparer.OrdinalIgnoreCase)
    {
        "Asia/Ho_Chi_Minh", "Asia/Bangkok", "Asia/Singapore", "Asia/Tokyo",
        "UTC", "Europe/London", "America/New_York"
    };

    private static readonly HashSet<string> DateFormats = new(StringComparer.Ordinal)
    {
        "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy"
    };

    public UpdateTenantProfileCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256).WithMessage("Tên doanh nghiệp không hợp lệ.");
        RuleFor(x => x.LegalName).MaximumLength(256).When(x => x.LegalName is not null);
        RuleFor(x => x.TaxId).MaximumLength(32).When(x => x.TaxId is not null);
        RuleFor(x => x.Phone).MaximumLength(64).When(x => x.Phone is not null);
        RuleFor(x => x.Email).MaximumLength(256).When(x => x.Email is not null);
        RuleFor(x => x.Website).MaximumLength(256).When(x => x.Website is not null);
        RuleFor(x => x.AddressLine1).MaximumLength(256).When(x => x.AddressLine1 is not null);
        RuleFor(x => x.AddressLine2).MaximumLength(256).When(x => x.AddressLine2 is not null);
        RuleFor(x => x.Ward).MaximumLength(128).When(x => x.Ward is not null);
        RuleFor(x => x.District).MaximumLength(128).When(x => x.District is not null);
        RuleFor(x => x.City).MaximumLength(128).When(x => x.City is not null);
        RuleFor(x => x.Province).MaximumLength(128).When(x => x.Province is not null);
        RuleFor(x => x.CountryCode).MaximumLength(2).When(x => x.CountryCode is not null);
        RuleFor(x => x.PostalCode).MaximumLength(32).When(x => x.PostalCode is not null);
        RuleFor(x => x.TimeZoneId)
            .Must(z => TimeZones.Contains(z.Trim()))
            .WithMessage("Múi giờ không nằm trong danh mục hỗ trợ.");
        RuleFor(x => x.DateFormat)
            .Must(z => DateFormats.Contains(z.Trim()))
            .WithMessage("Định dạng ngày không hợp lệ.");
        RuleFor(x => x.DefaultCurrencyCode)
            .NotEmpty()
            .Length(3)
            .WithMessage("Tiền tệ mặc định phải là mã ISO 3 ký tự.");
    }
}

public sealed class UpdateTenantProfileCommandHandler
    : IRequestHandler<UpdateTenantProfileCommand, TenantProfileDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public UpdateTenantProfileCommandHandler(
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

    public async Task<TenantProfileDto> Handle(
        UpdateTenantProfileCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.SettingsManage,
            "Bạn không có quyền cập nhật thông tin doanh nghiệp.",
            cancellationToken);

        var tenant = await _db.Tenants.FirstOrDefaultAsync(
            t => t.Id == _tenantContext.TenantId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thông tin doanh nghiệp.");

        var before = JsonSerializer.Serialize(new { tenant.Name, tenant.TaxId, tenant.TimeZoneId, tenant.DefaultCurrencyCode });
        tenant.Name = request.Name.Trim();
        tenant.LegalName = Norm(request.LegalName);
        tenant.TaxId = Norm(request.TaxId);
        tenant.Phone = Norm(request.Phone);
        tenant.Email = Norm(request.Email);
        tenant.Website = Norm(request.Website);
        tenant.AddressLine1 = Norm(request.AddressLine1);
        tenant.AddressLine2 = Norm(request.AddressLine2);
        tenant.Ward = Norm(request.Ward);
        tenant.District = Norm(request.District);
        tenant.City = Norm(request.City);
        tenant.Province = Norm(request.Province);
        tenant.CountryCode = string.IsNullOrWhiteSpace(request.CountryCode)
            ? null
            : request.CountryCode.Trim().ToUpperInvariant();
        tenant.PostalCode = Norm(request.PostalCode);
        tenant.TimeZoneId = request.TimeZoneId.Trim();
        tenant.DateFormat = request.DateFormat.Trim();
        tenant.DefaultCurrencyCode = request.DefaultCurrencyCode.Trim().ToUpperInvariant();

        _audit.Append(
            AuditActions.TenantProfileUpdate,
            AuditObjectTypes.Tenant,
            tenant.Id,
            beforeJson: before,
            afterJson: JsonSerializer.Serialize(new { tenant.Name, tenant.TaxId, tenant.TimeZoneId, tenant.DefaultCurrencyCode }));
        await _db.SaveChangesAsync(cancellationToken);
        return GetTenantProfileQueryHandler.Map(tenant);
    }

    private static string? Norm(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record SetTenantLogoCommand(string ContentType, byte[] Bytes) : IRequest;
public sealed record ClearTenantLogoCommand : IRequest;
public sealed record GetTenantLogoQuery : IRequest<(string ContentType, byte[] Bytes)?>;

public sealed class SetTenantLogoCommandHandler : IRequestHandler<SetTenantLogoCommand>
{
    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp"
    };

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public SetTenantLogoCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task Handle(SetTenantLogoCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.SettingsManage,
            "Bạn không có quyền cập nhật logo doanh nghiệp.",
            cancellationToken);

        if (!Types.Contains(request.ContentType))
        {
            throw new ConflictAppException("Logo chỉ nhận PNG, JPEG hoặc WebP.");
        }

        if (request.Bytes.Length is 0 or > TenantDefaults.LogoMaxBytes)
        {
            throw new ConflictAppException("Logo phải nhỏ hơn 512 KB.");
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(
            t => t.Id == _tenantContext.TenantId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thông tin doanh nghiệp.");
        tenant.LogoContentType = request.ContentType.Trim().ToLowerInvariant();
        tenant.LogoBytes = request.Bytes;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ClearTenantLogoCommandHandler : IRequestHandler<ClearTenantLogoCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ClearTenantLogoCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task Handle(ClearTenantLogoCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.SettingsManage,
            "Bạn không có quyền cập nhật logo doanh nghiệp.",
            cancellationToken);

        var tenant = await _db.Tenants.FirstOrDefaultAsync(
            t => t.Id == _tenantContext.TenantId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thông tin doanh nghiệp.");
        tenant.LogoContentType = null;
        tenant.LogoBytes = null;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class GetTenantLogoQueryHandler
    : IRequestHandler<GetTenantLogoQuery, (string ContentType, byte[] Bytes)?>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetTenantLogoQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<(string ContentType, byte[] Bytes)?> Handle(
        GetTenantLogoQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _tenantContext.TenantId, cancellationToken);
        if (tenant?.LogoBytes is not { Length: > 0 } || string.IsNullOrWhiteSpace(tenant.LogoContentType))
        {
            return null;
        }

        return (tenant.LogoContentType, tenant.LogoBytes);
    }
}
