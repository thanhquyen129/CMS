using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;

namespace LCMS.Application.Ratings;

public sealed record RatingContainerQty(string ContainerType, int Quantity);

public sealed record RatingLine(decimal Amount, string Formula, string? Band);

/// <summary>Pure rating math for the six modes. Does not read the database.</summary>
public static class RatingEngine
{
    public const decimal DefaultAirFactor = 167m;

    /// <summary>Unit rate uses gross kg, not the freight quantity (CBM on sea).</summary>
    public const string PerGrossKg = "per_gross_kg";

    public static decimal? Chargeable(
        string? transportMode,
        decimal? grossKg,
        decimal? volumeCbm,
        decimal? volumetricFactor,
        decimal? roundingStep)
    {
        if (grossKg is null && volumeCbm is null)
        {
            return null;
        }

        var mode = transportMode?.Trim().ToLowerInvariant();
        decimal raw;
        if (mode is "sea" or "ocean")
        {
            var tons = (grossKg ?? 0m) / 1000m;
            raw = Math.Max(tons, volumeCbm ?? 0m);
        }
        else
        {
            var volumetric = (volumeCbm ?? 0m) * (volumetricFactor ?? DefaultAirFactor);
            raw = Math.Max(grossKg ?? 0m, volumetric);
        }

        return CeilStep(raw, roundingStep);
    }

    public static decimal CeilStep(decimal value, decimal? step)
    {
        if (step is null || step <= 0)
        {
            return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
        }

        var steps = Math.Ceiling(value / step.Value);
        return decimal.Round(steps * step.Value, 4, MidpointRounding.AwayFromZero);
    }

    public static RatingLine WeightBreakPivot(decimal quantity, IReadOnlyList<RateBreak> breaks, decimal? minCharge)
    {
        var hit = Hit(quantity, breaks);
        var raw = quantity * hit.UnitAmount;
        if (minCharge is decimal min && raw < min)
        {
            raw = min;
        }

        return new RatingLine(Round(raw), $"{quantity} × {hit.UnitAmount}", Band(hit));
    }

    public static RatingLine WeightStep(
        decimal quantity,
        decimal? step,
        decimal unitAmount,
        IReadOnlyList<RateBreak> breaks,
        decimal? minCharge)
    {
        var normalized = CeilStep(quantity, step);
        decimal raw;
        string formula;
        string? band = null;
        if (breaks.Count > 0)
        {
            var hit = Hit(normalized, breaks);
            raw = hit.UnitAmount;
            formula = $"bậc {Band(hit)} = {hit.UnitAmount}";
            band = Band(hit);
        }
        else
        {
            raw = normalized * unitAmount;
            formula = $"{normalized} × {unitAmount}";
        }

        if (minCharge is decimal min && raw < min)
        {
            raw = min;
        }

        return new RatingLine(Round(raw), formula, band);
    }

    public static RatingLine Containers(
        IReadOnlyList<RatingContainerQty> quantities,
        IReadOnlyList<ContainerRatePrice> prices)
    {
        if (quantities.Count == 0)
        {
            throw new ConflictAppException("CONTAINER_RATE: Thiếu số lượng theo loại container.");
        }

        decimal total = 0m;
        var parts = new List<string>();
        foreach (var qty in quantities)
        {
            var price = prices.FirstOrDefault(p =>
                string.Equals(p.ContainerType, qty.ContainerType, StringComparison.OrdinalIgnoreCase));
            if (price is null)
            {
                throw new ConflictAppException($"CONTAINER_RATE: Không có giá cho loại container {qty.ContainerType}.");
            }

            var line = qty.Quantity * price.UnitAmount;
            total += line;
            parts.Add($"{qty.Quantity} × {price.UnitAmount} {qty.ContainerType}");
        }

        return new RatingLine(Round(total), string.Join("; ", parts), null);
    }

    public static IReadOnlyList<PricingRuleComponent> OrderComponents(IReadOnlyList<PricingRuleComponent> components)
    {
        var pending = components.ToList();
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<PricingRuleComponent>();
        while (pending.Count > 0)
        {
            var ready = pending
                .Where(c => string.IsNullOrWhiteSpace(c.DependsOnCode) || done.Contains(c.DependsOnCode))
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Code)
                .ToList();
            if (ready.Count == 0)
            {
                throw new ConflictAppException("COMPOSITE: Vòng phụ thuộc giữa các thành phần giá.");
            }

            foreach (var item in ready)
            {
                ordered.Add(item);
                done.Add(item.Code);
                pending.Remove(item);
            }
        }

        return ordered;
    }

    public static int Specificity(PricingRule rule)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(rule.ServiceTypeCode)) score++;
        if (!string.IsNullOrWhiteSpace(rule.PartyTypeCode)) score++;
        if (!string.IsNullOrWhiteSpace(rule.RouteCode)) score++;
        if (!string.IsNullOrWhiteSpace(rule.TransportMode)) score++;
        if (!string.IsNullOrWhiteSpace(rule.OriginCode)) score++;
        if (!string.IsNullOrWhiteSpace(rule.DestinationCode)) score++;
        if (!string.IsNullOrWhiteSpace(rule.CommodityCode)) score++;
        if (rule.CarrierPartyId is not null || !string.IsNullOrWhiteSpace(rule.CarrierName)) score++;
        if (rule.WeightMin is not null || rule.WeightMax is not null) score++;
        if (!string.IsNullOrWhiteSpace(rule.WeightBasis)) score++;
        return score;
    }

    public static IReadOnlyList<PricingRule> Select(IReadOnlyList<PricingRule> applicable)
    {
        if (applicable.Count == 0)
        {
            throw new ConflictAppException("NO_APPLICABLE_RATE: Không có quy tắc tính giá phù hợp với điều kiện áp dụng.");
        }

        var selected = new List<PricingRule>();
        foreach (var group in applicable.GroupBy(GroupKey))
        {
            var best = group.Max(Specificity);
            var top = group.Where(r => Specificity(r) == best).ToList();
            if (top.Count > 1)
            {
                throw new ConflictAppException("AMBIGUOUS_RATE_RULE: Hai quy tắc cùng độ cụ thể. Không chọn ngẫu nhiên.");
            }

            selected.Add(top[0]);
        }

        return selected
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .ToList();
    }

    public static bool Matches(
        PricingRule rule,
        string? serviceType,
        string? partyType,
        string? routeCode,
        string? transportMode,
        string? originCode,
        string? destinationCode,
        string? commodityCode,
        Guid? routeId = null,
        Guid? carrierPartyId = null,
        string? carrierName = null)
    {
        if (!Eq(rule.ServiceTypeCode, serviceType)) return false;
        if (!Eq(rule.PartyTypeCode, partyType)) return false;
        if (!RouteMatches(rule, routeId, routeCode)) return false;
        if (!CarrierMatches(rule, carrierPartyId, carrierName)) return false;
        if (!Eq(rule.TransportMode, transportMode)) return false;
        if (!Eq(rule.OriginCode, originCode)) return false;
        if (!Eq(rule.DestinationCode, destinationCode)) return false;
        if (!Eq(rule.CommodityCode, commodityCode)) return false;
        return true;
    }

    /// <summary>Explicit ANY matches every route. A stored route is directional. Empty legacy rows still match.</summary>
    public static bool RouteMatches(PricingRule rule, Guid? routeId, string? routeCode)
    {
        if (rule.AppliesAnyRoute == true)
        {
            return true;
        }

        if (rule.RouteId is Guid id)
        {
            if (routeId == id)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(rule.RouteCode) && Eq(rule.RouteCode, routeCode);
        }

        if (!string.IsNullOrWhiteSpace(rule.RouteCode))
        {
            return Eq(rule.RouteCode, routeCode);
        }

        return rule.AppliesAnyRoute is null;
    }

    /// <summary>Empty carrier does not restrict. A carrier id is not the vendor.</summary>
    public static bool CarrierMatches(PricingRule rule, Guid? carrierPartyId, string? carrierName)
    {
        if (rule.CarrierPartyId is Guid id)
        {
            return carrierPartyId == id;
        }

        if (!string.IsNullOrWhiteSpace(rule.CarrierName))
        {
            return string.Equals(rule.CarrierName.Trim(), carrierName?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    /// <summary>
    /// Weight tier is a selection condition. A miss is no price — never the nearest tier.
    /// Gross and chargeable are different sources.
    /// </summary>
    public static bool WeightMatches(PricingRule rule, decimal? grossKg, decimal? chargeableKg)
    {
        if (rule.WeightMin is null && rule.WeightMax is null)
        {
            return true;
        }

        var basis = rule.WeightBasis?.Trim().ToLowerInvariant();
        decimal? value = basis switch
        {
            WeightBand.Gross => grossKg,
            WeightBand.Chargeable => chargeableKg,
            _ => chargeableKg ?? grossKg
        };
        if (value is null || rule.WeightMin is not decimal min)
        {
            return false;
        }

        return WeightBand.Contains(value.Value, min, rule.WeightMax);
    }

    public static decimal Round(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    public static decimal RoundCurrency(decimal value, string currencyCode) =>
        string.Equals(currencyCode, "VND", StringComparison.OrdinalIgnoreCase)
            ? decimal.Round(value, 0, MidpointRounding.AwayFromZero)
            : decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string GroupKey(PricingRule rule) =>
        string.IsNullOrWhiteSpace(rule.ChargeCode)
            ? rule.Id.ToString("N")
            : rule.ChargeCode.Trim().ToUpperInvariant();

    private static bool Eq(string? filter, string? value)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return string.Equals(filter.Trim(), value?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static RateBreak Hit(decimal quantity, IReadOnlyList<RateBreak> breaks)
    {
        var hit = breaks
            .OrderBy(b => b.MinQuantity)
            .FirstOrDefault(b => quantity >= b.MinQuantity && (b.MaxQuantity is null || quantity <= b.MaxQuantity));
        if (hit is null)
        {
            throw new ConflictAppException("NO_APPLICABLE_RATE: Không có bậc trọng lượng phù hợp.");
        }

        return hit;
    }

    private static string Band(RateBreak hit) =>
        hit.MaxQuantity is null ? $"{hit.MinQuantity}+" : $"{hit.MinQuantity}–{hit.MaxQuantity}";
}
