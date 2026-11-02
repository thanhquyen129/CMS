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
    bool? DangerousGoods,
    Guid? VendorPartyId,
    Guid? CustomerPartyId,
    string? CustomerGroupCode,
    decimal ChargeableQuantity,
    decimal? GrossWeight,
    decimal Quantity,
    decimal BaseChargeAmount,
    IReadOnlyList<RatingContainerQty> Containers,
    Guid? BillId = null,
    Guid? ShipmentId = null,
    Guid? TransportLegId = null,
    bool? DeliveryOutsideHours = null,
    IReadOnlyList<ServiceSelectionSnapshot>? Selections = null);

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
    IReadOnlyList<SurchargeBreak> Breaks,
    decimal? VatRate = null,
    Guid? EconomicChargeTypeId = null,
    string ActivationPolicy = SurchargeActivationPolicies.UnclassifiedLegacy,
    string? ActivationConfigJson = null,
    string? ApprovalRef = null);

public sealed record SurchargeCharge(
    SurchargeCandidate Rule,
    decimal AmountOriginal,
    string Formula,
    string ReasonCode,
    string ReasonMessageVi,
    string? ActivationEvidenceRef,
    decimal AppliedQuantity,
    string ApplicationBucket);

public sealed record SurchargeEvaluationItem(
    SurchargeCandidate Rule,
    string ApplicationBucket,
    string ReasonCode,
    string ReasonMessageVi,
    string? ActivationEvidenceRef,
    decimal? AmountOriginal,
    string? Formula,
    decimal? AppliedQuantity);

public sealed record SurchargeEvaluationResult(
    IReadOnlyList<SurchargeCharge> Applied,
    IReadOnlyList<SurchargeEvaluationItem> EligibleNotActivated,
    IReadOnlyList<SurchargeEvaluationItem> Excluded,
    IReadOnlyList<string> UnpricedServices,
    IReadOnlyList<string> UnclassifiedWarnings);

public sealed record SurchargeApplyResult(
    IReadOnlyList<RatingDetail> Details,
    decimal CardCurrencyTotal,
    IReadOnlyList<string> Trace,
    SurchargeEvaluationResult Evaluation);

public static class SurchargeRating
{
    public const string AmbiguousCode = "AMBIGUOUS_SURCHARGE";

    public static SurchargeEvaluationResult Evaluate(
        IReadOnlyList<SurchargeCandidate> candidates,
        SurchargeMatchContext ctx)
    {
        var applied = new List<SurchargeCharge>();
        var eligible = new List<SurchargeEvaluationItem>();
        var excluded = new List<SurchargeEvaluationItem>();
        var unclassified = new List<string>();
        var selections = ctx.Selections ?? [];

        foreach (var group in candidates.GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase))
        {
            var step1 = group.ToList();
            var directionOk = step1
                .Where(c => string.Equals(c.Direction, SurchargeDirections.Both, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Direction, ctx.Direction, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var miss in step1.Except(directionOk))
            {
                excluded.Add(Item(miss, "excluded", SurchargeReasonCodes.ExcludedWrongDirection,
                    "Phụ phí không khớp chiều mua/bán đang tính."));
            }

            var effective = directionOk.Where(c => IsEffective(c, ctx.AsOf)).ToList();
            foreach (var miss in directionOk.Except(effective))
            {
                excluded.Add(Item(miss, "excluded", SurchargeReasonCodes.ExcludedInactive,
                    "Phiên bản phụ phí ngoài thời gian hiệu lực."));
            }

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

            var versionRules = effective.Where(c => c.VersionId == versionIds[0]).ToList();
            var matched = versionRules.Where(c => Matches(c, ctx)).ToList();
            foreach (var miss in versionRules.Except(matched))
            {
                excluded.Add(Item(miss, "excluded", SurchargeReasonCodes.ExcludedScopeMismatch,
                    "Không khớp phạm vi/điều kiện hợp lệ (tuyến, phương thức, đối tác hoặc trọng lượng)."));
            }

            if (matched.Count == 0)
            {
                continue;
            }

            var bestScore = matched.Max(Specificity);
            var best = matched.Where(r => Specificity(r) == bestScore).ToList();
            var bestPriority = best.Max(r => r.Priority);
            best = best.Where(r => r.Priority == bestPriority).ToList();
            if (best.Count > 1)
            {
                throw new ConflictAppException(
                    $"{AmbiguousCode}: phụ phí {group.Key} khớp nhiều quy tắc cùng mức cụ thể và cùng ưu tiên.");
            }

            var winner = best[0];
            foreach (var dup in matched.Where(m => m.RuleId != winner.RuleId))
            {
                excluded.Add(Item(dup, "excluded", SurchargeReasonCodes.DuplicateSkipped,
                    "Bỏ qua vì đã có quy tắc ưu tiên hơn trong cùng mã phụ phí."));
            }

            var config = SurchargeActivationConfigParser.Parse(winner.ActivationConfigJson);
            var activation = SurchargeActivationResolver.Resolve(
                winner.ActivationPolicy,
                winner.ApprovalRef,
                config,
                ctx,
                selections);

            if (string.Equals(activation.ReasonCode, SurchargeReasonCodes.ExcludedUnclassified, StringComparison.OrdinalIgnoreCase))
            {
                unclassified.Add($"{winner.Code} v{winner.VersionNo}");
            }

            if (!activation.Activated)
            {
                var bucket = activation.Bucket ?? "eligible_not_activated";
                var target = string.Equals(bucket, "excluded", StringComparison.OrdinalIgnoreCase) ? excluded : eligible;
                target.Add(Item(
                    winner,
                    bucket,
                    activation.ReasonCode,
                    activation.ReasonMessageVi,
                    activation.EvidenceRef));
                continue;
            }

            var qty = activation.Quantity ?? 1m;
            var amount = Compute(winner, ctx, qty);
            if (amount is null)
            {
                eligible.Add(Item(
                    winner,
                    "eligible_not_activated",
                    SurchargeReasonCodes.CalculationInvalid,
                    "Không tính được số tiền phụ phí (thiếu cơ sở tính hoặc số lượng).",
                    activation.EvidenceRef));
                continue;
            }

            applied.Add(new SurchargeCharge(
                winner,
                amount.AmountOriginal,
                amount.Formula,
                SurchargeReasonCodes.Applied,
                activation.ReasonMessageVi,
                activation.EvidenceRef,
                qty,
                "applied"));
        }

        var unpriced = SurchargeActivationResolver.UnpricedSelectedServices(
            ctx.Direction,
            selections,
            candidates.Where(c =>
                string.Equals(c.Direction, SurchargeDirections.Both, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Direction, ctx.Direction, StringComparison.OrdinalIgnoreCase)).ToList());

        return new SurchargeEvaluationResult(applied, eligible, excluded, unpriced, unclassified);
    }

    /// <summary>Backward-compatible helper used by unit tests — returns only applied charges.</summary>
    public static IReadOnlyList<SurchargeCharge> Select(
        IReadOnlyList<SurchargeCandidate> candidates,
        SurchargeMatchContext ctx) =>
        Evaluate(candidates, ctx).Applied;

    public static bool IsEffective(SurchargeCandidate candidate, DateTimeOffset asOf)
    {
        if (candidate.ValidFrom is DateTimeOffset from && RatingEngine.IsBeforeEffectiveDay(asOf, from))
        {
            return false;
        }

        if (candidate.ValidTo is DateTimeOffset to && RatingEngine.IsAfterEffectiveDay(asOf, to))
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

    public static SurchargeCharge? Compute(SurchargeCandidate rule, SurchargeMatchContext ctx, decimal activationQty = 1m)
    {
        var mode = rule.CalculationMode;
        var basis = rule.Basis?.Trim().ToLowerInvariant();
        decimal raw;
        string formula;
        if (string.Equals(mode, SurchargeCalcModes.UnitRate, StringComparison.OrdinalIgnoreCase))
        {
            var qty = BasisQuantity(rule, ctx, activationQty);
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
            var qty = BasisQuantity(rule, ctx, activationQty);
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
            var qty = string.IsNullOrWhiteSpace(rule.Basis) ? 1m : BasisQuantity(rule, ctx, activationQty);
            raw = rule.RateAmountPercent * qty;
            raw = Clamp(raw, rule.MinAmount, rule.MaxAmount);
            formula = $"composite {qty} × {rule.RateAmountPercent}";
        }
        else
        {
            // FIXED_RATE — basis decides whether amount is once or per unit.
            if (basis is SurchargeCalcBases.PerUnit or SurchargeCalcBases.PerPackage or "unit" or "package")
            {
                if (activationQty <= 0)
                {
                    return null;
                }

                raw = rule.RateAmountPercent * activationQty;
                formula = $"fixed {rule.RateAmountPercent} × {activationQty}";
            }
            else if (basis is SurchargeCalcBases.PerShipment or SurchargeCalcBases.PerLeg)
            {
                raw = rule.RateAmountPercent;
                formula = $"fixed {rule.RateAmountPercent} ({basis})";
            }
            else
            {
                raw = rule.RateAmountPercent;
                formula = $"fixed {rule.RateAmountPercent}";
            }
        }

        if (!string.Equals(mode, SurchargeCalcModes.Composite, StringComparison.OrdinalIgnoreCase))
        {
            raw = Clamp(raw, rule.MinAmount, rule.MaxAmount);
        }

        raw = decimal.Round(raw, 4, MidpointRounding.AwayFromZero);
        return new SurchargeCharge(
            rule,
            raw,
            $"v{rule.VersionNo} · {formula}",
            SurchargeReasonCodes.Applied,
            "Đã áp dụng",
            null,
            activationQty,
            "applied");
    }

    private static SurchargeEvaluationItem Item(
        SurchargeCandidate rule,
        string bucket,
        string reasonCode,
        string reasonVi,
        string? evidence = null) =>
        new(rule, bucket, reasonCode, reasonVi, evidence, null, null, null);

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
            "dangerous_goods" => ctx.DangerousGoods switch
            {
                true => "true",
                false => "false",
                null => null
            },
            "container_type" => ctx.Containers.FirstOrDefault(c => c.Quantity > 0)?.ContainerType,
            _ => null
        };

        if (dimension == "dangerous_goods" && actual is null)
        {
            // Eligibility does not invent DG=false; treat unknown as not matching a concrete filter.
            return false;
        }

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

        if (!string.IsNullOrWhiteSpace(scope.CustomerGroupCode)
            && !string.Equals(scope.CustomerGroupCode.Trim(), ctx.CustomerGroupCode?.Trim(), StringComparison.OrdinalIgnoreCase))
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
        if (scope.CustomerGroupCode is not null) score += 1;
        if (scope.CustomerPartyId is not null) score += 1;
        if (!string.IsNullOrWhiteSpace(scope.ServiceTypeCode)) score += 1;
        if (!string.IsNullOrWhiteSpace(scope.RouteCode)) score += 1;
        if (!string.IsNullOrWhiteSpace(scope.TransportMode)) score += 1;
        return score;
    }

    private static decimal BasisQuantity(SurchargeCandidate rule, SurchargeMatchContext ctx, decimal activationQty)
    {
        var basis = rule.Basis?.Trim().ToLowerInvariant();
        if (basis is "gross_weight" or "weight" or SurchargeCalcBases.PerKg)
        {
            if (ctx.GrossWeight is null || ctx.GrossWeight <= 0)
            {
                throw new ConflictAppException($"Phụ phí {rule.Code} cần trọng lượng thực.");
            }

            return ctx.GrossWeight.Value;
        }

        if (basis is "quantity" or "qty" or "teu" or SurchargeCalcBases.PerUnit or SurchargeCalcBases.PerPackage)
        {
            return activationQty > 0 ? activationQty : ctx.Quantity;
        }

        if (basis is SurchargeCalcBases.PerBill or SurchargeCalcBases.PerShipment or SurchargeCalcBases.PerLeg)
        {
            return 1m;
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
        CancellationToken cancellationToken,
        Guid? shipmentId = null,
        Guid? transportLegId = null)
    {
        var versions = (await db.SurchargeVersions.AsNoTracking()
            .Where(v => v.PublishStatus == SurchargeVersionStatuses.Published)
            .ToListAsync(cancellationToken))
            .Where(v => v.ValidFrom is null || !RatingEngine.IsBeforeEffectiveDay(rateDate, v.ValidFrom.Value))
            .Where(v => v.ValidTo is null || !RatingEngine.IsAfterEffectiveDay(rateDate, v.ValidTo.Value))
            .ToList();
        if (versions.Count == 0)
        {
            var empty = new SurchargeEvaluationResult([], [], [], [], []);
            return new SurchargeApplyResult([], 0m, [], empty);
        }

        var surchargeIds = versions.Select(v => v.SurchargeId).Distinct().ToList();
        var masters = await db.Surcharges.AsNoTracking()
            .Where(s => surchargeIds.Contains(s.Id) && s.Status == SurchargeStatuses.Active)
            .ToListAsync(cancellationToken);
        var activeIds = masters.Select(s => s.Id).ToHashSet();
        versions = versions.Where(v => activeIds.Contains(v.SurchargeId)).ToList();
        if (versions.Count == 0)
        {
            var empty = new SurchargeEvaluationResult([], [], [], [], []);
            return new SurchargeApplyResult([], 0m, [], empty);
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
        string? customerGroup = null;
        if (bill.CustomerPartyId is Guid customerId)
        {
            customerGroup = await db.BusinessParties.AsNoTracking()
                .Where(p => p.Id == customerId)
                .Select(p => p.GroupCode)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var resolvedShipmentId = shipmentId
            ?? await db.BillShipmentLinks.AsNoTracking()
                .Where(l => l.BillId == bill.Id)
                .Select(l => (Guid?)l.ShipmentId)
                .FirstOrDefaultAsync(cancellationToken);
        var selections = await LoadSelectionsAsync(db, bill.Id, resolvedShipmentId, transportLegId, cancellationToken);

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
                breaks.Where(b => b.SurchargeRuleId == rule.Id).ToList(),
                surchargeVersion.VatRate,
                surchargeVersion.EconomicChargeTypeId,
                SurchargeActivationValidator.Normalize(surchargeVersion.ActivationPolicy),
                surchargeVersion.ActivationConfigJson,
                surchargeVersion.ApprovalRef);
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
            customerGroup,
            quantity,
            grossWeight,
            quantity,
            baseChargeAmount,
            containers ?? [],
            bill.Id,
            resolvedShipmentId,
            transportLegId,
            null,
            selections);

        var evaluation = SurchargeRating.Evaluate(candidates, ctx);
        var nature = direction == SurchargeDirections.Sell ? "revenue" : "cost";
        var reporting = string.IsNullOrWhiteSpace(reportingCurrency) ? cardCurrency : reportingCurrency.Trim().ToUpperInvariant();
        var details = new List<RatingDetail>();
        decimal cardTotal = 0m;
        var trace = new List<string>();

        foreach (var charge in evaluation.Applied)
        {
            var original = charge.AmountOriginal;
            var originalCcy = charge.Rule.CurrencyCode.ToUpperInvariant();
            var (cardAmount, cardFx) = await ConvertAsync(fx, original, originalCcy, cardCurrency, rateDate, cancellationToken);
            var (reportingAmount, reportingFx) = string.Equals(reporting, cardCurrency, StringComparison.OrdinalIgnoreCase)
                ? (cardAmount, cardFx)
                : await ConvertAsync(fx, original, originalCcy, reporting, rateDate, cancellationToken);
            cardTotal += cardAmount;
            trace.Add($"{charge.Rule.Code} v{charge.Rule.VersionNo}");
            var scope = charge.Rule.Scopes.FirstOrDefault(s => s.VendorPartyId is not null || s.CustomerPartyId is not null);
            Guid? suggested = nature == "revenue"
                ? scope?.CustomerPartyId ?? card?.CustomerPartyId ?? bill.CustomerPartyId
                : scope?.VendorPartyId ?? card?.SupplierPartyId;
            details.Add(BuildDetail(
                tenantId,
                charge.Rule,
                nature,
                cardAmount,
                cardCurrency,
                charge.Formula,
                original,
                originalCcy,
                reportingAmount,
                reportingFx,
                suggested,
                "applied",
                charge.ReasonCode,
                charge.ReasonMessageVi,
                charge.ActivationEvidenceRef,
                charge.AppliedQuantity));
        }

        return new SurchargeApplyResult(details, cardTotal, trace, evaluation);
    }

    private static RatingDetail BuildDetail(
        Guid tenantId,
        SurchargeCandidate rule,
        string nature,
        decimal cardAmount,
        string cardCurrency,
        string? formula,
        decimal? original,
        string? originalCcy,
        decimal? reportingAmount,
        FxStamp reportingFx,
        Guid? suggested,
        string bucket,
        string reasonCode,
        string reasonVi,
        string? evidence,
        decimal? qty) =>
        new()
        {
            TenantId = tenantId,
            RuleCode = rule.Code,
            ComponentCode = rule.Code,
            ComponentName = rule.Name,
            FinancialNature = nature,
            FinancialMaturity = "expected",
            Amount = cardAmount,
            CurrencyCode = cardCurrency,
            FormulaText = formula,
            SourceType = RatingSourceTypes.Surcharge,
            SourceId = rule.SurchargeId,
            SourceVersionId = rule.VersionId,
            SourceRuleId = rule.RuleId,
            AmountOriginal = original,
            OriginalCurrency = originalCcy,
            ReportingAmount = reportingAmount,
            LineFxRate = reportingFx.Rate,
            LineFxAsOf = reportingFx.AsOf,
            LineFxSource = reportingFx.Source,
            VatRate = rule.VatRate,
            PartnerSuggestedId = suggested,
            EconomicChargeTypeId = rule.EconomicChargeTypeId,
            ApplicationBucket = bucket,
            ActivationPolicySnapshot = rule.ActivationPolicy,
            ActivationEvidenceRef = evidence,
            ReasonCode = reasonCode,
            ReasonMessageVi = reasonVi,
            CalculationBasis = rule.Basis,
            AppliedQuantity = qty
        };

    private static async Task<IReadOnlyList<ServiceSelectionSnapshot>> LoadSelectionsAsync(
        ILcmsDbContext db,
        Guid billId,
        Guid? shipmentId,
        Guid? legId,
        CancellationToken cancellationToken)
    {
        var query = db.ServiceSelections.AsNoTracking()
            .Where(s =>
                (s.ContextType == ServiceSelectionContexts.Bill && s.ContextId == billId)
                || (shipmentId != null && s.ContextType == ServiceSelectionContexts.Shipment && s.ContextId == shipmentId)
                || (legId != null && s.ContextType == ServiceSelectionContexts.Leg && s.ContextId == legId));
        var rows = await query.ToListAsync(cancellationToken);
        return rows.Select(s => new ServiceSelectionSnapshot(
            s.ContextType,
            s.ContextId,
            s.ServiceCode,
            s.Scope,
            s.Quantity,
            s.IsSelected,
            s.Revision,
            s.ConfirmedByUserId,
            s.ConfirmedAt)).ToList();
    }

    private static async Task<bool?> IsDangerousAsync(
        ILcmsDbContext db,
        Bill bill,
        string? commodityCode,
        CancellationToken cancellationToken)
    {
        if (bill.CommodityTypeId is Guid commodityId)
        {
            var row = await db.CommodityTypes.AsNoTracking()
                .Where(c => c.Id == commodityId)
                .Select(c => (bool?)c.IsDangerousGoods)
                .FirstOrDefaultAsync(cancellationToken);
            return row;
        }

        if (string.IsNullOrWhiteSpace(commodityCode))
        {
            return null;
        }

        var byCode = await db.CommodityTypes.AsNoTracking()
            .Where(c => c.Code == commodityCode)
            .Select(c => (bool?)c.IsDangerousGoods)
            .FirstOrDefaultAsync(cancellationToken);
        return byCode;
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
