using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.PricingRules.Commands;

public sealed record AddPricingRuleCommand(
    Guid RateVersionId,
    string Code,
    string Name,
    string CalcMethod,
    decimal UnitAmount,
    string CurrencyCode,
    string? Applicability,
    string? ServiceTypeCode,
    string? PartyTypeCode,
    string? RouteCode,
    decimal? MinAmount,
    decimal? MaxAmount,
    int SortOrder,
    string? ChargeCode = null,
    string? TransportMode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? CommodityCode = null,
    decimal? VolumetricFactor = null,
    decimal? RoundingStep = null) : IRequest<Guid>;

public sealed class AddPricingRuleCommandValidator : AbstractValidator<AddPricingRuleCommand>
{
    public AddPricingRuleCommandValidator()
    {
        RuleFor(x => x.RateVersionId).NotEmpty().WithMessage("Phiên bản bảng giá không hợp lệ.");
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã quy tắc không được để trống.")
            .MaximumLength(64).WithMessage("Mã quy tắc không được vượt quá 64 ký tự.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên quy tắc không được để trống.")
            .MaximumLength(256).WithMessage("Tên quy tắc không được vượt quá 256 ký tự.");
        RuleFor(x => x.CalcMethod)
            .NotEmpty().WithMessage("Phương pháp tính giá không được để trống.")
            .Must(m => PricingCalcMethods.All.Contains(m))
            .WithMessage("Phương pháp tính giá không hợp lệ.");
        RuleFor(x => x.UnitAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Đơn giá / số tiền không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.Applicability)
            .MaximumLength(512)
            .When(x => x.Applicability is not null);
        RuleFor(x => x.ServiceTypeCode)
            .MaximumLength(64)
            .When(x => x.ServiceTypeCode is not null);
        RuleFor(x => x.PartyTypeCode)
            .MaximumLength(32)
            .When(x => x.PartyTypeCode is not null);
        RuleFor(x => x.RouteCode)
            .MaximumLength(64)
            .When(x => x.RouteCode is not null);
        RuleFor(x => x.MinAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền tối thiểu không được âm.")
            .When(x => x.MinAmount.HasValue);
        RuleFor(x => x.MaxAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền tối đa không được âm.")
            .When(x => x.MaxAmount.HasValue);
        RuleFor(x => x)
            .Must(x => !x.MinAmount.HasValue || !x.MaxAmount.HasValue || x.MinAmount <= x.MaxAmount)
            .WithMessage("Số tiền tối thiểu không được lớn hơn số tiền tối đa.");
        RuleFor(x => x)
            .Must(x => !string.Equals(x.CalcMethod, PricingCalcMethods.MinMaxClamp, StringComparison.OrdinalIgnoreCase)
                       || x.MinAmount.HasValue
                       || x.MaxAmount.HasValue)
            .WithMessage("min_max_clamp yêu cầu ít nhất một trong minAmount hoặc maxAmount.");
    }
}

public sealed class AddPricingRuleCommandHandler : IRequestHandler<AddPricingRuleCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AddPricingRuleCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(AddPricingRuleCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var version = await _db.RateVersions.FirstOrDefaultAsync(v => v.Id == request.RateVersionId, cancellationToken);
        if (version is null)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        EnsureDraft(version);

        var code = request.Code.Trim();
        if (await _db.PricingRules.AnyAsync(
                r => r.RateVersionId == version.Id && r.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã quy tắc đã tồn tại trên phiên bản này.");
        }

        var rule = new PricingRule
        {
            TenantId = tenantId,
            RateVersionId = version.Id,
            Code = code,
            Name = request.Name.Trim(),
            CalcMethod = request.CalcMethod.Trim().ToLowerInvariant(),
            UnitAmount = request.UnitAmount,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            Applicability = string.IsNullOrWhiteSpace(request.Applicability) ? null : request.Applicability.Trim(),
            ServiceTypeCode = string.IsNullOrWhiteSpace(request.ServiceTypeCode) ? null : request.ServiceTypeCode.Trim(),
            PartyTypeCode = string.IsNullOrWhiteSpace(request.PartyTypeCode) ? null : request.PartyTypeCode.Trim().ToLowerInvariant(),
            RouteCode = string.IsNullOrWhiteSpace(request.RouteCode) ? null : request.RouteCode.Trim(),
            MinAmount = request.MinAmount,
            MaxAmount = request.MaxAmount,
            ChargeCode = string.IsNullOrWhiteSpace(request.ChargeCode) ? null : request.ChargeCode.Trim(),
            TransportMode = string.IsNullOrWhiteSpace(request.TransportMode) ? null : request.TransportMode.Trim(),
            OriginCode = string.IsNullOrWhiteSpace(request.OriginCode) ? null : request.OriginCode.Trim(),
            DestinationCode = string.IsNullOrWhiteSpace(request.DestinationCode) ? null : request.DestinationCode.Trim(),
            CommodityCode = string.IsNullOrWhiteSpace(request.CommodityCode) ? null : request.CommodityCode.Trim(),
            VolumetricFactor = request.VolumetricFactor,
            RoundingStep = request.RoundingStep,
            SortOrder = request.SortOrder,
            IsActive = true
        };

        _db.PricingRules.Add(rule);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Mã quy tắc đã tồn tại trên phiên bản này.");
        }

        return rule.Id;
    }

    internal static void EnsureDraft(RateVersion version)
    {
        if (version.IsPublished)
        {
            throw new ConflictAppException(
                "Phiên bản bảng giá đã phát hành không được sửa. Hãy tạo phiên bản mới.");
        }
    }
}

public sealed record AddPricingRuleComponentCommand(
    Guid PricingRuleId,
    string Code,
    string Name,
    string FinancialNature,
    string? CostTypeCode,
    string? RevenueTypeCode,
    decimal Amount,
    string CurrencyCode,
    int SortOrder,
    string? CalcMethod = null,
    string? DependsOnCode = null) : IRequest<Guid>;

public sealed class AddPricingRuleComponentCommandValidator : AbstractValidator<AddPricingRuleComponentCommand>
{
    public AddPricingRuleComponentCommandValidator()
    {
        RuleFor(x => x.PricingRuleId).NotEmpty().WithMessage("Quy tắc tính giá không hợp lệ.");
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã thành phần không được để trống.")
            .MaximumLength(64).WithMessage("Mã thành phần không được vượt quá 64 ký tự.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên thành phần không được để trống.")
            .MaximumLength(256).WithMessage("Tên thành phần không được vượt quá 256 ký tự.");
        RuleFor(x => x.FinancialNature)
            .NotEmpty().WithMessage("Tính chất tài chính không được để trống.")
            .Must(n => n is "cost" or "revenue")
            .WithMessage("Tính chất tài chính phải là cost hoặc revenue.");
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền thành phần không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.CostTypeCode)
            .MaximumLength(64)
            .When(x => x.CostTypeCode is not null);
        RuleFor(x => x.RevenueTypeCode)
            .MaximumLength(64)
            .When(x => x.RevenueTypeCode is not null);
    }
}

public sealed class AddPricingRuleComponentCommandHandler : IRequestHandler<AddPricingRuleComponentCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AddPricingRuleComponentCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(AddPricingRuleComponentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        var rule = await _db.PricingRules.FirstOrDefaultAsync(r => r.Id == request.PricingRuleId, cancellationToken);
        if (rule is null)
        {
            throw new NotFoundAppException("Không tìm thấy quy tắc tính giá.");
        }

        var version = await _db.RateVersions.FirstOrDefaultAsync(v => v.Id == rule.RateVersionId, cancellationToken);
        if (version is null)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        AddPricingRuleCommandHandler.EnsureDraft(version);

        var nature = request.FinancialNature.Trim().ToLowerInvariant();
        if (nature == "cost")
        {
            await EnsureCatalogCodeAsync(
                MasterCatalogKinds.CostType, request.CostTypeCode, "Loại chi phí", cancellationToken);
        }
        else if (nature == "revenue")
        {
            await EnsureCatalogCodeAsync(
                MasterCatalogKinds.RevenueType, request.RevenueTypeCode, "Loại doanh thu", cancellationToken);
        }

        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var currencyRow = await _db.Currencies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == currency && c.IsActive, cancellationToken);
        if (currencyRow is null)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["currencyCode"] = [$"Tiền tệ {currency} không có trong danh mục hoặc đã ngừng dùng."]
            });
        }

        var code = request.Code.Trim();
        if (await _db.PricingRuleComponents.AnyAsync(
                c => c.PricingRuleId == rule.Id && c.Code == code, cancellationToken))
        {
            throw new ConflictAppException("Mã thành phần đã tồn tại trên quy tắc này.");
        }

        var component = new PricingRuleComponent
        {
            TenantId = tenantId,
            PricingRuleId = rule.Id,
            Code = code,
            Name = request.Name.Trim(),
            FinancialNature = nature,
            CostTypeCode = nature == "cost" && !string.IsNullOrWhiteSpace(request.CostTypeCode)
                ? request.CostTypeCode.Trim()
                : null,
            RevenueTypeCode = nature == "revenue" && !string.IsNullOrWhiteSpace(request.RevenueTypeCode)
                ? request.RevenueTypeCode.Trim()
                : null,
            Amount = request.Amount,
            CurrencyCode = currency,
            SortOrder = request.SortOrder,
            CalcMethod = string.IsNullOrWhiteSpace(request.CalcMethod) ? null : request.CalcMethod.Trim().ToLowerInvariant(),
            DependsOnCode = string.IsNullOrWhiteSpace(request.DependsOnCode) ? null : request.DependsOnCode.Trim()
        };

        _db.PricingRuleComponents.Add(component);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Mã thành phần đã tồn tại trên quy tắc này.");
        }

        return component.Id;
    }

    private async Task EnsureCatalogCodeAsync(
        string kind,
        string? code,
        string label,
        CancellationToken cancellationToken)
    {
        var codes = await _db.MasterCatalogItems.AsNoTracking()
            .Where(i => i.Kind == kind && i.IsActive)
            .Select(i => i.Code)
            .ToListAsync(cancellationToken);
        if (codes.Count == 0)
        {
            return;
        }

        var normalized = code?.Trim() ?? "";
        if (normalized.Length == 0
            || !codes.Any(c => string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["typeCode"] = [$"{label} phải chọn từ danh mục đang dùng."]
            });
        }
    }
}

public sealed record UpdatePricingRuleComponentCommand(
    Guid ComponentId,
    string Name,
    string FinancialNature,
    string? CostTypeCode,
    string? RevenueTypeCode,
    decimal Amount,
    string CurrencyCode) : IRequest;

public sealed class UpdatePricingRuleComponentCommandValidator : AbstractValidator<UpdatePricingRuleComponentCommand>
{
    public UpdatePricingRuleComponentCommandValidator()
    {
        RuleFor(x => x.ComponentId).NotEmpty().WithMessage("Thành phần không hợp lệ.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên thành phần không được để trống.")
            .MaximumLength(256).WithMessage("Tên thành phần không được vượt quá 256 ký tự.");
        RuleFor(x => x.FinancialNature)
            .Must(n => n is "cost" or "revenue")
            .WithMessage("Tính chất tài chính phải là cost hoặc revenue.");
        RuleFor(x => x.Amount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền thành phần không được âm.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
    }
}

public sealed class UpdatePricingRuleComponentCommandHandler : IRequestHandler<UpdatePricingRuleComponentCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpdatePricingRuleComponentCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(UpdatePricingRuleComponentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var component = await _db.PricingRuleComponents
            .FirstOrDefaultAsync(c => c.Id == request.ComponentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thành phần giá.");
        var rule = await _db.PricingRules.FirstOrDefaultAsync(r => r.Id == component.PricingRuleId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy quy tắc tính giá.");
        var version = await _db.RateVersions.FirstOrDefaultAsync(v => v.Id == rule.RateVersionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        AddPricingRuleCommandHandler.EnsureDraft(version);

        component.Name = request.Name.Trim();
        component.FinancialNature = request.FinancialNature.Trim().ToLowerInvariant();
        component.CostTypeCode = string.IsNullOrWhiteSpace(request.CostTypeCode) ? null : request.CostTypeCode.Trim();
        component.RevenueTypeCode = string.IsNullOrWhiteSpace(request.RevenueTypeCode) ? null : request.RevenueTypeCode.Trim();
        component.Amount = request.Amount;
        component.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeletePricingRuleComponentCommand(Guid ComponentId) : IRequest;

public sealed class DeletePricingRuleComponentCommandHandler : IRequestHandler<DeletePricingRuleComponentCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public DeletePricingRuleComponentCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(DeletePricingRuleComponentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var component = await _db.PricingRuleComponents
            .FirstOrDefaultAsync(c => c.Id == request.ComponentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thành phần giá.");
        var rule = await _db.PricingRules.FirstOrDefaultAsync(r => r.Id == component.PricingRuleId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy quy tắc tính giá.");
        var version = await _db.RateVersions.FirstOrDefaultAsync(v => v.Id == rule.RateVersionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        AddPricingRuleCommandHandler.EnsureDraft(version);

        component.SoftDelete(null);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record PricingRuleComponentDto(
    Guid Id,
    Guid PricingRuleId,
    string Code,
    string Name,
    string FinancialNature,
    string? CostTypeCode,
    string? RevenueTypeCode,
    decimal Amount,
    string CurrencyCode,
    int SortOrder);

public sealed record RateBreakDto(
    Guid Id,
    Guid PricingRuleId,
    int SequenceNo,
    decimal MinQuantity,
    decimal? MaxQuantity,
    decimal UnitAmount);

public sealed record ContainerRateDto(
    Guid Id,
    Guid PricingRuleId,
    string ContainerType,
    decimal UnitAmount);

public sealed record PricingRuleDto(
    Guid Id,
    Guid RateVersionId,
    string Code,
    string Name,
    string CalcMethod,
    decimal UnitAmount,
    string CurrencyCode,
    string? Applicability,
    string? ServiceTypeCode,
    string? PartyTypeCode,
    string? RouteCode,
    decimal? MinAmount,
    decimal? MaxAmount,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<PricingRuleComponentDto> Components,
    IReadOnlyList<RateBreakDto> Breaks,
    IReadOnlyList<ContainerRateDto> ContainerRates,
    string? ChargeCode = null,
    string? CommodityCode = null,
    string? DestinationCode = null);

public sealed record ListPricingRulesQuery(Guid RateVersionId) : IRequest<IReadOnlyList<PricingRuleDto>>;

public sealed class ListPricingRulesQueryHandler : IRequestHandler<ListPricingRulesQuery, IReadOnlyList<PricingRuleDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListPricingRulesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<PricingRuleDto>> Handle(
        ListPricingRulesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var versionExists = await _db.RateVersions.AnyAsync(v => v.Id == request.RateVersionId, cancellationToken);
        if (!versionExists)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        var rules = await _db.PricingRules
            .AsNoTracking()
            .Where(r => r.RateVersionId == request.RateVersionId)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .ToListAsync(cancellationToken);

        var ruleIds = rules.Select(r => r.Id).ToList();
        var components = await _db.PricingRuleComponents
            .AsNoTracking()
            .Where(c => ruleIds.Contains(c.PricingRuleId))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);
        var byRule = components
            .GroupBy(c => c.PricingRuleId)
            .ToDictionary(g => g.Key, g => g.Select(c => new PricingRuleComponentDto(
                c.Id, c.PricingRuleId, c.Code, c.Name, c.FinancialNature,
                c.CostTypeCode, c.RevenueTypeCode, c.Amount, c.CurrencyCode, c.SortOrder))
                .ToList());

        var breaks = ruleIds.Count == 0
            ? []
            : await _db.RateBreaks
                .AsNoTracking()
                .Where(b => ruleIds.Contains(b.PricingRuleId))
                .OrderBy(b => b.SequenceNo)
                .ToListAsync(cancellationToken);
        var breaksByRule = breaks
            .GroupBy(b => b.PricingRuleId)
            .ToDictionary(g => g.Key, g => g.Select(b => new RateBreakDto(
                b.Id, b.PricingRuleId, b.SequenceNo, b.MinQuantity, b.MaxQuantity, b.UnitAmount))
                .ToList());

        var containerRates = ruleIds.Count == 0
            ? []
            : await _db.ContainerRatePrices
                .AsNoTracking()
                .Where(c => ruleIds.Contains(c.PricingRuleId))
                .OrderBy(c => c.ContainerType)
                .ToListAsync(cancellationToken);
        var containersByRule = containerRates
            .GroupBy(c => c.PricingRuleId)
            .ToDictionary(g => g.Key, g => g.Select(c => new ContainerRateDto(
                c.Id, c.PricingRuleId, c.ContainerType, c.UnitAmount))
                .ToList());

        return rules
            .Select(r => new PricingRuleDto(
                r.Id, r.RateVersionId, r.Code, r.Name, r.CalcMethod,
                r.UnitAmount, r.CurrencyCode, r.Applicability,
                r.ServiceTypeCode, r.PartyTypeCode, r.RouteCode,
                r.MinAmount, r.MaxAmount, r.SortOrder, r.IsActive,
                byRule.TryGetValue(r.Id, out var list) ? list : [],
                breaksByRule.TryGetValue(r.Id, out var br) ? br : [],
                containersByRule.TryGetValue(r.Id, out var cr) ? cr : [],
                r.ChargeCode,
                r.CommodityCode,
                r.DestinationCode))
            .ToList();
    }
}
