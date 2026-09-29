using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences.Edit;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings;

/// <summary>A rating input the selected rule needs but the Bill does not have (ADR-0039 D03).</summary>
public sealed record RatingMissingFieldDto(
    string Field,
    string Label,
    IReadOnlyList<string> RuleCodes,
    string Message,
    string Action);

public static class RatingReadinessActions
{
    public const string EditBill = "edit_bill";
    public const string RatingForm = "rating_form";
    public const string RateCard = "rate_card";
}

/// <summary>409 rating_not_ready — lists missing fields; no 0/default rating is created.</summary>
public sealed class RatingNotReadyAppException : AppException
{
    public IReadOnlyList<RatingMissingFieldDto> Missing { get; }

    public RatingNotReadyAppException(IReadOnlyList<RatingMissingFieldDto> missing)
        : base(BuildMessage(missing), 409, "rating_not_ready")
    {
        Missing = missing;
    }

    private static string BuildMessage(IReadOnlyList<RatingMissingFieldDto> missing) =>
        missing.Count == 0
            ? "Chưa đủ dữ liệu để tính giá."
            : string.Join(" ", missing.Select(m => m.Message));
}

public sealed record RatingInput(
    Guid BillId,
    Guid RateVersionId,
    decimal? Quantity = null,
    decimal? Weight = null,
    string? ServiceTypeCode = null,
    string? PartyTypeCode = null,
    string? RouteCode = null,
    DateTimeOffset? RateDate = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? TransportMode = null,
    string? CommodityCode = null,
    decimal? GrossWeightKg = null,
    decimal? VolumeCbm = null,
    string? ChargeableOverrideReason = null,
    IReadOnlyList<RatingContainerQty>? Containers = null);

public sealed class ResolvedRatingContext
{
    public required Bill Bill { get; init; }
    public required RateVersion Version { get; init; }
    public RateCard? Card { get; init; }
    public DateTimeOffset RateDate { get; init; }
    public string? ServiceType { get; init; }
    public string? PartyType { get; init; }
    public string? RouteCode { get; init; }
    public string? TransportMode { get; init; }
    public string? OriginCode { get; init; }
    public string? DestinationCode { get; init; }
    public string? CommodityCode { get; init; }
    public IReadOnlyList<PricingRule> Selected { get; init; } = [];
    public IReadOnlyDictionary<Guid, List<PricingRuleComponent>> ComponentsByRule { get; init; } = new Dictionary<Guid, List<PricingRuleComponent>>();
    public IReadOnlyDictionary<Guid, IReadOnlyList<RateBreak>> BreaksByRule { get; init; } = new Dictionary<Guid, IReadOnlyList<RateBreak>>();
    public IReadOnlyList<ContainerRatePrice> ContainerPrices { get; init; } = [];
    public decimal? Gross { get; init; }
    public decimal? Volume { get; init; }
    public required ChargeableWeightStateDto BillChargeable { get; init; }
    public decimal? Quantity { get; init; }
    public string Basis { get; init; } = "missing";
    public string? QuantityRuleCode { get; init; }
    public bool RequiresOverride { get; init; }
    public List<RatingMissingFieldDto> Missing { get; } = [];
}

/// <summary>Resolves the rating context of the rule actually selected — no global hard-coded requirements.</summary>
public sealed class RatingContextResolver
{
    private readonly ILcmsDbContext _db;

    public RatingContextResolver(ILcmsDbContext db)
    {
        _db = db;
    }

    public async Task<ResolvedRatingContext> ResolveAsync(RatingInput input, CancellationToken cancellationToken)
    {
        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == input.BillId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy Bill.");
        var version = await _db.RateVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == input.RateVersionId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        if (!version.IsPublished)
        {
            throw new ConflictAppException("Chỉ được tính giá trên phiên bản bảng giá đã phát hành.");
        }

        var rateDate = input.RateDate ?? DateTimeOffset.UtcNow;
        if (version.EffectiveFrom is DateTimeOffset from && rateDate < from)
        {
            throw new ConflictAppException("Phiên bản bảng giá chưa đến ngày hiệu lực.");
        }

        if (version.EffectiveTo is DateTimeOffset to && rateDate > to)
        {
            throw new ConflictAppException("Phiên bản bảng giá không còn hiệu lực theo ngày áp dụng.");
        }

        var card = await _db.RateCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == version.RateCardId, cancellationToken);

        var serviceType = N(input.ServiceTypeCode) ?? N(bill.ServiceTypeCode) ?? N(bill.BillType);
        var partyType = N(input.PartyTypeCode) ?? N(card?.PartyType);
        var routeCode = N(input.RouteCode) ?? N(bill.RouteCode);
        var transportMode = N(input.TransportMode) ?? N(bill.TransportMode);
        var originCode = N(input.OriginCode) ?? N(bill.OriginCode);
        var destinationCode = N(input.DestinationCode) ?? N(bill.DestinationCode);
        var commodityCode = N(input.CommodityCode);
        if (commodityCode is null && bill.CommodityTypeId is Guid commodityId)
        {
            commodityCode = N(await _db.CommodityTypes.AsNoTracking()
                .Where(c => c.Id == commodityId)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(cancellationToken));
        }

        var rules = await _db.PricingRules.AsNoTracking()
            .Where(r => r.RateVersionId == version.Id && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .ToListAsync(cancellationToken);
        var applicable = rules
            .Where(r => RatingEngine.Matches(r, serviceType, partyType, routeCode, transportMode, originCode, destinationCode, commodityCode))
            .ToList();

        var missing = new List<RatingMissingFieldDto>();
        IReadOnlyList<PricingRule> selected = [];
        if (applicable.Count == 0)
        {
            missing.AddRange(MissingDimensions(rules, serviceType, partyType, routeCode, transportMode, originCode, destinationCode, commodityCode));
            if (missing.Count == 0)
            {
                missing.Add(new RatingMissingFieldDto(
                    "rule_match",
                    "Quy tắc giá phù hợp",
                    [],
                    "NO_APPLICABLE_RATE: Không có quy tắc tính giá phù hợp với điều kiện áp dụng.",
                    RatingReadinessActions.RateCard));
            }
        }
        else
        {
            selected = RatingEngine.Select(applicable);
        }

        var ruleIds = selected.Select(r => r.Id).ToList();
        var components = await _db.PricingRuleComponents.AsNoTracking()
            .Where(c => ruleIds.Contains(c.PricingRuleId))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);
        var componentsByRule = components.GroupBy(c => c.PricingRuleId).ToDictionary(g => g.Key, g => g.ToList());
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
        var cwOverride = await _db.OperationalFieldOverrides.AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.ObjectType == OperationalObjectTypes.Bill && o.ObjectId == bill.Id && o.FieldCode == OperationalFieldCodes.ChargeableWeightKg,
                cancellationToken);
        var mode = transportMode ?? N(card?.TransportMode);
        var billCw = ChargeableWeightPolicy.Resolve(measures, cwOverride, mode, bill.SourceSystem);
        var gross = input.GrossWeightKg ?? measures.FirstOrDefault(m => m.MeasureCode == MeasureCodes.GrossWeightKg)?.Quantity;
        var volume = input.VolumeCbm ?? measures.FirstOrDefault(m => m.MeasureCode == MeasureCodes.VolumeCbm)?.Quantity;
        var factorRule = selected.FirstOrDefault(r => r.VolumetricFactor is not null || r.RoundingStep is not null);
        var computed = ChargeableWeightPolicy.Compute(mode, gross, volume, factorRule?.VolumetricFactor, factorRule?.RoundingStep);
        var persisted = billCw.Persisted ? billCw.Value : null;

        decimal? quantity;
        string basis;
        string? quantityRule = null;
        var requiresOverride = false;
        if (input.Quantity is decimal asked)
        {
            quantity = asked;
            if (persisted is decimal billValue && billValue != asked)
            {
                basis = "override";
                requiresOverride = true;
                if (string.IsNullOrWhiteSpace(input.ChargeableOverrideReason))
                {
                    missing.Add(new RatingMissingFieldDto(
                        "chargeable_override_reason",
                        "Lý do ghi đè",
                        [],
                        $"Trọng lượng tính cước đã xác nhận / đã có trên Bill là {OperationalFieldValues.FormatDecimal(billValue)} ({billCw.SourceLabel}). Nhập lý do để ghi đè.",
                        RatingReadinessActions.RatingForm));
                }
            }
            else
            {
                basis = persisted is null ? "manual" : billCw.IsConfirmed ? "confirmed" : "measured";
            }
        }
        else if (input.Weight is decimal weight)
        {
            quantity = weight;
            basis = "weight";
        }
        else if (persisted is decimal billValue)
        {
            quantity = billValue;
            basis = billCw.IsConfirmed ? "confirmed" : "measured";
            quantityRule = billCw.RuleCode;
        }
        else if (computed.Value is decimal systemValue)
        {
            quantity = systemValue;
            basis = ChargeableWeightPolicy.IsSea(mode) ? "sea_wm" : "air_volumetric";
            quantityRule = computed.RuleCode;
        }
        else
        {
            quantity = null;
            basis = "missing";
        }

        var needQty = selected.Where(r => NeedsQuantity(r, componentsByRule)).Select(r => r.Code).ToList();
        if (quantity is null)
        {
            if (needQty.Count > 0)
            {
                var uom = ChargeableWeightPolicy.UomFor(mode);
                missing.Add(new RatingMissingFieldDto(
                    OperationalFieldCodes.ChargeableWeightKg,
                    uom == "W/M" ? "Khối tính cước (W/M)" : "Trọng lượng tính cước",
                    needQty,
                    $"Trọng lượng tính cước chưa xác định — quy tắc {string.Join(", ", needQty)} cần giá trị này. {billCw.MissingReason ?? computed.MissingReason}".Trim(),
                    RatingReadinessActions.EditBill));
            }
            else
            {
                quantity = 1m;
                basis = "not_required";
            }
        }

        var perKg = selected
            .Where(r => string.Equals(r.Applicability, RatingEngine.PerGrossKg, StringComparison.OrdinalIgnoreCase)
                        && !componentsByRule.ContainsKey(r.Id)
                        && !string.Equals(r.CalcMethod, PricingCalcMethods.Composite, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Code)
            .ToList();
        if (perKg.Count > 0 && (input.Weight ?? gross) is not > 0m)
        {
            missing.Add(new RatingMissingFieldDto(
                OperationalFieldCodes.GrossWeightKg,
                "Trọng lượng thực (kg)",
                perKg,
                $"Phụ phí theo kg cần trọng lượng thực (quy tắc {string.Join(", ", perKg)}).",
                RatingReadinessActions.EditBill));
        }

        var containerRules = selected
            .Where(r => string.Equals(r.CalcMethod, PricingCalcMethods.ContainerRate, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Code)
            .ToList();
        if (containerRules.Count > 0 && (input.Containers is null || input.Containers.Count == 0))
        {
            missing.Add(new RatingMissingFieldDto(
                "containers",
                "Số lượng theo loại container",
                containerRules,
                "CONTAINER_RATE: Thiếu số lượng theo loại container.",
                RatingReadinessActions.RatingForm));
        }

        var brokenComposite = selected
            .Where(r => string.Equals(r.CalcMethod, PricingCalcMethods.Composite, StringComparison.OrdinalIgnoreCase)
                        && (!componentsByRule.TryGetValue(r.Id, out var parts) || parts.Count == 0))
            .Select(r => r.Code)
            .ToList();
        if (brokenComposite.Count > 0)
        {
            missing.Add(new RatingMissingFieldDto(
                "rule_config",
                "Cấu hình quy tắc giá",
                brokenComposite,
                "COMPOSITE: Quy tắc thiếu thành phần giá.",
                RatingReadinessActions.RateCard));
        }

        var ctx = new ResolvedRatingContext
        {
            Bill = bill,
            Version = version,
            Card = card,
            RateDate = rateDate,
            ServiceType = serviceType,
            PartyType = partyType,
            RouteCode = routeCode,
            TransportMode = transportMode,
            OriginCode = originCode,
            DestinationCode = destinationCode,
            CommodityCode = commodityCode,
            Selected = selected,
            ComponentsByRule = componentsByRule,
            BreaksByRule = breaksByRule,
            ContainerPrices = containerPrices,
            Gross = gross,
            Volume = volume,
            BillChargeable = billCw,
            Quantity = quantity,
            Basis = basis,
            QuantityRuleCode = quantityRule,
            RequiresOverride = requiresOverride
        };
        ctx.Missing.AddRange(missing);
        return ctx;
    }

    /// <summary>Whether the rule multiplies by the freight quantity (CW / W/M).</summary>
    public static bool NeedsQuantity(PricingRule rule, IReadOnlyDictionary<Guid, List<PricingRuleComponent>> componentsByRule)
    {
        var method = rule.CalcMethod;
        if (Is(method, PricingCalcMethods.WeightBreakPivot) || Is(method, PricingCalcMethods.WeightStep))
        {
            return true;
        }

        if (Is(method, PricingCalcMethods.ContainerRate))
        {
            return false;
        }

        if (Is(method, PricingCalcMethods.Composite))
        {
            return componentsByRule.TryGetValue(rule.Id, out var parts)
                   && parts.Any(c => Is(c.CalcMethod, PricingCalcMethods.UnitRate) || Is(c.CalcMethod, PricingCalcMethods.MinMaxClamp));
        }

        var quantityMethod = Is(method, PricingCalcMethods.UnitRate) || Is(method, PricingCalcMethods.MinMaxClamp);
        if (componentsByRule.ContainsKey(rule.Id))
        {
            return quantityMethod;
        }

        return quantityMethod && !string.Equals(rule.Applicability, RatingEngine.PerGrossKg, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<RatingMissingFieldDto> MissingDimensions(
        IReadOnlyList<PricingRule> rules,
        string? serviceType,
        string? partyType,
        string? routeCode,
        string? transportMode,
        string? originCode,
        string? destinationCode,
        string? commodityCode)
    {
        var found = new Dictionary<string, (string Label, List<string> Rules)>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            var dims = new (string Field, string Label, string? Filter, string? Value)[]
            {
                (OperationalFieldCodes.ServiceTypeCode, "Loại dịch vụ", rule.ServiceTypeCode, serviceType),
                ("party_type", "Loại đối tác", rule.PartyTypeCode, partyType),
                (OperationalFieldCodes.RouteCode, "Tuyến", rule.RouteCode, routeCode),
                (OperationalFieldCodes.TransportMode, "Phương thức vận chuyển", rule.TransportMode, transportMode),
                (OperationalFieldCodes.OriginCode, "Điểm đi", rule.OriginCode, originCode),
                (OperationalFieldCodes.DestinationCode, "Điểm đến", rule.DestinationCode, destinationCode),
                (OperationalFieldCodes.CommodityTypeId, "Loại hàng", rule.CommodityCode, commodityCode)
            };
            var conflicting = dims.Any(d => !string.IsNullOrWhiteSpace(d.Filter)
                                            && d.Value is not null
                                            && !string.Equals(d.Filter.Trim(), d.Value.Trim(), StringComparison.OrdinalIgnoreCase));
            if (conflicting)
            {
                continue;
            }

            foreach (var dim in dims.Where(d => !string.IsNullOrWhiteSpace(d.Filter) && d.Value is null))
            {
                if (!found.TryGetValue(dim.Field, out var entry))
                {
                    entry = (dim.Label, []);
                    found[dim.Field] = entry;
                }

                entry.Rules.Add(rule.Code);
            }
        }

        return found.Select(kv => new RatingMissingFieldDto(
            kv.Key,
            kv.Value.Label,
            kv.Value.Rules.Distinct().ToList(),
            $"NO_APPLICABLE_RATE: Thiếu {kv.Value.Label} — quy tắc {string.Join(", ", kv.Value.Rules.Distinct())} yêu cầu.",
            kv.Key == "party_type" ? RatingReadinessActions.RateCard : RatingReadinessActions.EditBill));
    }

    private static bool Is(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
