using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs.Commands;
using LCMS.Application.Fx;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings.Commands;

public sealed record CreateRatingCommand(
    Guid BillId,
    Guid RateVersionId,
    decimal? Quantity,
    decimal? Weight,
    string? ServiceTypeCode,
    string? PartyTypeCode,
    string? RouteCode,
    decimal? BaseAmount,
    Guid? SupersedesRatingId,
    bool SeedExpectedCosts = false,
    bool SeedExpectedRevenues = false,
    DateTimeOffset? RateDate = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? TransportMode = null,
    string? CommodityCode = null,
    decimal? GrossWeightKg = null,
    decimal? VolumeCbm = null,
    string? ChargeableOverrideReason = null,
    string? TargetCurrency = null,
    IReadOnlyList<RatingContainerQty>? Containers = null) : IRequest<Guid>;

public sealed class CreateRatingCommandValidator : AbstractValidator<CreateRatingCommand>
{
    public CreateRatingCommandValidator()
    {
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.RateVersionId).NotEmpty().WithMessage("Phiên bản bảng giá không hợp lệ.");
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.")
            .When(x => x.Quantity.HasValue);
        RuleFor(x => x.Weight)
            .GreaterThan(0).WithMessage("Trọng lượng phải lớn hơn 0.")
            .When(x => x.Weight.HasValue);
        RuleFor(x => x.BaseAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền cơ sở không được âm.")
            .When(x => x.BaseAmount.HasValue);
        RuleFor(x => x.ServiceTypeCode)
            .MaximumLength(64)
            .When(x => x.ServiceTypeCode is not null);
        RuleFor(x => x.PartyTypeCode)
            .MaximumLength(32)
            .When(x => x.PartyTypeCode is not null);
        RuleFor(x => x.RouteCode)
            .MaximumLength(64)
            .When(x => x.RouteCode is not null);
    }
}

/// <summary>
/// Rate Bill against published version: applicability filter + formula types + optional re-rate / seed.
/// </summary>
public sealed class CreateRatingCommandHandler : IRequestHandler<CreateRatingCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISender _sender;
    private readonly IFxRateLookup _fx;
    private readonly IPermissionService _permissions;

    public CreateRatingCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ISender sender,
        IFxRateLookup fx,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _sender = sender;
        _fx = fx;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateRatingCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        var version = await _db.RateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.RateVersionId, cancellationToken);
        if (version is null)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        if (!version.IsPublished)
        {
            throw new ConflictAppException("Chỉ được tính giá trên phiên bản bảng giá đã phát hành.");
        }

        var rateDate = request.RateDate ?? DateTimeOffset.UtcNow;
        if (version.EffectiveFrom is DateTimeOffset from && rateDate < from)
        {
            throw new ConflictAppException("Phiên bản bảng giá chưa đến ngày hiệu lực.");
        }

        if (version.EffectiveTo is DateTimeOffset to && rateDate > to)
        {
            throw new ConflictAppException("Phiên bản bảng giá không còn hiệu lực theo ngày áp dụng.");
        }

        var card = await _db.RateCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == version.RateCardId, cancellationToken);

        var serviceType = Normalize(request.ServiceTypeCode) ?? Normalize(bill.BillType);
        var partyType = Normalize(request.PartyTypeCode) ?? Normalize(card?.PartyType);
        var routeCode = Normalize(request.RouteCode) ?? Normalize(bill.RouteCode);
        var transportMode = Normalize(request.TransportMode) ?? Normalize(bill.TransportMode);
        var originCode = Normalize(request.OriginCode) ?? Normalize(bill.OriginCode);
        var destinationCode = Normalize(request.DestinationCode) ?? Normalize(bill.DestinationCode);
        var commodityCode = Normalize(request.CommodityCode);
        if (commodityCode is null && bill.CommodityTypeId is Guid commodityId)
        {
            var fromBill = await _db.CommodityTypes.AsNoTracking()
                .Where(c => c.Id == commodityId)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(cancellationToken);
            commodityCode = Normalize(fromBill);
        }

        Rating? prior = null;
        if (request.SupersedesRatingId.HasValue)
        {
            prior = await _db.Ratings.FirstOrDefaultAsync(
                r => r.Id == request.SupersedesRatingId.Value,
                cancellationToken);
            if (prior is null)
            {
                throw new NotFoundAppException("Không tìm thấy lần tính giá cần thay thế.");
            }

            if (prior.BillId != bill.Id)
            {
                throw new ConflictAppException("Lần tính giá thay thế phải thuộc cùng Bill.");
            }

            if (string.Equals(prior.Status, RatingStatuses.Superseded, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictAppException("Lần tính giá đã bị thay thế; không được ghi đè lịch sử.");
            }

            await _permissions.EnsureAsync(
                PermissionCodes.RateRerate,
                "Bạn không có quyền tính lại giá.",
                cancellationToken);
        }

        var rules = await _db.PricingRules
            .AsNoTracking()
            .Where(r => r.RateVersionId == version.Id && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .ToListAsync(cancellationToken);

        var applicable = rules
            .Where(r => RatingEngine.Matches(
                r, serviceType, partyType, routeCode, transportMode, originCode, destinationCode, commodityCode))
            .ToList();
        var selected = RatingEngine.Select(applicable);

        var ruleIds = selected.Select(r => r.Id).ToList();
        var components = await _db.PricingRuleComponents
            .AsNoTracking()
            .Where(c => ruleIds.Contains(c.PricingRuleId))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);

        var componentsByRule = components
            .GroupBy(c => c.PricingRuleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var breaks = await _db.RateBreaks.AsNoTracking()
            .Where(b => ruleIds.Contains(b.PricingRuleId))
            .ToListAsync(cancellationToken);
        var breaksByRule = breaks.GroupBy(b => b.PricingRuleId).ToDictionary(g => g.Key, g => (IReadOnlyList<RateBreak>)g.ToList());
        var containerPrices = await _db.ContainerRatePrices.AsNoTracking()
            .Where(p => ruleIds.Contains(p.PricingRuleId))
            .ToListAsync(cancellationToken);

        var measures = await _db.OperationalMeasurements.AsNoTracking()
            .Where(m => m.ObjectType == OperationalObjectTypes.Bill && m.ObjectId == bill.Id)
            .ToListAsync(cancellationToken);
        var gross = request.GrossWeightKg
            ?? measures.FirstOrDefault(m => m.MeasureCode == MeasureCodes.GrossWeightKg)?.Quantity;
        var volume = request.VolumeCbm
            ?? measures.FirstOrDefault(m => m.MeasureCode == MeasureCodes.VolumeCbm)?.Quantity;
        var confirmed = measures.FirstOrDefault(m => m.MeasureCode == MeasureCodes.ChargeableWeightKg && m.IsConfirmed);
        var factorRule = selected.FirstOrDefault(r => r.VolumetricFactor is not null || r.RoundingStep is not null);
        var computed = RatingEngine.Chargeable(
            transportMode ?? card?.TransportMode,
            gross,
            volume,
            factorRule?.VolumetricFactor,
            factorRule?.RoundingStep);

        string basis;
        decimal quantity;
        if (request.Quantity is decimal asked)
        {
            quantity = asked;
            basis = "manual";
            if (confirmed is not null && confirmed.Quantity != asked)
            {
                if (string.IsNullOrWhiteSpace(request.ChargeableOverrideReason))
                {
                    throw new ConflictAppException("Trọng lượng tính cước đã xác nhận. Nhập lý do để ghi đè.");
                }

                await _permissions.EnsureAsync(
                    PermissionCodes.RateQuantityOverride,
                    "Bạn không có quyền ghi đè số lượng tính giá.",
                    cancellationToken);
                basis = "override";
            }
        }
        else if (request.Weight is decimal weight)
        {
            quantity = weight;
            basis = "weight";
        }
        else if (confirmed is not null)
        {
            quantity = confirmed.Quantity;
            basis = "confirmed";
        }
        else if (computed is decimal chargeable)
        {
            quantity = chargeable;
            var mode = (transportMode ?? card?.TransportMode)?.ToLowerInvariant();
            basis = mode is "sea" or "ocean" ? "sea_wm" : "air_volumetric";
        }
        else
        {
            quantity = 1m;
            basis = "default";
        }

        var rating = new Rating
        {
            TenantId = tenantId,
            BillId = bill.Id,
            RateVersionId = version.Id,
            RatedAt = DateTimeOffset.UtcNow,
            CurrencyCode = card?.CurrencyCode ?? selected[0].CurrencyCode,
            Quantity = quantity,
            Weight = request.Weight ?? gross,
            ServiceTypeCode = serviceType,
            PartyTypeCode = partyType,
            RouteCode = routeCode,
            BaseAmount = request.BaseAmount,
            Status = RatingStatuses.Completed,
            SupersedesRatingId = prior?.Id,
            ChargeableWeightKg = confirmed?.Quantity ?? computed ?? quantity,
            ChargeableBasis = basis,
            RateDate = rateDate
        };

        var details = new List<RatingDetail>();
        decimal total = 0m;
        decimal runningBase = request.BaseAmount ?? 0m;
        var hasExplicitBase = request.BaseAmount.HasValue;

        foreach (var rule in selected)
        {
            var method = rule.CalcMethod;
            if (string.Equals(method, PricingCalcMethods.WeightBreakPivot, StringComparison.OrdinalIgnoreCase))
            {
                var line = RatingEngine.WeightBreakPivot(quantity, Breaks(breaksByRule, rule.Id), rule.MinAmount);
                total += line.Amount;
                details.Add(Line(tenantId, rule, null, rule.Code, rule.Name, "cost", line.Amount, rule.CurrencyCode, line.Formula));
                continue;
            }

            if (string.Equals(method, PricingCalcMethods.WeightStep, StringComparison.OrdinalIgnoreCase))
            {
                var line = RatingEngine.WeightStep(quantity, rule.RoundingStep, rule.UnitAmount, Breaks(breaksByRule, rule.Id), rule.MinAmount);
                total += line.Amount;
                details.Add(Line(tenantId, rule, null, rule.Code, rule.Name, "cost", line.Amount, rule.CurrencyCode, line.Formula));
                continue;
            }

            if (string.Equals(method, PricingCalcMethods.ContainerRate, StringComparison.OrdinalIgnoreCase))
            {
                var prices = containerPrices.Where(p => p.PricingRuleId == rule.Id).ToList();
                var line = RatingEngine.Containers(request.Containers ?? [], prices);
                total += line.Amount;
                details.Add(Line(tenantId, rule, null, rule.Code, rule.Name, "cost", line.Amount, rule.CurrencyCode, line.Formula));
                continue;
            }

            if (string.Equals(method, PricingCalcMethods.Composite, StringComparison.OrdinalIgnoreCase))
            {
                if (!componentsByRule.TryGetValue(rule.Id, out var compositeParts) || compositeParts.Count == 0)
                {
                    throw new ConflictAppException("COMPOSITE: Quy tắc thiếu thành phần giá.");
                }

                var ordered = RatingEngine.OrderComponents(compositeParts);
                var byCode = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                foreach (var component in ordered)
                {
                    var componentBase = !string.IsNullOrWhiteSpace(component.DependsOnCode) && byCode.TryGetValue(component.DependsOnCode, out var dep)
                        ? dep
                        : hasExplicitBase ? request.BaseAmount!.Value : runningBase;
                    var amount = ComputeAmount(
                        string.IsNullOrWhiteSpace(component.CalcMethod) ? PricingCalcMethods.Fixed : component.CalcMethod,
                        component.Amount,
                        quantity,
                        componentBase,
                        rule.MinAmount,
                        rule.MaxAmount);
                    byCode[component.Code] = amount;
                    total += amount;
                    if (!hasExplicitBase)
                    {
                        runningBase += amount;
                    }

                    details.Add(Line(tenantId, rule, component.Id, component.Code, component.Name, component.FinancialNature, amount, component.CurrencyCode, component.CalcMethod));
                }

                continue;
            }

            if (componentsByRule.TryGetValue(rule.Id, out var ruleComponents) && ruleComponents.Count > 0)
            {
                foreach (var component in ruleComponents)
                {
                    var amount = ComputeAmount(
                        rule.CalcMethod,
                        component.Amount,
                        quantity,
                        hasExplicitBase ? request.BaseAmount!.Value : runningBase,
                        rule.MinAmount,
                        rule.MaxAmount);
                    total += amount;
                    if (!hasExplicitBase)
                    {
                        runningBase += amount;
                    }

                    details.Add(new RatingDetail
                    {
                        TenantId = tenantId,
                        PricingRuleId = rule.Id,
                        PricingRuleComponentId = component.Id,
                        RuleCode = rule.Code,
                        ComponentCode = component.Code,
                        ComponentName = component.Name,
                        FinancialNature = component.FinancialNature,
                        FinancialMaturity = "expected",
                        Amount = amount,
                        CurrencyCode = component.CurrencyCode
                    });
                }
            }
            else
            {
                var lineQty = QuantityFor(rule, quantity, request.Weight ?? gross);
                var amount = ComputeAmount(
                    rule.CalcMethod,
                    rule.UnitAmount,
                    lineQty,
                    hasExplicitBase ? request.BaseAmount!.Value : runningBase,
                    rule.MinAmount,
                    rule.MaxAmount);
                var perKg = string.Equals(rule.Applicability, RatingEngine.PerGrossKg, StringComparison.OrdinalIgnoreCase);
                var formula = perKg ? $"{lineQty} kg × {rule.UnitAmount}" : rule.CalcMethod;
                var cardAmount = amount;
                var cardCcy = rule.CurrencyCode;
                string? cardFormula = formula;
                if (perKg)
                {
                    (cardAmount, cardCcy, cardFormula) = await ToCardCurrencyAsync(
                        amount, rule.CurrencyCode, card?.CurrencyCode ?? rule.CurrencyCode, formula, rateDate, cancellationToken);
                }

                total += cardAmount;
                if (!hasExplicitBase)
                {
                    runningBase += cardAmount;
                }

                details.Add(new RatingDetail
                {
                    TenantId = tenantId,
                    PricingRuleId = rule.Id,
                    PricingRuleComponentId = null,
                    RuleCode = rule.Code,
                    ComponentCode = rule.Code,
                    ComponentName = rule.Name,
                    FinancialNature = "cost",
                    FinancialMaturity = "expected",
                    Amount = cardAmount,
                    CurrencyCode = cardCcy,
                    FormulaText = cardFormula
                });
            }
        }

        var cardCurrency = card?.CurrencyCode ?? selected[0].CurrencyCode;
        rating.OriginalAmount = total;
        rating.OriginalCurrency = cardCurrency;
        var target = string.IsNullOrWhiteSpace(request.TargetCurrency) ? bill.PreferredCurrency : request.TargetCurrency.Trim();
        if (!string.IsNullOrWhiteSpace(target)
            && !string.Equals(target, cardCurrency, StringComparison.OrdinalIgnoreCase))
        {
            var fx = await _fx.ResolveAsync(cardCurrency, target, DateOnly.FromDateTime(rateDate.UtcDateTime), cancellationToken);
            if (fx is null)
            {
                throw new ConflictAppException($"Không có tỷ giá {cardCurrency}/{target} cho ngày áp dụng.");
            }

            var converted = RatingEngine.RoundCurrency(total * fx.Rate, target);
            rating.FxRate = fx.Rate;
            rating.FxAsOf = fx.RateDate;
            rating.FxSource = fx.Source;
            rating.RoundedAmount = converted;
            rating.TotalAmount = converted;
            rating.CurrencyCode = target.ToUpperInvariant();
        }
        else
        {
            rating.FxRate = 1m;
            rating.FxSource = "identity";
            rating.RoundedAmount = RatingEngine.RoundCurrency(total, cardCurrency);
            rating.TotalAmount = total;
        }

        rating.ContextJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            rateDate,
            serviceType,
            partyType,
            routeCode,
            transportMode,
            originCode,
            destinationCode,
            commodityCode,
            gross,
            volume,
            chargeable = rating.ChargeableWeightKg,
            basis,
            versionId = version.Id,
            rules = selected.Select(r => r.Code).ToArray()
        });

        if (prior is not null)
        {
            // Mark prior superseded; never mutate its details (C-011 history).
            prior.Status = RatingStatuses.Superseded;
        }

        _db.Ratings.Add(rating);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var detail in details)
        {
            detail.RatingId = rating.Id;
        }

        _db.RatingDetails.AddRange(details);
        await _db.SaveChangesAsync(cancellationToken);

        if (request.SeedExpectedCosts)
        {
            await _sender.Send(new SeedExpectedCostsFromRatingCommand(rating.Id), cancellationToken);
        }

        if (request.SeedExpectedRevenues)
        {
            await _sender.Send(
                new LCMS.Application.Revenues.Commands.SeedExpectedRevenuesFromRatingCommand(rating.Id),
                cancellationToken);
        }

        return rating.Id;
    }

    internal static bool MatchesApplicability(
        PricingRule rule,
        string? serviceType,
        string? partyType,
        string? routeCode)
    {
        if (!string.IsNullOrWhiteSpace(rule.ServiceTypeCode)
            && !string.Equals(rule.ServiceTypeCode, serviceType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.PartyTypeCode)
            && !string.Equals(rule.PartyTypeCode, partyType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.RouteCode)
            && !string.Equals(rule.RouteCode, routeCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    internal static decimal ComputeAmount(
        string calcMethod,
        decimal unitOrFixed,
        decimal quantity,
        decimal baseAmount,
        decimal? minAmount,
        decimal? maxAmount)
    {
        decimal raw;
        if (string.Equals(calcMethod, PricingCalcMethods.UnitRate, StringComparison.OrdinalIgnoreCase))
        {
            raw = unitOrFixed * quantity;
        }
        else if (string.Equals(calcMethod, PricingCalcMethods.PercentOfBase, StringComparison.OrdinalIgnoreCase))
        {
            raw = baseAmount * unitOrFixed / 100m;
        }
        else if (string.Equals(calcMethod, PricingCalcMethods.MinMaxClamp, StringComparison.OrdinalIgnoreCase))
        {
            raw = unitOrFixed * quantity;
            raw = Clamp(raw, minAmount, maxAmount);
            return decimal.Round(raw, 4, MidpointRounding.AwayFromZero);
        }
        else
        {
            // fixed (default)
            raw = unitOrFixed;
        }

        return decimal.Round(raw, 4, MidpointRounding.AwayFromZero);
    }

    private static decimal QuantityFor(PricingRule rule, decimal quantity, decimal? grossKg)
    {
        if (!string.Equals(rule.Applicability, RatingEngine.PerGrossKg, StringComparison.OrdinalIgnoreCase))
        {
            return quantity;
        }

        if (grossKg is null || grossKg <= 0)
        {
            throw new ConflictAppException("Phụ phí theo kg cần trọng lượng thực.");
        }

        return grossKg.Value;
    }

    private async Task<(decimal Amount, string Currency, string? Formula)> ToCardCurrencyAsync(
        decimal amount,
        string amountCurrency,
        string cardCurrency,
        string? formula,
        DateTimeOffset rateDate,
        CancellationToken cancellationToken)
    {
        if (string.Equals(amountCurrency, cardCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return (amount, cardCurrency, formula);
        }

        var fx = await _fx.ResolveAsync(
            amountCurrency,
            cardCurrency,
            DateOnly.FromDateTime(rateDate.UtcDateTime),
            cancellationToken);
        if (fx is null)
        {
            throw new ConflictAppException(
                $"Không có tỷ giá {amountCurrency}/{cardCurrency} để cộng phụ phí vào tổng bảng giá.");
        }

        var converted = RatingEngine.RoundCurrency(amount * fx.Rate, cardCurrency);
        var text = string.IsNullOrWhiteSpace(formula)
            ? $"{amount} {amountCurrency} × {fx.Rate}"
            : $"{formula} · {amount} {amountCurrency} × {fx.Rate}";
        return (converted, cardCurrency, text);
    }

    private static IReadOnlyList<RateBreak> Breaks(
        IReadOnlyDictionary<Guid, IReadOnlyList<RateBreak>> breaksByRule,
        Guid ruleId) =>
        breaksByRule.TryGetValue(ruleId, out var rows) ? rows : [];

    private static RatingDetail Line(
        Guid tenantId,
        PricingRule rule,
        Guid? componentId,
        string code,
        string name,
        string nature,
        decimal amount,
        string currency,
        string? formula) =>
        new()
        {
            TenantId = tenantId,
            PricingRuleId = rule.Id,
            PricingRuleComponentId = componentId,
            RuleCode = rule.Code,
            ComponentCode = code,
            ComponentName = name,
            FinancialNature = nature,
            FinancialMaturity = "expected",
            Amount = amount,
            CurrencyCode = currency,
            FormulaText = formula
        };

    private static decimal Clamp(decimal value, decimal? min, decimal? max)
    {
        if (min.HasValue && value < min.Value)
        {
            value = min.Value;
        }

        if (max.HasValue && value > max.Value)
        {
            value = max.Value;
        }

        return value;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
