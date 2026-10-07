using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Fx;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Surcharges;

public sealed record SurchargeMatchContext(
    DateTimeOffset AsOf,
    string Direction,
    Guid RateCardId,
    Guid RateVersionId,
    string? TransportMode,
    string? ServiceTypeCode,
    string? RouteCode,
    string? OriginCode,
    string? DestinationCode,
    string? CommodityCode,
    bool DangerousGoods,
    Guid? VendorPartyId,
    Guid? CustomerPartyId,
    decimal ChargeableQuantity,
    decimal? GrossWeight,
    decimal Quantity,
    decimal BaseChargeAmount,
    IReadOnlyList<RatingContainerQty> Containers);

public sealed record SurchargeCandidate(
    Guid SurchargeId,
    string Code,
    string Name,
    string Direction,
    Guid VersionId,
    int VersionNo,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    Guid RuleId,
    string CalculationMode,
    string? Basis,
    string CurrencyCode,
    decimal RateAmountPercent,
    int Priority,
    decimal? MinAmount,
    decimal? MaxAmount,
    string? ContainerType,
    IReadOnlyList<SurchargeCondition> Conditions,
    IReadOnlyList<SurchargeScope> Scopes,
    IReadOnlyList<SurchargeBreak> Breaks);

public sealed record SurchargeCharge(
    SurchargeCandidate Rule,
    decimal AmountOriginal,
    string Formula);

public sealed record SurchargeApplyResult(
    IReadOnlyList<RatingDetail> Details,
    decimal CardCurrencyTotal,
    IReadOnlyList<string> Trace);

public static class SurchargeRating
{
    public const string AmbiguousCode = "AMBIGUOUS_SURCHARGE";

    public static IReadOnlyList<SurchargeCharge> Select(
        IReadOnlyList<SurchargeCandidate> candidates,
        SurchargeMatchContext ctx)
    {
        var chosen = new List<SurchargeCharge>();
        foreach (var group in candidates.GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase))
        {
            var effective = group.Where(c => IsEffective(c, ctx.AsOf)).ToList();
            if (effective.Count == 0)
            {
                continue;
            }

            var maxFrom = effective.Max(c => c.ValidFrom ?? DateTimeOffset.MinValue);
            var versionIds = effective
                .Where(c => (c.ValidFrom ?? DateTimeOffset.MinValue) == maxFrom)
                .Select(c => c.VersionId)
                .Distinct()
                .ToList();
            if (versionIds.Count > 1)
            {
                throw new ConflictAppException(
                    $"{AmbiguousCode}: phụ phí {group.Key} có nhiều phiên bản cùng ngày hiệu lực.");
            }

            var rules = effective
                .Where(c => c.VersionId == versionIds[0] && Matches(c, ctx))
                .ToList();
            if (rules.Count == 0)
            {
                continue;
            }

            var bestScore = rules.Max(Specificity);
            var best = rules.Where(r => Specificity(r) == bestScore).ToList();
            var bestPriority = best.Max(r => r.Priority);
            best = best.Where(r => r.Priority == bestPriority).ToList();
            if (best.Count > 1)
            {
                throw new ConflictAppException(
                    $"{AmbiguousCode}: phụ phí {group.Key} khớp nhiều quy tắc cùng mức cụ thể và cùng ưu tiên.");
            }

            var amount = Compute(best[0], ctx);
            if (amount is null)
            {
                continue;
            }

            chosen.Add(amount);
        }

        return chosen;
    }

    public static bool IsEffective(SurchargeCandidate candidate, DateTimeOffset asOf)
    {
        if (candidate.ValidFrom is DateTimeOffset from && asOf < from)
        {
            return false;
        }

        if (candidate.ValidTo is DateTimeOffset to && asOf > to)
        {
            return false;
        }

        return true;
    }

    public static bool Matches(SurchargeCandidate candidate, SurchargeMatchContext ctx)
    {
        if (!string.Equals(candidate.Direction, SurchargeDirections.Both, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate.Direction, ctx.Direction, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var condition in candidate.Conditions)
        {
            if (!ConditionMatches(condition, ctx))
            {
                return false;
            }
        }

        if (candidate.Scopes.Count == 0)
        {
            return true;
        }

        return candidate.Scopes.Any(scope => ScopeMatches(scope, ctx));
    }

    public static int Specificity(SurchargeCandidate candidate)
    {
        var score = candidate.Conditions.Count(c => !string.IsNullOrWhiteSpace(c.Dimension));
        if (candidate.Scopes.Count == 0)
        {
            return score;
        }

        return score + candidate.Scopes.Max(ScopeScore);
    }

    public static SurchargeCharge? Compute(SurchargeCandidate rule, SurchargeMatchContext ctx)
    {
        var mode = rule.CalculationMode;
        decimal raw;
        string formula;
        if (string.Equals(mode, SurchargeCalcModes.UnitRate, StringComparison.OrdinalIgnoreCase))
        {
            var qty = BasisQuantity(rule, ctx);
            raw = rule.RateAmountPercent * qty;
            formula = $"{qty} × {rule.RateAmountPercent}";
        }
        else if (string.Equals(mode, SurchargeCalcModes.Percentage, StringComparison.OrdinalIgnoreCase))
        {
            raw = ctx.BaseChargeAmount * rule.RateAmountPercent / 100m;
            formula = $"{rule.RateAmountPercent}% × {ctx.BaseChargeAmount}";
        }
        else if (string.Equals(mode, SurchargeCalcModes.ContainerRate, StringComparison.OrdinalIgnoreCase))
        {
            var qty = ContainerQuantity(rule.ContainerType, ctx.Containers);
            if (qty <= 0)
            {
                return null;
            }

            raw = rule.RateAmountPercent * qty;
            formula = $"{qty} × {rule.RateAmountPercent} {rule.ContainerType}".Trim();
        }
        else if (string.Equals(mode, SurchargeCalcModes.WeightBreak, StringComparison.OrdinalIgnoreCase))
        {
            var qty = BasisQuantity(rule, ctx);
            var band = rule.Breaks
                .OrderBy(b => b.SequenceNo)
                .FirstOrDefault(b => qty >= b.MinQuantity && (b.MaxQuantity is null || qty <= b.MaxQuantity));
            if (band is null)
            {
                throw new ConflictAppException(
                    $"WEIGHT_BREAK: phụ phí {rule.Code} không có bậc cho số lượng {qty}.");
            }

            raw = band.UnitAmount * qty;
            formula = $"{qty} × {band.UnitAmount}";
        }
        else if (string.Equals(mode, SurchargeCalcModes.Composite, StringComparison.OrdinalIgnoreCase))
        {
            var qty = string.IsNullOrWhiteSpace(rule.Basis) ? 1m : BasisQuantity(rule, ctx);
            raw = rule.RateAmountPercent * qty;
            raw = Clamp(raw, rule.MinAmount, rule.MaxAmount);
            formula = $"composite {qty} × {rule.RateAmountPercent}";
        }
        else
        {
            raw = rule.RateAmountPercent;
            formula = $"fixed {rule.RateAmountPercent}";
        }

        if (!string.Equals(mode, SurchargeCalcModes.Composite, StringComparison.OrdinalIgnoreCase))
        {
            raw = Clamp(raw, rule.MinAmount, rule.MaxAmount);
        }

        raw = decimal.Round(raw, 4, MidpointRounding.AwayFromZero);
        return new SurchargeCharge(rule, raw, $"v{rule.VersionNo} · {formula}");
    }

    private static bool ConditionMatches(SurchargeCondition condition, SurchargeMatchContext ctx)
    {
        var dimension = condition.Dimension.Trim().ToLowerInvariant();
        var op = condition.Operator.Trim().ToLowerInvariant();
        if (dimension is "chargeable_weight" or "weight" or "quantity")
        {
            var qty = dimension == "quantity" ? ctx.Quantity : ctx.ChargeableQuantity;
            if (op == "between")
            {
                if (condition.ValueFrom is decimal from && qty < from)
                {
                    return false;
                }

                if (condition.ValueTo is decimal to && qty > to)
                {
                    return false;
                }

                return true;
            }
        }

        var actual = dimension switch
        {
            "transport_mode" => ctx.TransportMode,
            "service" or "service_type" => ctx.ServiceTypeCode,
            "route" => ctx.RouteCode,
            "origin" => ctx.OriginCode,
            "destination" => ctx.DestinationCode,
            "commodity" => ctx.CommodityCode,
            "dangerous_goods" => ctx.DangerousGoods ? "true" : "false",
            "container_type" => ctx.Containers.FirstOrDefault(c => c.Quantity > 0)?.ContainerType,
            _ => null
        };

        if (op == "in")
        {
            var set = (condition.ValueText ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return actual is not null && set.Any(v => string.Equals(v, actual, StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(condition.ValueText))
        {
            return string.IsNullOrWhiteSpace(actual);
        }

        return string.Equals(condition.ValueText.Trim(), actual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ScopeMatches(SurchargeScope scope, SurchargeMatchContext ctx)
    {
        if (scope.RateCardId is Guid card && card != ctx.RateCardId)
        {
            return false;
        }

        if (scope.RateVersionId is Guid version && version != ctx.RateVersionId)
        {
            return false;
        }

        if (scope.VendorPartyId is Guid vendor && vendor != ctx.VendorPartyId)
        {
            return false;
        }

        if (scope.CustomerPartyId is Guid customer && customer != ctx.CustomerPartyId)
        {
            return false;
        }

        if (!Eq(scope.ServiceTypeCode, ctx.ServiceTypeCode)
            || !Eq(scope.RouteCode, ctx.RouteCode)
            || !Eq(scope.TransportMode, ctx.TransportMode))
        {
            return false;
        }

        return true;
    }

    private static int ScopeScore(SurchargeScope scope)
    {
        var score = 0;
        if (scope.RateVersionId is not null) score += 2;
        if (scope.RateCardId is not null) score += 2;
        if (scope.VendorPartyId is not null) score += 1;
        if (scope.CustomerPartyId is not null) score += 1;
        if (!string.IsNullOrWhiteSpace(scope.ServiceTypeCode)) score += 1;
        if (!string.IsNullOrWhiteSpace(scope.RouteCode)) score += 1;
        if (!string.IsNullOrWhiteSpace(scope.TransportMode)) score += 1;
        return score;
    }

    private static decimal BasisQuantity(SurchargeCandidate rule, SurchargeMatchContext ctx)
    {
        var basis = rule.Basis?.Trim().ToLowerInvariant();
        if (basis is "gross_weight" or "weight")
        {
            if (ctx.GrossWeight is null || ctx.GrossWeight <= 0)
            {
                throw new ConflictAppException($"Phụ phí {rule.Code} cần trọng lượng thực.");
            }

            return ctx.GrossWeight.Value;
        }

        if (basis is "quantity" or "qty" or "teu")
        {
            return ctx.Quantity;
        }

        return ctx.ChargeableQuantity;
    }

    private static int ContainerQuantity(string? containerType, IReadOnlyList<RatingContainerQty> containers)
    {
        if (string.IsNullOrWhiteSpace(containerType))
        {
            return containers.Sum(c => c.Quantity);
        }

        return containers
            .Where(c => string.Equals(c.ContainerType, containerType, StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.Quantity);
    }

    private static decimal Clamp(decimal value, decimal? min, decimal? max)
    {
        if (min is decimal floor && value < floor)
        {
            value = floor;
        }

        if (max is decimal ceiling && value > ceiling)
        {
            value = ceiling;
        }

        return value;
    }

    private static bool Eq(string? filter, string? value) =>
        string.IsNullOrWhiteSpace(filter)
        || string.Equals(filter.Trim(), value, StringComparison.OrdinalIgnoreCase);
}

public static class SurchargeRatingApplier
{
    public static async Task<SurchargeApplyResult> ApplyAsync(
        ILcmsDbContext db,
        IFxRateLookup fx,
        Guid tenantId,
        Bill bill,
        RateCard? card,
        RateVersion version,
        DateTimeOffset rateDate,
        string? transportMode,
        string? serviceType,
        string? routeCode,
        string? originCode,
        string? destinationCode,
        string? commodityCode,
        decimal quantity,
        decimal? grossWeight,
        decimal baseChargeAmount,
        string cardCurrency,
        string? reportingCurrency,
        IReadOnlyList<RatingContainerQty>? containers,
        CancellationToken cancellationToken)
    {
        var versions = (await db.SurchargeVersions.AsNoTracking()
            .Where(v => v.PublishStatus == SurchargeVersionStatuses.Published)
            .ToListAsync(cancellationToken))
            .Where(v => v.ValidFrom is null || v.ValidFrom <= rateDate)
            .Where(v => v.ValidTo is null || v.ValidTo >= rateDate)
            .ToList();
        if (versions.Count == 0)
        {
            return new SurchargeApplyResult([], 0m, []);
        }

        var surchargeIds = versions.Select(v => v.SurchargeId).Distinct().ToList();
        var masters = await db.Surcharges.AsNoTracking()
            .Where(s => surchargeIds.Contains(s.Id) && s.Status == SurchargeStatuses.Active)
            .ToListAsync(cancellationToken);
        var activeIds = masters.Select(s => s.Id).ToHashSet();
        versions = versions.Where(v => activeIds.Contains(v.SurchargeId)).ToList();
        if (versions.Count == 0)
        {
            return new SurchargeApplyResult([], 0m, []);
        }

        var versionIds = versions.Select(v => v.Id).ToList();
        var rules = await db.SurchargeRules.AsNoTracking()
            .Where(r => versionIds.Contains(r.SurchargeVersionId))
            .ToListAsync(cancellationToken);
        var ruleIds = rules.Select(r => r.Id).ToList();
        var conditions = ruleIds.Count == 0
            ? []
            : await db.SurchargeConditions.AsNoTracking().Where(c => ruleIds.Contains(c.SurchargeRuleId)).ToListAsync(cancellationToken);
        var scopes = ruleIds.Count == 0
            ? []
            : await db.SurchargeScopes.AsNoTracking().Where(s => ruleIds.Contains(s.SurchargeRuleId)).ToListAsync(cancellationToken);
        var breaks = ruleIds.Count == 0
            ? []
            : await db.SurchargeBreaks.AsNoTracking().Where(b => ruleIds.Contains(b.SurchargeRuleId)).ToListAsync(cancellationToken);

        var dangerous = await IsDangerousAsync(db, bill, commodityCode, cancellationToken);
        var direction = string.Equals(card?.PartyType, "customer", StringComparison.OrdinalIgnoreCase)
            ? SurchargeDirections.Sell
            : SurchargeDirections.Buy;
        var masterById = masters.ToDictionary(s => s.Id);
        var versionById = versions.ToDictionary(v => v.Id);
        var candidates = rules.Select(rule =>
        {
            var surchargeVersion = versionById[rule.SurchargeVersionId];
            var master = masterById[surchargeVersion.SurchargeId];
            return new SurchargeCandidate(
                master.Id,
                master.Code,
                master.Name,
                master.Direction,
                surchargeVersion.Id,
                surchargeVersion.VersionNo,
                surchargeVersion.ValidFrom,
                surchargeVersion.ValidTo,
                rule.Id,
                rule.CalculationMode,
                rule.Basis,
                rule.CurrencyCode,
                rule.RateAmountPercent,
                rule.Priority,
                rule.MinAmount,
                rule.MaxAmount,
                rule.ContainerType,
                conditions.Where(c => c.SurchargeRuleId == rule.Id).ToList(),
                scopes.Where(s => s.SurchargeRuleId == rule.Id).ToList(),
                breaks.Where(b => b.SurchargeRuleId == rule.Id).ToList());
        }).ToList();

        var ctx = new SurchargeMatchContext(
            rateDate,
            direction,
            card?.Id ?? version.RateCardId,
            version.Id,
            transportMode,
            serviceType,
            routeCode,
            originCode,
            destinationCode,
            commodityCode,
            dangerous,
            bill.VendorPartyId,
            bill.CustomerPartyId,
            quantity,
            grossWeight,
            quantity,
            baseChargeAmount,
            containers ?? []);

        var charges = SurchargeRating.Select(candidates, ctx);
        var nature = direction == SurchargeDirections.Sell ? "revenue" : "cost";
        var reporting = string.IsNullOrWhiteSpace(reportingCurrency) ? cardCurrency : reportingCurrency.Trim().ToUpperInvariant();
        var details = new List<RatingDetail>();
        decimal cardTotal = 0m;
        var trace = new List<string>();
        foreach (var charge in charges)
        {
            var original = charge.AmountOriginal;
            var originalCcy = charge.Rule.CurrencyCode.ToUpperInvariant();
            var (cardAmount, cardFx) = await ConvertAsync(fx, original, originalCcy, cardCurrency, rateDate, cancellationToken);
            var (reportingAmount, reportingFx) = string.Equals(reporting, cardCurrency, StringComparison.OrdinalIgnoreCase)
                ? (cardAmount, cardFx)
                : await ConvertAsync(fx, original, originalCcy, reporting, rateDate, cancellationToken);
            cardTotal += cardAmount;
            trace.Add($"{charge.Rule.Code} v{charge.Rule.VersionNo}");
            details.Add(new RatingDetail
            {
                TenantId = tenantId,
                RuleCode = charge.Rule.Code,
                ComponentCode = charge.Rule.Code,
                ComponentName = charge.Rule.Name,
                FinancialNature = nature,
                FinancialMaturity = "expected",
                Amount = cardAmount,
                CurrencyCode = cardCurrency,
                FormulaText = charge.Formula,
                SourceType = RatingSourceTypes.Surcharge,
                SourceId = charge.Rule.SurchargeId,
                SourceVersionId = charge.Rule.VersionId,
                AmountOriginal = original,
                OriginalCurrency = originalCcy,
                ReportingAmount = reportingAmount,
                LineFxRate = reportingFx.Rate,
                LineFxAsOf = reportingFx.AsOf,
                LineFxSource = reportingFx.Source
            });
        }

        return new SurchargeApplyResult(details, cardTotal, trace);
    }

    private static async Task<bool> IsDangerousAsync(
        ILcmsDbContext db,
        Bill bill,
        string? commodityCode,
        CancellationToken cancellationToken)
    {
        if (bill.CommodityTypeId is Guid commodityId)
        {
            return await db.CommodityTypes.AsNoTracking()
                .Where(c => c.Id == commodityId)
                .Select(c => c.IsDangerousGoods)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(commodityCode))
        {
            return false;
        }

        return await db.CommodityTypes.AsNoTracking()
            .AnyAsync(
                c => c.Code == commodityCode && c.IsDangerousGoods,
                cancellationToken);
    }

    private static async Task<(decimal Amount, FxStamp Stamp)> ConvertAsync(
        IFxRateLookup fx,
        decimal amount,
        string from,
        string to,
        DateTimeOffset rateDate,
        CancellationToken cancellationToken)
    {
        var roundedFrom = RatingEngine.RoundCurrency(amount, from);
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return (RatingEngine.RoundCurrency(roundedFrom, to), new FxStamp(1m, DateOnly.FromDateTime(rateDate.UtcDateTime), "identity"));
        }

        var resolved = await fx.ResolveAsync(from, to, DateOnly.FromDateTime(rateDate.UtcDateTime), cancellationToken);
        if (resolved is null)
        {
            throw new ConflictAppException($"Không có tỷ giá {from}/{to} cho phụ phí tại ngày áp dụng.");
        }

        var converted = RatingEngine.RoundCurrency(roundedFrom * resolved.Rate, to);
        return (converted, new FxStamp(resolved.Rate, resolved.RateDate, resolved.Source));
    }

    private readonly record struct FxStamp(decimal Rate, DateOnly AsOf, string Source);
}
