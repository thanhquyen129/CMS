using System.Text.Json;
using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Common;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Surcharges;

public sealed record SurchargeBreakInput(int SequenceNo, decimal MinQuantity, decimal? MaxQuantity, decimal UnitAmount);

public sealed record SurchargeRuleInput(
    string CalculationMode,
    string? Basis,
    string CurrencyCode,
    decimal RateAmountPercent,
    int? Priority,
    decimal? MinAmount,
    decimal? MaxAmount,
    string? ContainerType,
    string? TransportMode,
    string? ServiceTypeCode,
    string? RouteCode,
    string? OriginCode,
    string? DestinationCode,
    string? CommodityCode,
    bool? DangerousGoods,
    decimal? WeightFrom,
    decimal? WeightTo,
    Guid? RateCardId,
    Guid? RateVersionId,
    Guid? VendorPartyId,
    Guid? CustomerPartyId,
    IReadOnlyList<SurchargeBreakInput>? Breaks,
    string? CustomerGroupCode = null);

public sealed record CreateSurchargeCommand(
    string Code,
    string Name,
    string Direction,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    bool Publish,
    SurchargeRuleInput Rule,
    decimal? VatRate = null) : IRequest<CreateSurchargeResult>;

public sealed record CreateSurchargeResult(Guid SurchargeId, Guid VersionId);

public sealed class CreateSurchargeCommandValidator : AbstractValidator<CreateSurchargeCommand>
{
    public CreateSurchargeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Direction)
            .Must(d => SurchargeDirections.All.Contains(d))
            .WithMessage("Chiều phụ phí phải là buy, sell hoặc both.");
        RuleFor(x => x.Rule).NotNull();
        RuleFor(x => x.Rule.CalculationMode)
            .Must(m => SurchargeCalcModes.All.Contains(NormalizeMode(m)))
            .WithMessage("Cách tính phụ phí không được hỗ trợ.");
        RuleFor(x => x.Rule.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.Rule.RateAmountPercent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VatRate)
            .Must(DeclaredVat.IsValid)
            .WithMessage("Thuế suất VAT phải từ 0 đến 100, hoặc để trống nếu chưa khai báo.");
        RuleFor(x => x.Rule.CustomerGroupCode).MaximumLength(64).When(x => x.Rule.CustomerGroupCode is not null);
    }

    internal static string NormalizeMode(string? mode)
    {
        var value = (mode ?? "").Trim().ToLowerInvariant().Replace('-', '_');
        return value switch
        {
            "unit" or "unit_rate" => SurchargeCalcModes.UnitRate,
            "fixed" or "fixed_rate" => SurchargeCalcModes.FixedRate,
            "container" or "container_rate" => SurchargeCalcModes.ContainerRate,
            "weight_break" or "weightbreak" => SurchargeCalcModes.WeightBreak,
            "percent" or "percentage" or "percent_of_base" => SurchargeCalcModes.Percentage,
            "composite" or "min_max" or "min_max_clamp" => SurchargeCalcModes.Composite,
            _ => value
        };
    }
}

public sealed class CreateSurchargeCommandHandler : IRequestHandler<CreateSurchargeCommand, CreateSurchargeResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public CreateSurchargeCommandHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<CreateSurchargeResult> Handle(CreateSurchargeCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenant.TenantId!.Value;
        var direction = request.Direction.Trim().ToLowerInvariant();
        await SurchargeWriteGuard.EnsureDirectionAsync(_db, _permissions, direction, write: true, cancellationToken);
        if (request.Publish)
        {
            await SurchargeWriteGuard.EnsureDirectionAsync(_db, _permissions, direction, write: false, cancellationToken);
        }
        var code = request.Code.Trim();
        if (await _db.Surcharges.AnyAsync(s => s.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã phụ phí đã tồn tại trong thuê bao này.");
        }

        var surcharge = new Surcharge
        {
            TenantId = tenantId,
            Code = code,
            Name = request.Name.Trim(),
            Direction = direction,
            Status = SurchargeStatuses.Active
        };
        var version = new SurchargeVersion
        {
            TenantId = tenantId,
            SurchargeId = surcharge.Id,
            VersionNo = 1,
            PublishStatus = SurchargeVersionStatuses.Draft,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            VatRate = request.VatRate
        };
        if (request.Publish)
        {
            version.PublishStatus = SurchargeVersionStatuses.Published;
            version.PublishedAt = DateTimeOffset.UtcNow;
            version.ValidFrom ??= version.PublishedAt;
        }

        _db.Surcharges.Add(surcharge);
        _db.SurchargeVersions.Add(version);
        SurchargePartnerRules.Ensure(direction, request.Rule);
        SurchargeGraph.AddRule(_db, tenantId, version.Id, request.Rule);
        await _db.SaveChangesAsync(cancellationToken);
        return new CreateSurchargeResult(surcharge.Id, version.Id);
    }
}

public sealed record UpdateSurchargeVersionCommand(
    Guid SurchargeId,
    Guid VersionId,
    string Name,
    string Direction,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    SurchargeRuleInput Rule,
    decimal? VatRate = null) : IRequest<Guid>;

public sealed class UpdateSurchargeVersionCommandHandler : IRequestHandler<UpdateSurchargeVersionCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public UpdateSurchargeVersionCommandHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(UpdateSurchargeVersionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var direction = request.Direction.Trim().ToLowerInvariant();
        await SurchargeWriteGuard.EnsureDirectionAsync(_db, _permissions, direction, write: true, cancellationToken);
        var surcharge = await _db.Surcharges.FirstOrDefaultAsync(s => s.Id == request.SurchargeId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phụ phí.");
        var version = await _db.SurchargeVersions.FirstOrDefaultAsync(
            v => v.Id == request.VersionId && v.SurchargeId == surcharge.Id,
            cancellationToken) ?? throw new NotFoundAppException("Không tìm thấy phiên bản phụ phí.");
        if (version.IsPublished)
        {
            throw new ConflictAppException("Phiên bản phụ phí đã phát hành bất biến. Hãy tạo phiên bản mới.");
        }

        surcharge.Name = request.Name.Trim();
        surcharge.Direction = direction;
        version.ValidFrom = request.ValidFrom;
        version.ValidTo = request.ValidTo;
        version.VatRate = request.VatRate;
        SurchargePartnerRules.Ensure(direction, request.Rule);
        await SurchargeGraph.ReplaceRulesAsync(_db, _tenant.TenantId!.Value, version.Id, request.Rule, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return version.Id;
    }
}

public sealed record AddSurchargeRuleCommand(Guid VersionId, SurchargeRuleInput Rule) : IRequest<Guid>;

public sealed class AddSurchargeRuleCommandHandler : IRequestHandler<AddSurchargeRuleCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public AddSurchargeRuleCommandHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(AddSurchargeRuleCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var version = await _db.SurchargeVersions.FirstOrDefaultAsync(v => v.Id == request.VersionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên bản phụ phí.");
        if (version.IsPublished)
        {
            throw new ConflictAppException("Phiên bản phụ phí đã phát hành bất biến. Hãy tạo phiên bản mới.");
        }

        var surcharge = await _db.Surcharges.FirstAsync(s => s.Id == version.SurchargeId, cancellationToken);
        await SurchargeWriteGuard.EnsureDirectionAsync(_db, _permissions, surcharge.Direction, write: true, cancellationToken);
        SurchargePartnerRules.Ensure(surcharge.Direction, request.Rule);
        var rule = SurchargeGraph.AddRule(_db, _tenant.TenantId!.Value, version.Id, request.Rule);
        await _db.SaveChangesAsync(cancellationToken);
        return rule.Id;
    }
}

public sealed record PublishSurchargeVersionCommand(Guid SurchargeId, Guid VersionId) : IRequest<Guid>;

public sealed class PublishSurchargeVersionCommandHandler : IRequestHandler<PublishSurchargeVersionCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public PublishSurchargeVersionCommandHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(PublishSurchargeVersionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var surcharge = await _db.Surcharges.FirstOrDefaultAsync(s => s.Id == request.SurchargeId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phụ phí.");
        await SurchargeWriteGuard.EnsureDirectionAsync(_db, _permissions, surcharge.Direction, write: false, cancellationToken);
        var version = await _db.SurchargeVersions.FirstOrDefaultAsync(
            v => v.Id == request.VersionId && v.SurchargeId == surcharge.Id,
            cancellationToken) ?? throw new NotFoundAppException("Không tìm thấy phiên bản phụ phí.");
        if (version.IsPublished)
        {
            throw new ConflictAppException("Phiên bản phụ phí đã phát hành.");
        }

        var hasRule = await _db.SurchargeRules.AnyAsync(r => r.SurchargeVersionId == version.Id, cancellationToken);
        if (!hasRule)
        {
            throw new ConflictAppException("Phiên bản phụ phí chưa có công thức.");
        }

        version.PublishStatus = SurchargeVersionStatuses.Published;
        version.PublishedAt = DateTimeOffset.UtcNow;
        version.ValidFrom ??= version.PublishedAt;
        await _db.SaveChangesAsync(cancellationToken);
        return version.Id;
    }
}

public sealed record CreateSurchargeVersionCommand(Guid SurchargeId) : IRequest<Guid>;

public sealed class CreateSurchargeVersionCommandHandler : IRequestHandler<CreateSurchargeVersionCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public CreateSurchargeVersionCommandHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateSurchargeVersionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenant.TenantId!.Value;
        var surcharge = await _db.Surcharges.FirstOrDefaultAsync(s => s.Id == request.SurchargeId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phụ phí.");
        await SurchargeWriteGuard.EnsureDirectionAsync(_db, _permissions, surcharge.Direction, write: true, cancellationToken);
        var latest = await _db.SurchargeVersions
            .Where(v => v.SurchargeId == surcharge.Id)
            .OrderByDescending(v => v.VersionNo)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ConflictAppException("Phụ phí chưa có phiên bản để sao chép.");

        var next = new SurchargeVersion
        {
            TenantId = tenantId,
            SurchargeId = surcharge.Id,
            VersionNo = latest.VersionNo + 1,
            PublishStatus = SurchargeVersionStatuses.Draft,
            ValidFrom = latest.ValidFrom,
            ValidTo = latest.ValidTo,
            VatRate = latest.VatRate
        };
        _db.SurchargeVersions.Add(next);
        await SurchargeGraph.CloneRulesAsync(_db, tenantId, latest.Id, next.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return next.Id;
    }
}

internal static class SurchargeWriteGuard
{
    public static async Task EnsureDirectionAsync(
        ILcmsDbContext db,
        IPermissionService permissions,
        string direction,
        bool write,
        CancellationToken cancellationToken)
    {
        if (string.Equals(direction, SurchargeDirections.Both, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureBothAllowedAsync(db, cancellationToken);
            if (write)
            {
                await permissions.EnsureAsync(PermissionCodes.RateBuyWrite, "Bạn không có quyền sửa phụ phí mua.", cancellationToken);
                await permissions.EnsureAsync(PermissionCodes.RateSellWrite, "Bạn không có quyền sửa phụ phí bán.", cancellationToken);
            }
            else
            {
                await permissions.EnsureAsync(PermissionCodes.RateBuyPublish, "Bạn không có quyền phát hành phụ phí mua.", cancellationToken);
                await permissions.EnsureAsync(PermissionCodes.RateSellPublish, "Bạn không có quyền phát hành phụ phí bán.", cancellationToken);
            }

            return;
        }

        var sell = string.Equals(direction, SurchargeDirections.Sell, StringComparison.OrdinalIgnoreCase);
        var code = write
            ? sell ? PermissionCodes.RateSellWrite : PermissionCodes.RateBuyWrite
            : sell ? PermissionCodes.RateSellPublish : PermissionCodes.RateBuyPublish;
        await permissions.EnsureAsync(code, "Bạn không có quyền cập nhật phụ phí này.", cancellationToken);
    }

    private static async Task EnsureBothAllowedAsync(ILcmsDbContext db, CancellationToken cancellationToken)
    {
        var body = await db.Policies.AsNoTracking()
            .Where(p => p.PolicyKey == PolicyKeys.Rating && p.Status == PolicyStatuses.Active)
            .OrderByDescending(p => p.Version)
            .Select(p => p.BodyJson)
            .FirstOrDefaultAsync(cancellationToken);
        if (!AllowsBoth(body))
        {
            throw new ConflictAppException(
                "Chiều both chỉ dùng khi chính sách tính giá bật allowBothDirection. Hãy chọn mua hoặc bán.");
        }
    }

    private static bool AllowsBoth(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("allowBothDirection", out var value)
                && value.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

internal static class SurchargePartnerRules
{
    public static void Ensure(string direction, SurchargeRuleInput rule)
    {
        var normalized = direction.Trim().ToLowerInvariant();
        var hasGroup = !string.IsNullOrWhiteSpace(rule.CustomerGroupCode);
        if (normalized == "buy" && (rule.CustomerPartyId is not null || hasGroup))
        {
            throw new ConflictAppException("Phụ phí mua chỉ gắn nhà cung cấp.");
        }

        if (normalized == "sell" && rule.VendorPartyId is not null)
        {
            throw new ConflictAppException("Phụ phí bán không gắn nhà cung cấp.");
        }

        if (rule.CustomerPartyId is not null && hasGroup)
        {
            throw new ConflictAppException("Chọn khách hàng hoặc nhóm khách hàng, không cả hai.");
        }
    }
}

internal static class SurchargeGraph
{
    public static SurchargeRule AddRule(ILcmsDbContext db, Guid tenantId, Guid versionId, SurchargeRuleInput input)
    {
        var rule = new SurchargeRule
        {
            TenantId = tenantId,
            SurchargeVersionId = versionId,
            CalculationMode = CreateSurchargeCommandValidator.NormalizeMode(input.CalculationMode),
            Basis = Clean(input.Basis),
            CurrencyCode = input.CurrencyCode.Trim().ToUpperInvariant(),
            RateAmountPercent = input.RateAmountPercent,
            Priority = input.Priority ?? 0,
            MinAmount = input.MinAmount,
            MaxAmount = input.MaxAmount,
            ContainerType = Clean(input.ContainerType)
        };
        db.SurchargeRules.Add(rule);
        AddCondition(db, tenantId, rule.Id, "transport_mode", input.TransportMode);
        AddCondition(db, tenantId, rule.Id, "service", input.ServiceTypeCode);
        AddCondition(db, tenantId, rule.Id, "route", input.RouteCode);
        AddCondition(db, tenantId, rule.Id, "origin", input.OriginCode);
        AddCondition(db, tenantId, rule.Id, "destination", input.DestinationCode);
        AddCondition(db, tenantId, rule.Id, "commodity", input.CommodityCode);
        if (input.DangerousGoods is bool dg)
        {
            db.SurchargeConditions.Add(new SurchargeCondition
            {
                TenantId = tenantId,
                SurchargeRuleId = rule.Id,
                Dimension = "dangerous_goods",
                Operator = "eq",
                ValueText = dg ? "true" : "false"
            });
        }

        if (input.WeightFrom is not null || input.WeightTo is not null)
        {
            db.SurchargeConditions.Add(new SurchargeCondition
            {
                TenantId = tenantId,
                SurchargeRuleId = rule.Id,
                Dimension = "chargeable_weight",
                Operator = "between",
                ValueFrom = input.WeightFrom,
                ValueTo = input.WeightTo
            });
        }

        if (input.RateCardId is not null || input.RateVersionId is not null
            || input.VendorPartyId is not null || input.CustomerPartyId is not null
            || !string.IsNullOrWhiteSpace(input.CustomerGroupCode))
        {
            db.SurchargeScopes.Add(new SurchargeScope
            {
                TenantId = tenantId,
                SurchargeRuleId = rule.Id,
                RateCardId = input.RateCardId,
                RateVersionId = input.RateVersionId,
                VendorPartyId = input.VendorPartyId,
                CustomerPartyId = input.CustomerPartyId,
                CustomerGroupCode = string.IsNullOrWhiteSpace(input.CustomerGroupCode) ? null : input.CustomerGroupCode.Trim()
            });
        }

        var sequence = 1;
        foreach (var band in input.Breaks ?? [])
        {
            db.SurchargeBreaks.Add(new SurchargeBreak
            {
                TenantId = tenantId,
                SurchargeRuleId = rule.Id,
                SequenceNo = band.SequenceNo == 0 ? sequence : band.SequenceNo,
                MinQuantity = band.MinQuantity,
                MaxQuantity = band.MaxQuantity,
                UnitAmount = band.UnitAmount
            });
            sequence++;
        }

        return rule;
    }

    public static void AddPublished(
        ILcmsDbContext db,
        Guid tenantId,
        string code,
        string name,
        string direction,
        DateTimeOffset? validFrom,
        SurchargeRuleInput rule)
    {
        var surcharge = new Surcharge
        {
            TenantId = tenantId,
            Code = code.Trim(),
            Name = name.Trim(),
            Direction = direction,
            Status = SurchargeStatuses.Active
        };
        var version = new SurchargeVersion
        {
            TenantId = tenantId,
            SurchargeId = surcharge.Id,
            VersionNo = 1,
            PublishStatus = SurchargeVersionStatuses.Published,
            ValidFrom = validFrom,
            PublishedAt = validFrom ?? DateTimeOffset.UtcNow
        };
        db.Surcharges.Add(surcharge);
        db.SurchargeVersions.Add(version);
        AddRule(db, tenantId, version.Id, rule);
    }

    public static async Task ReplaceRulesAsync(
        ILcmsDbContext db,
        Guid tenantId,
        Guid versionId,
        SurchargeRuleInput input,
        CancellationToken cancellationToken)
    {
        var rules = await db.SurchargeRules.Where(r => r.SurchargeVersionId == versionId).ToListAsync(cancellationToken);
        var ruleIds = rules.Select(r => r.Id).ToList();
        if (ruleIds.Count > 0)
        {
            var conditions = await db.SurchargeConditions.Where(c => ruleIds.Contains(c.SurchargeRuleId)).ToListAsync(cancellationToken);
            var scopes = await db.SurchargeScopes.Where(s => ruleIds.Contains(s.SurchargeRuleId)).ToListAsync(cancellationToken);
            var breaks = await db.SurchargeBreaks.Where(b => ruleIds.Contains(b.SurchargeRuleId)).ToListAsync(cancellationToken);
            foreach (var row in conditions.Concat<EntityBase>(scopes).Concat(breaks).Concat(rules))
            {
                row.SoftDelete(null);
            }
        }

        AddRule(db, tenantId, versionId, input);
    }

    public static async Task CloneRulesAsync(
        ILcmsDbContext db,
        Guid tenantId,
        Guid sourceVersionId,
        Guid targetVersionId,
        CancellationToken cancellationToken)
    {
        var rules = await db.SurchargeRules.AsNoTracking()
            .Where(r => r.SurchargeVersionId == sourceVersionId)
            .ToListAsync(cancellationToken);
        var ruleIds = rules.Select(r => r.Id).ToList();
        var conditions = await db.SurchargeConditions.AsNoTracking().Where(c => ruleIds.Contains(c.SurchargeRuleId)).ToListAsync(cancellationToken);
        var scopes = await db.SurchargeScopes.AsNoTracking().Where(s => ruleIds.Contains(s.SurchargeRuleId)).ToListAsync(cancellationToken);
        var breaks = await db.SurchargeBreaks.AsNoTracking().Where(b => ruleIds.Contains(b.SurchargeRuleId)).ToListAsync(cancellationToken);
        foreach (var source in rules)
        {
            var copy = new SurchargeRule
            {
                TenantId = tenantId,
                SurchargeVersionId = targetVersionId,
                CalculationMode = source.CalculationMode,
                Basis = source.Basis,
                CurrencyCode = source.CurrencyCode,
                RateAmountPercent = source.RateAmountPercent,
                Priority = source.Priority,
                MinAmount = source.MinAmount,
                MaxAmount = source.MaxAmount,
                ContainerType = source.ContainerType
            };
            db.SurchargeRules.Add(copy);
            foreach (var condition in conditions.Where(c => c.SurchargeRuleId == source.Id))
            {
                db.SurchargeConditions.Add(new SurchargeCondition
                {
                    TenantId = tenantId,
                    SurchargeRuleId = copy.Id,
                    Dimension = condition.Dimension,
                    Operator = condition.Operator,
                    ValueText = condition.ValueText,
                    ValueFrom = condition.ValueFrom,
                    ValueTo = condition.ValueTo
                });
            }

            foreach (var scope in scopes.Where(s => s.SurchargeRuleId == source.Id))
            {
                db.SurchargeScopes.Add(new SurchargeScope
                {
                    TenantId = tenantId,
                    SurchargeRuleId = copy.Id,
                    RateCardId = scope.RateCardId,
                    RateVersionId = scope.RateVersionId,
                    VendorPartyId = scope.VendorPartyId,
                    CustomerPartyId = scope.CustomerPartyId,
                    CustomerGroupCode = scope.CustomerGroupCode,
                    ServiceTypeCode = scope.ServiceTypeCode,
                    RouteCode = scope.RouteCode,
                    TransportMode = scope.TransportMode
                });
            }

            foreach (var band in breaks.Where(b => b.SurchargeRuleId == source.Id))
            {
                db.SurchargeBreaks.Add(new SurchargeBreak
                {
                    TenantId = tenantId,
                    SurchargeRuleId = copy.Id,
                    SequenceNo = band.SequenceNo,
                    MinQuantity = band.MinQuantity,
                    MaxQuantity = band.MaxQuantity,
                    UnitAmount = band.UnitAmount
                });
            }
        }
    }

    private static void AddCondition(ILcmsDbContext db, Guid tenantId, Guid ruleId, string dimension, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        db.SurchargeConditions.Add(new SurchargeCondition
        {
            TenantId = tenantId,
            SurchargeRuleId = ruleId,
            Dimension = dimension,
            Operator = "eq",
            ValueText = value.Trim()
        });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
