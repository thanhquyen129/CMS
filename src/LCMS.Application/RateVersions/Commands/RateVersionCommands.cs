using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.RateCards;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateVersions.Commands;

public sealed record CreateRateVersionCommand(
    Guid RateCardId,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Note,
    decimal? VatRate = null,
    Guid? CloneFromVersionId = null) : IRequest<Guid>;

public sealed class CreateRateVersionCommandValidator : AbstractValidator<CreateRateVersionCommand>
{
    public CreateRateVersionCommandValidator()
    {
        RuleFor(x => x.RateCardId).NotEmpty().WithMessage("Bảng giá không hợp lệ.");
        RuleFor(x => x.Note)
            .MaximumLength(1024)
            .When(x => x.Note is not null);
        RuleFor(x => x)
            .Must(x => x.EffectiveTo is null || x.EffectiveFrom is null || x.EffectiveTo >= x.EffectiveFrom)
            .WithMessage("Ngày hiệu lực đến phải sau hoặc bằng ngày hiệu lực từ.");
        RuleFor(x => x.VatRate)
            .Must(DeclaredVat.IsValid)
            .WithMessage("Thuế suất VAT phải từ 0 đến 100, hoặc để trống nếu chưa khai báo.");
    }
}

public sealed class CreateRateVersionCommandHandler : IRequestHandler<CreateRateVersionCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public CreateRateVersionCommandHandler(
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

    public async Task<Guid> Handle(CreateRateVersionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        var card = await _db.RateCards.FirstOrDefaultAsync(c => c.Id == request.RateCardId, cancellationToken);
        if (card is null)
        {
            throw new NotFoundAppException("Không tìm thấy bảng giá.");
        }

        var writeCode = string.Equals(card.PartyType, "customer", StringComparison.OrdinalIgnoreCase)
            ? PermissionCodes.RateSellWrite
            : PermissionCodes.RateBuyWrite;
        await _permissions.EnsureAsync(writeCode, "Bạn không có quyền tạo phiên bản bảng giá này.", cancellationToken);

        var existingDraft = await _db.RateVersions
            .FirstOrDefaultAsync(v => v.RateCardId == request.RateCardId && v.Status == RateVersionStatuses.Draft, cancellationToken);
        if (existingDraft != null)
        {
            return existingDraft.Id;
        }

        var nextNo = await _db.RateVersions
            .Where(v => v.RateCardId == request.RateCardId)
            .Select(v => (int?)v.VersionNo)
            .MaxAsync(cancellationToken) ?? 0;

        RateVersion? sourceVersion = null;
        if (request.CloneFromVersionId.HasValue)
        {
            sourceVersion = await _db.RateVersions
                .FirstOrDefaultAsync(v => v.Id == request.CloneFromVersionId.Value && v.RateCardId == request.RateCardId, cancellationToken);
        }
        else
        {
            sourceVersion = await _db.RateVersions
                .Where(v => v.RateCardId == request.RateCardId && v.Status == RateVersionStatuses.Published)
                .OrderByDescending(v => v.VersionNo)
                .FirstOrDefaultAsync(cancellationToken);
        }


        var version = new RateVersion
        {
            TenantId = tenantId,
            RateCardId = request.RateCardId,
            VersionNo = nextNo + 1,
            Status = RateVersionStatuses.Draft,
            EffectiveFrom = request.EffectiveFrom ?? sourceVersion?.EffectiveFrom,
            EffectiveTo = request.EffectiveTo ?? sourceVersion?.EffectiveTo,
            Note = string.IsNullOrWhiteSpace(request.Note) ? sourceVersion?.Note : request.Note.Trim(),
            VatRate = request.VatRate ?? sourceVersion?.VatRate,
            ExplicitChargeGroups = sourceVersion?.ExplicitChargeGroups ?? false
        };

        _db.RateVersions.Add(version);

        if (sourceVersion != null)
        {
            var sourceRules = await _db.PricingRules
                .Where(r => r.RateVersionId == sourceVersion.Id && r.DeletedAt == null)
                .ToListAsync(cancellationToken);

            var sourceRuleIds = sourceRules.Select(r => r.Id).ToList();

            var sourceComponents = await _db.PricingRuleComponents
                .Where(c => sourceRuleIds.Contains(c.PricingRuleId) && c.DeletedAt == null)
                .ToListAsync(cancellationToken);

            var sourceBreaks = await _db.RateBreaks
                .Where(b => sourceRuleIds.Contains(b.PricingRuleId) && b.DeletedAt == null)
                .ToListAsync(cancellationToken);

            var sourceContainers = await _db.ContainerRatePrices
                .Where(c => sourceRuleIds.Contains(c.PricingRuleId) && c.DeletedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var rule in sourceRules)
            {
                var newRule = new PricingRule
                {
                    TenantId = tenantId,
                    RateVersionId = version.Id,
                    Code = rule.Code,
                    Name = rule.Name,
                    CalcMethod = rule.CalcMethod,
                    UnitAmount = rule.UnitAmount,
                    CurrencyCode = rule.CurrencyCode,
                    Applicability = rule.Applicability,
                    ServiceTypeCode = rule.ServiceTypeCode,
                    PartyTypeCode = rule.PartyTypeCode,
                    RouteId = rule.RouteId,
                    AppliesAnyRoute = rule.AppliesAnyRoute,
                    RouteCode = rule.RouteCode,
                    CarrierPartyId = rule.CarrierPartyId,
                    CarrierName = rule.CarrierName,
                    WeightBasis = rule.WeightBasis,
                    WeightMin = rule.WeightMin,
                    WeightMax = rule.WeightMax,
                    MatrixGroupCode = rule.MatrixGroupCode,
                    MinAmount = rule.MinAmount,
                    MaxAmount = rule.MaxAmount,
                    ChargeCode = rule.ChargeCode,
                    LineRole = rule.LineRole,
                    TransportMode = rule.TransportMode,
                    OriginCode = rule.OriginCode,
                    DestinationCode = rule.DestinationCode,
                    CommodityCode = rule.CommodityCode,
                    VolumetricFactor = rule.VolumetricFactor,
                    RoundingStep = rule.RoundingStep,
                    SortOrder = rule.SortOrder,
                    IsActive = true
                };
                _db.PricingRules.Add(newRule);

                var comps = sourceComponents.Where(c => c.PricingRuleId == rule.Id);
                foreach (var c in comps)
                {
                    _db.PricingRuleComponents.Add(new PricingRuleComponent
                    {
                        TenantId = tenantId,
                        PricingRuleId = newRule.Id,
                        Code = c.Code,
                        Name = c.Name,
                        FinancialNature = c.FinancialNature,
                        CostTypeCode = c.CostTypeCode,
                        RevenueTypeCode = c.RevenueTypeCode,
                        Amount = c.Amount,
                        CurrencyCode = c.CurrencyCode,
                        SortOrder = c.SortOrder,
                        CalcMethod = c.CalcMethod,
                        DependsOnCode = c.DependsOnCode,
                        EconomicChargeTypeId = c.EconomicChargeTypeId
                    });
                }

                var brks = sourceBreaks.Where(b => b.PricingRuleId == rule.Id);
                foreach (var b in brks)
                {
                    _db.RateBreaks.Add(new RateBreak
                    {
                        TenantId = tenantId,
                        PricingRuleId = newRule.Id,
                        SequenceNo = b.SequenceNo,
                        MinQuantity = b.MinQuantity,
                        MaxQuantity = b.MaxQuantity,
                        UnitAmount = b.UnitAmount
                    });
                }

                var cnts = sourceContainers.Where(c => c.PricingRuleId == rule.Id);
                foreach (var c in cnts)
                {
                    _db.ContainerRatePrices.Add(new ContainerRatePrice
                    {
                        TenantId = tenantId,
                        PricingRuleId = newRule.Id,
                        ContainerType = c.ContainerType,
                        UnitAmount = c.UnitAmount
                    });
                }
            }
        }

        _audit.Append(
            AuditActions.RateCardVersionCreated,
            AuditObjectTypes.RateCard,
            request.RateCardId,
            null,
            version.VersionNo.ToString(),
            sourceVersion != null ? $"Cloned from v{sourceVersion.VersionNo}" : null);
        await _db.SaveChangesAsync(cancellationToken);
        return version.Id;
    }
}

public sealed record SetDraftRateVersionVatCommand(Guid Id, decimal? VatRate) : IRequest;

public sealed class SetDraftRateVersionVatCommandValidator : AbstractValidator<SetDraftRateVersionVatCommand>
{
    public SetDraftRateVersionVatCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.VatRate)
            .Must(DeclaredVat.IsValid)
            .WithMessage("Thuế suất VAT phải từ 0 đến 100, hoặc để trống nếu chưa khai báo.");
    }
}

public sealed class SetDraftRateVersionVatCommandHandler : IRequestHandler<SetDraftRateVersionVatCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public SetDraftRateVersionVatCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(SetDraftRateVersionVatCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var version = await _db.RateVersions.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        if (version.IsPublished)
        {
            throw new ConflictAppException("Phiên bản đã phát hành bất biến. Hãy tạo phiên bản mới để khai thuế suất VAT.");
        }

        version.VatRate = request.VatRate;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record PublishRateVersionCommand(Guid Id) : IRequest;

public sealed class PublishRateVersionCommandHandler : IRequestHandler<PublishRateVersionCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;

    public PublishRateVersionCommandHandler(
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

    public async Task Handle(PublishRateVersionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var version = await _db.RateVersions.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);
        if (version is null)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        if (version.IsPublished)
        {
            throw new ConflictAppException("Phiên bản bảng giá đã được phát hành.");
        }

        var hasRules = await _db.PricingRules.AnyAsync(r => r.RateVersionId == version.Id, cancellationToken);
        if (!hasRules)
        {
            throw new ConflictAppException("Phiên bản bảng giá phải có ít nhất một quy tắc tính giá trước khi phát hành.");
        }

        var card = await _db.RateCards.FirstOrDefaultAsync(c => c.Id == version.RateCardId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy bảng giá.");
        var methods = await _db.PricingRules
            .Where(r => r.RateVersionId == version.Id)
            .Select(r => r.CalcMethod)
            .ToListAsync(cancellationToken);
        var hasMatrix = methods.Any(RateDeclarationPolicy.IsMatrixMethod);
        var hasBasic = methods.Any(m => !RateDeclarationPolicy.IsMatrixMethod(m));
        if (hasMatrix && hasBasic)
        {
            throw new ConflictAppException(RateDeclarationPolicy.MixedLegacyMessage);
        }

        var draftRules = await _db.PricingRules.Where(r => r.RateVersionId == version.Id).ToListAsync(cancellationToken);
        var conflict = RateRuleConflicts.Find(draftRules);
        if (conflict is not null)
        {
            throw new ConflictAppException(conflict, RateRuleConflicts.Code);
        }

        if (version.ExplicitChargeGroups)
        {
            foreach (var rule in draftRules.Where(r => r.DeletedAt == null && r.IsActive))
            {
                var ruleError = RateFieldPolicy.PartyTypeConflict(card.PartyType, rule.PartyTypeCode)
                    ?? (RateDeclarationPolicy.IsMatrixMethod(rule.CalcMethod)
                        ? null
                        : WeightBand.DeclarationError(rule.WeightBasis, rule.WeightMin, rule.WeightMax));
                if (ruleError is not null)
                {
                    throw new UnprocessableAppException($"Quy tắc {rule.Code}: {ruleError}", "invalid_rate_field");
                }
            }

            var chargeCatalog = await _db.EconomicChargeTypes.AsNoTracking()
                .Where(t => t.TenantId == card.TenantId && t.IsActive)
                .Select(t => t.Code)
                .ToListAsync(cancellationToken);
            var ruleIds = draftRules.Select(rule => rule.Id).ToList();
            var components = await _db.PricingRuleComponents.AsNoTracking()
                .Where(component => ruleIds.Contains(component.PricingRuleId) && component.DeletedAt == null)
                .ToListAsync(cancellationToken);
            var finance = draftRules
                .Where(rule => rule.DeletedAt == null && rule.IsActive)
                .Select(rule =>
                {
                    var rows = components.Where(component => component.PricingRuleId == rule.Id).ToList();
                    return new ExplicitRuleFinance(
                        rule.ChargeCode,
                        rule.LineRole ?? (RateDeclarationPolicy.IsMatrixMethod(rule.CalcMethod) ? RateLineRoles.BaseRate : null),
                        rows.Select(component => component.CostTypeCode).FirstOrDefault(code => !string.IsNullOrWhiteSpace(code)),
                        rows.Select(component => component.RevenueTypeCode).FirstOrDefault(code => !string.IsNullOrWhiteSpace(code)),
                        rule.Code,
                        RateDeclarationPolicy.IsMatrixMethod(rule.CalcMethod));
                })
                .ToList();
            var publishBlock = RateFieldPolicy.PublishBlock(
                string.Equals(card.PartyType, "vendor", StringComparison.OrdinalIgnoreCase),
                card.SupplierPartyId is not null,
                finance,
                chargeCatalog);
            if (publishBlock is not null)
            {
                throw new ConflictAppException(publishBlock, "invalid_rate_field");
            }
        }

        var inferred = RateDeclarationPolicy.Infer(hasMatrix, hasBasic);
        if (string.IsNullOrWhiteSpace(card.DeclarationMode))
        {
            card.DeclarationMode = inferred;
        }
        else if (inferred is not null
            && !string.Equals(card.DeclarationMode, inferred, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Hình thức khai báo không khớp dữ liệu giá của phiên bản.");
        }

        var publishCode = string.Equals(card.PartyType, "customer", StringComparison.OrdinalIgnoreCase)
            ? PermissionCodes.RateSellPublish
            : PermissionCodes.RateBuyPublish;
        await _permissions.EnsureAsync(publishCode, "Bạn không có quyền phát hành bảng giá này.", cancellationToken);

        version.Status = RateVersionStatuses.Published;
        version.PublishedAt = DateTimeOffset.UtcNow;
        version.EffectiveFrom ??= version.PublishedAt;
        _audit.Append(
            AuditActions.RateCardPublished,
            AuditObjectTypes.RateCard,
            card.Id,
            null,
            card.DeclarationMode,
            $"v{version.VersionNo}");
        await _db.SaveChangesAsync(cancellationToken);
    }
}
