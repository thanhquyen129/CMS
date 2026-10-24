using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences.Edit;
using LCMS.Application.Surcharges;
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
    IReadOnlyList<RatingContainerQty>? Containers = null,
    string? ContextType = null,
    Guid? TransportLegId = null,
    Guid? VendorPartyId = null);

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
    public IReadOnlyList<string> ExcludedLegacySurchargeCodes { get; init; } = [];
    public string ContextType { get; init; } = RatingContextTypes.Bill;
    public Guid? TransportLegId { get; init; }
    public Guid? ShipmentId { get; init; }
    public Guid? VendorPartyId { get; init; }
    public int LinkedBillCount { get; init; }
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
        if (card is { IsActive: false })
        {
            throw new ConflictAppException(BillRateNotices.Retired);
        }

        var contextType = RatingContextTypes.Normalize(input.ContextType);
        TransportLeg? leg = null;
        Shipment? shipment = null;
        var linkedBillCount = 0;
        Guid? vendorPartyId = input.VendorPartyId;
        if (contextType == RatingContextTypes.Leg)
        {
            if (input.TransportLegId is not Guid legId || legId == Guid.Empty)
            {
                throw new ConflictAppException("Chọn chặng liên kết với Bill trước khi tính giá mua.");
            }

            var linked = await _db.BillLegLinks.AsNoTracking()
                .AnyAsync(l => l.BillId == bill.Id && l.TransportLegId == legId, cancellationToken);
            if (!linked)
            {
                throw new ConflictAppException("Chặng không liên kết với Bill này.");
            }

            leg = await _db.TransportLegs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == legId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy chặng.");
            shipment = await _db.Shipments.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == leg.ShipmentId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy lô hàng của chặng.");
            linkedBillCount = await _db.BillLegLinks.AsNoTracking()
                .CountAsync(l => l.TransportLegId == leg.Id, cancellationToken);

            if (card is not null && !string.Equals(card.PartyType, "vendor", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictAppException("Tính giá theo chặng chỉ dùng bảng giá mua.");
            }

            if (card?.SupplierPartyId is Guid supplierId)
            {
                if (vendorPartyId is Guid chosen && chosen != supplierId)
                {
                    throw new ConflictAppException("Bảng giá không thuộc vendor đã chọn.");
                }

                vendorPartyId = supplierId;
            }
            else if (card is not null)
            {
                throw new ConflictAppException("Bảng giá mua chưa gắn vendor.");
            }
        }

        var legContext = contextType == RatingContextTypes.Leg;
        var serviceType = N(input.ServiceTypeCode)
            ?? (legContext
                ? N(leg?.ServiceTypeCode) ?? N(shipment?.ServiceTypeCode)
                : N(bill.ServiceTypeCode) ?? N(bill.BillType));
        var partyType = N(card?.PartyType) ?? N(input.PartyTypeCode);
        var originCode = N(input.OriginCode)
            ?? (legContext ? N(leg?.OriginCode) ?? N(shipment?.OriginCode) : N(bill.OriginCode));
        var destinationCode = N(input.DestinationCode)
            ?? (legContext ? N(leg?.DestinationCode) ?? N(shipment?.DestinationCode) : N(bill.DestinationCode));
        var routeCode = N(input.RouteCode)
            ?? (legContext ? N(leg?.RouteCode) ?? N(shipment?.RouteCode) : N(bill.RouteCode))
            ?? (originCode != null && destinationCode != null ? $"{originCode}-{destinationCode}" : null);
        var transportMode = legContext
            ? ShipmentModePolicy.LegRatingMode(leg?.TransportMode, shipment?.TransportMode, input.TransportMode)
            : N(input.TransportMode) ?? N(bill.TransportMode);
        var commodityCode = N(input.CommodityCode);
        var commodityId = legContext
            ? leg?.CommodityTypeId ?? shipment?.CommodityTypeId
            : bill.CommodityTypeId;
        if (commodityCode is null && commodityId is Guid resolvedCommodityId)
        {
            commodityCode = N(await _db.CommodityTypes.AsNoTracking()
                .Where(c => c.Id == resolvedCommodityId)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(cancellationToken));
        }

        var storedRules = await _db.PricingRules.AsNoTracking()
            .Where(r => r.RateVersionId == version.Id && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .ToListAsync(cancellationToken);
        var explicitGroups = version.ExplicitChargeGroups;
        var excludedLegacy = storedRules
            .Where(r => RateChargeSelection.ExcludeFromBase(r.Code, r.Name, r.ChargeCode, r.LineRole, explicitGroups))
            .Select(r => r.Code)
            .ToList();
        var rules = storedRules
            .Where(r => !RateChargeSelection.ExcludeFromBase(r.Code, r.Name, r.ChargeCode, r.LineRole, explicitGroups))
            .ToList();
        var routeId = legContext ? null : bill.RouteId;
        var carrierPartyId = legContext ? shipment?.CarrierPartyId : null;
        var carrierName = legContext ? shipment?.CarrierName : null;
        var applicable = rules
            .Where(r => RatingEngine.Matches(
                r,
                serviceType,
                partyType,
                routeCode,
                transportMode,
                originCode,
                destinationCode,
                commodityCode,
                routeId,
                carrierPartyId,
                carrierName))
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
            selected = applicable;
        }

        var ruleIds = selected.Select(r => r.Id).ToList();
        var rawComponents = await _db.PricingRuleComponents.AsNoTracking()
            .Where(c => ruleIds.Contains(c.PricingRuleId))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);
        var surchargeOnlyRuleIds = explicitGroups
            ? []
            : rawComponents
            .GroupBy(c => c.PricingRuleId)
            .Where(g => g.All(c => LegacyComponentClassifier.IsAdditionalCharge(c.Code, c.Name, null)))
            .Select(g => g.Key)
            .ToHashSet();
        if (surchargeOnlyRuleIds.Count > 0)
        {
            selected = selected.Where(r => !surchargeOnlyRuleIds.Contains(r.Id)).ToList();
            ruleIds = selected.Select(r => r.Id).ToList();
        }

        if (selected.Count == 0 && !missing.Exists(m => m.Field == "rule_match"))
        {
            missing.Add(new RatingMissingFieldDto(
                "rule_match",
                "Quy tắc giá phù hợp",
                [],
                "NO_APPLICABLE_RATE: Không có quy tắc tính giá phù hợp với điều kiện áp dụng.",
                RatingReadinessActions.RateCard));
        }

        excludedLegacy.AddRange(explicitGroups
            ? []
            : rawComponents
            .Where(c => LegacyComponentClassifier.IsAdditionalCharge(c.Code, c.Name, null))
            .Select(c => c.Code));
        var components = rawComponents
            .Where(c => ruleIds.Contains(c.PricingRuleId)
                        && (explicitGroups || !LegacyComponentClassifier.IsAdditionalCharge(c.Code, c.Name, null)))
            .ToList();
        var componentsByRule = components.GroupBy(c => c.PricingRuleId).ToDictionary(g => g.Key, g => g.ToList());
        var breaks = await _db.RateBreaks.AsNoTracking()
            .Where(b => ruleIds.Contains(b.PricingRuleId))
            .ToListAsync(cancellationToken);
        var breaksByRule = breaks.GroupBy(b => b.PricingRuleId).ToDictionary(g => g.Key, g => (IReadOnlyList<RateBreak>)g.ToList());
        var containerPrices = await _db.ContainerRatePrices.AsNoTracking()
            .Where(p => ruleIds.Contains(p.PricingRuleId))
            .ToListAsync(cancellationToken);

        var measureType = contextType == RatingContextTypes.Leg ? OperationalObjectTypes.Leg : OperationalObjectTypes.Bill;
        var measureId = contextType == RatingContextTypes.Leg ? leg!.Id : bill.Id;
        var measures = await _db.OperationalMeasurements.AsNoTracking()
            .Where(m => m.ObjectType == measureType && m.ObjectId == measureId)
            .ToListAsync(cancellationToken);
        if (contextType == RatingContextTypes.Leg
            && shipment is not null
            && !measures.Any(m => m.MeasureCode is MeasureCodes.GrossWeightKg or MeasureCodes.VolumeCbm or MeasureCodes.ChargeableWeightKg))
        {
            measures = await _db.OperationalMeasurements.AsNoTracking()
                .Where(m => m.ObjectType == OperationalObjectTypes.Shipment && m.ObjectId == shipment.Id)
                .ToListAsync(cancellationToken);
        }
        var cwOverride = await _db.OperationalFieldOverrides.AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.ObjectType == measureType && o.ObjectId == measureId && o.FieldCode == OperationalFieldCodes.ChargeableWeightKg,
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
                var legGap = contextType == RatingContextTypes.Leg;
                missing.Add(new RatingMissingFieldDto(
                    OperationalFieldCodes.ChargeableWeightKg,
                    uom == "W/M" ? "Khối tính cước (W/M)" : "Trọng lượng tính cước",
                    needQty,
                    legGap
                        ? "Chặng chưa có trọng lượng tính cước. Không dùng trọng lượng của cả Bill. Vui lòng mở Sửa Chặng để xác nhận."
                        : $"Trọng lượng tính cước chưa xác định — quy tắc {string.Join(", ", needQty)} cần giá trị này. {billCw.MissingReason ?? computed.MissingReason}".Trim(),
                    legGap ? RatingReadinessActions.RatingForm : RatingReadinessActions.EditBill));
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
            var legWeight = contextType == RatingContextTypes.Leg;
            missing.Add(new RatingMissingFieldDto(
                OperationalFieldCodes.GrossWeightKg,
                "Trọng lượng thực (kg)",
                perKg,
                legWeight
                    ? $"Chặng chưa có trọng lượng thực (quy tắc {string.Join(", ", perKg)}). Không dùng trọng lượng của cả Bill."
                    : $"Phụ phí theo kg cần trọng lượng thực (quy tắc {string.Join(", ", perKg)}).",
                legWeight ? RatingReadinessActions.RatingForm : RatingReadinessActions.EditBill));
        }

        var weighed = selected.Where(r => RatingEngine.WeightMatches(r, gross, quantity)).ToList();
        if (selected.Count > 0 && weighed.Count == 0)
        {
            var bands = string.Join(", ", selected.Where(r => r.WeightMin.HasValue || r.WeightMax.HasValue)
                .Select(r => $"[{r.WeightMin ?? 0}, {r.WeightMax?.ToString() ?? "∞"})"));
            var bandInfo = !string.IsNullOrWhiteSpace(bands) ? $" (các nấc hiện hành: {bands})" : "";
            missing.Add(new RatingMissingFieldDto(
                "weight_band",
                "Nấc trọng lượng",
                selected.Select(r => r.Code).ToList(),
                $"NO_APPLICABLE_RATE: Trọng lượng ({quantity:G29} kg) không thuộc nấc nào{bandInfo}. Khoảng nửa mở [từ, đến) không bao gồm cận trên. Hãy tạo phiên bản mới có nấc phù hợp.",
                RatingReadinessActions.RateCard));
            selected = [];
        }
        else if (weighed.Count > 0)
        {
            selected = RatingEngine.Select(weighed);
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
                "COMPOSITE: Quy tắc thiếu cấu phần cước chính.",
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
            RequiresOverride = requiresOverride,
            ExcludedLegacySurchargeCodes = excludedLegacy
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ContextType = contextType,
            TransportLegId = leg?.Id,
            ShipmentId = shipment?.Id,
            VendorPartyId = vendorPartyId,
            LinkedBillCount = linkedBillCount
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
