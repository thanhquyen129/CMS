using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;

namespace LCMS.Application.RateCards;

public static class RateRuleConflicts
{
    public const string Code = "rate_conflict";

    public const string Message =
        "RATE_CONFLICT: Hai quy tắc cùng khoản mục, cùng điều kiện và nấc trọng lượng chồng nhau. Không phát hành khi chưa có thứ tự ưu tiên.";

    public static string? Find(IReadOnlyList<PricingRule> rules)
    {
        var basic = rules
            .Where(r => r.DeletedAt is null && r.IsActive && !RateDeclarationPolicy.IsMatrixMethod(r.CalcMethod))
            .ToList();
        for (var i = 0; i < basic.Count; i++)
        {
            for (var j = i + 1; j < basic.Count; j++)
            {
                if (Conflicts(basic[i], basic[j]))
                {
                    return Message;
                }
            }
        }

        return null;
    }

    public static bool Conflicts(PricingRule left, PricingRule right)
    {
        var charge = Charge(left);
        if (charge is null || charge != Charge(right))
        {
            return false;
        }

        if (RatingEngine.Specificity(left) != RatingEngine.Specificity(right))
        {
            return false;
        }

        if (!Same(left.ServiceTypeCode, right.ServiceTypeCode)) return false;
        if (!Same(left.TransportMode, right.TransportMode)) return false;
        if (!Same(left.CommodityCode, right.CommodityCode)) return false;
        if (!Same(left.WeightBasis, right.WeightBasis)) return false;
        if (!SameRoute(left, right)) return false;
        if (!SameCarrier(left, right)) return false;
        if (!WeightBand.Overlaps(left.WeightMin, left.WeightMax, right.WeightMin, right.WeightMax)) return false;
        return true;
    }

    private static string? Charge(PricingRule rule) =>
        string.IsNullOrWhiteSpace(rule.ChargeCode) ? null : rule.ChargeCode.Trim().ToUpperInvariant();

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim() ?? "", right?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);

    private static bool SameRoute(PricingRule left, PricingRule right)
    {
        if (left.AppliesAnyRoute == true || right.AppliesAnyRoute == true)
        {
            return left.AppliesAnyRoute == true && right.AppliesAnyRoute == true;
        }

        if (left.RouteId is Guid a && right.RouteId is Guid b)
        {
            return a == b;
        }

        return Same(left.RouteCode, right.RouteCode);
    }

    private static bool SameCarrier(PricingRule left, PricingRule right)
    {
        if (left.CarrierPartyId is not null || right.CarrierPartyId is not null)
        {
            return left.CarrierPartyId == right.CarrierPartyId;
        }

        return Same(left.CarrierName, right.CarrierName);
    }
}
