using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.EconomicCharges;

public sealed record EconomicChargeTypeDto(Guid Id, string Code, string Name, bool IsActive);

public sealed record ListEconomicChargeTypesQuery : IRequest<IReadOnlyList<EconomicChargeTypeDto>>;

public sealed class ListEconomicChargeTypesQueryHandler : IRequestHandler<ListEconomicChargeTypesQuery, IReadOnlyList<EconomicChargeTypeDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListEconomicChargeTypesQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<EconomicChargeTypeDto>> Handle(ListEconomicChargeTypesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        return await _db.EconomicChargeTypes.AsNoTracking()
            .OrderBy(t => t.Code)
            .Select(t => new EconomicChargeTypeDto(t.Id, t.Code, t.Name, t.IsActive))
            .ToListAsync(cancellationToken);
    }
}

public sealed record UpsertEconomicChargeTypeCommand(string Code, string Name) : IRequest<Guid>;

public sealed class UpsertEconomicChargeTypeCommandValidator : AbstractValidator<UpsertEconomicChargeTypeCommand>
{
    public UpsertEconomicChargeTypeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}

public sealed class UpsertEconomicChargeTypeCommandHandler : IRequestHandler<UpsertEconomicChargeTypeCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public UpsertEconomicChargeTypeCommandHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(UpsertEconomicChargeTypeCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await EnsurePricingWriteAsync(cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        var existing = await _db.EconomicChargeTypes.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
        if (existing is null)
        {
            existing = new EconomicChargeType
            {
                TenantId = _tenant.TenantId!.Value,
                Code = code,
                Name = request.Name.Trim(),
                IsActive = true
            };
            _db.EconomicChargeTypes.Add(existing);
        }
        else
        {
            existing.Name = request.Name.Trim();
            existing.IsActive = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }

    private async Task EnsurePricingWriteAsync(CancellationToken cancellationToken)
    {
        if (await _permissions.HasPermissionAsync(PermissionCodes.RateBuyWrite, cancellationToken)
            || await _permissions.HasPermissionAsync(PermissionCodes.RateSellWrite, cancellationToken))
        {
            return;
        }

        throw new ForbiddenAppException("Bạn không có quyền khai báo khoản mục kinh tế.");
    }
}

public sealed record MapChargeTypeCommand(string SourceKind, string SourceCode, Guid EconomicChargeTypeId) : IRequest<Guid>;

public sealed class MapChargeTypeCommandValidator : AbstractValidator<MapChargeTypeCommand>
{
    public MapChargeTypeCommandValidator()
    {
        RuleFor(x => x.SourceKind).Must(k => ChargeTypeMappingKinds.All.Contains(k ?? ""))
            .WithMessage("Nguồn mapping phải là cost_type, revenue_type hoặc component.");
        RuleFor(x => x.SourceCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.EconomicChargeTypeId).NotEmpty();
    }
}

public sealed class MapChargeTypeCommandHandler : IRequestHandler<MapChargeTypeCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public MapChargeTypeCommandHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(MapChargeTypeCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        if (!await _permissions.HasPermissionAsync(PermissionCodes.RateBuyWrite, cancellationToken)
            && !await _permissions.HasPermissionAsync(PermissionCodes.RateSellWrite, cancellationToken))
        {
            throw new ForbiddenAppException("Bạn không có quyền gắn khoản mục kinh tế.");
        }

        var typeExists = await _db.EconomicChargeTypes.AnyAsync(t => t.Id == request.EconomicChargeTypeId && t.IsActive, cancellationToken);
        if (!typeExists)
        {
            throw new NotFoundAppException("Không tìm thấy khoản mục kinh tế.");
        }

        var kind = request.SourceKind.Trim().ToLowerInvariant();
        var code = request.SourceCode.Trim().ToUpperInvariant();
        var row = await _db.ChargeTypeMappings.FirstOrDefaultAsync(
            m => m.SourceKind == kind && m.SourceCode == code,
            cancellationToken);
        if (row is null)
        {
            row = new ChargeTypeMapping
            {
                TenantId = _tenant.TenantId!.Value,
                SourceKind = kind,
                SourceCode = code,
                EconomicChargeTypeId = request.EconomicChargeTypeId
            };
            _db.ChargeTypeMappings.Add(row);
        }
        else
        {
            row.EconomicChargeTypeId = request.EconomicChargeTypeId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }
}

internal static class EconomicChargeTypeRules
{
    public static async Task<Guid> RequireActiveAsync(
        ILcmsDbContext db,
        Guid? id,
        CancellationToken cancellationToken)
    {
        if (id is null || id == Guid.Empty)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["economicChargeTypeId"] = ["Chọn khoản mục kinh tế."]
            });
        }

        var row = await db.EconomicChargeTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id.Value, cancellationToken);
        if (row is null || !row.IsActive)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["economicChargeTypeId"] = ["Khoản mục kinh tế không thuộc thuê bao này hoặc đã ngừng dùng."]
            });
        }

        return row.Id;
    }
}

/// <summary>
/// Idempotent baseline economic charge catalog for demo and ops tenants (closes GAP DATA-01).
/// </summary>
public static class EconomicChargeCatalogSeeder
{
    public static readonly (string Code, string Name)[] Baseline =
    [
        ("FREIGHT", "Cước vận tải chính (Base freight)"),
        ("PICKUP", "Phí lấy hàng tận nơi"),
        ("DELIVERY", "Phí giao hàng"),
        ("REMOTE", "Phụ phí vùng xa / hải đảo"),
        ("HANDLING", "Phí bốc xếp / nâng hạ"),
        ("FUEL", "Phụ phí nhiên liệu")
    ];

    public static async Task EnsureForTenantAsync(
        ILcmsDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.EconomicChargeTypes.IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && t.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var byCode = existing.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var (code, name) in Baseline)
        {
            if (byCode.TryGetValue(code, out var row))
            {
                if (!row.IsActive)
                {
                    row.IsActive = true;
                    changed = true;
                }

                continue;
            }

            db.EconomicChargeTypes.Add(new EconomicChargeType
            {
                TenantId = tenantId,
                Code = code,
                Name = name,
                IsActive = true
            });
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task EnsureForDemoAndOpsTenantsAsync(
        ILcmsDbContext db,
        CancellationToken cancellationToken = default)
    {
        var tenantIds = await db.Tenants.AsNoTracking()
            .Where(t => t.DeletedAt == null && (t.Code.ToLower() == "ops" || t.Code.ToLower() == "demo"))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        foreach (var tenantId in tenantIds)
        {
            await EnsureForTenantAsync(db, tenantId, cancellationToken);
        }
    }
}


public sealed record EconomicChargeAccountingMappingDto(
    Guid EconomicChargeTypeId,
    string Code,
    string Name,
    string? CostTypeCode,
    string? RevenueTypeCode,
    string NoticeMessage
);

public sealed record GetEconomicChargeAccountingMappingQuery(Guid Id) : IRequest<EconomicChargeAccountingMappingDto>;

public sealed class GetEconomicChargeAccountingMappingQueryHandler : IRequestHandler<GetEconomicChargeAccountingMappingQuery, EconomicChargeAccountingMappingDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public GetEconomicChargeAccountingMappingQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<EconomicChargeAccountingMappingDto> Handle(GetEconomicChargeAccountingMappingQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var chargeType = await _db.EconomicChargeTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy loại phí kinh tế.");

        var mappings = await _db.ChargeTypeMappings.AsNoTracking()
            .Where(m => m.EconomicChargeTypeId == request.Id)
            .ToListAsync(cancellationToken);

        var costType = mappings.FirstOrDefault(m => string.Equals(m.SourceKind, ChargeTypeMappingKinds.CostType, StringComparison.OrdinalIgnoreCase))?.SourceCode;
        var revType = mappings.FirstOrDefault(m => string.Equals(m.SourceKind, ChargeTypeMappingKinds.RevenueType, StringComparison.OrdinalIgnoreCase))?.SourceCode;
        var comp = mappings.FirstOrDefault(m => string.Equals(m.SourceKind, ChargeTypeMappingKinds.Component, StringComparison.OrdinalIgnoreCase))?.SourceCode;
        costType ??= comp;
        revType ??= comp;

        return new EconomicChargeAccountingMappingDto(
            chargeType.Id,
            chargeType.Code,
            chargeType.Name,
            costType,
            revType,
            $"Tự động ánh xạ: BUY -> {costType ?? "chưa ánh xạ"}, SELL -> {revType ?? "chưa ánh xạ"}."
        );
    }
}
