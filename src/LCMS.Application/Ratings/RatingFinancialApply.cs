using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;

namespace LCMS.Application.Ratings;

/// <summary>Shared EconomicIdentity + applied-bucket helpers for seed/apply (ADR-0043).</summary>
public static class RatingFinancialApply
{
    public static IReadOnlyList<RatingDetail> FilterSeedableDetails(
        IEnumerable<RatingDetail> details,
        string financialNature)
    {
        var filtered = details
            .Where(d => string.Equals(d.FinancialNature, financialNature, StringComparison.OrdinalIgnoreCase))
            .Where(d => RatingApplicationBuckets.MaySeedToFinancials(d.ApplicationBucket, d.SourceType))
            .ToList();

        // BR-DF-012: one economic item per apply — collapse same charge+source lineage.
        return filtered
            .GroupBy(d => (
                Code: (d.ComponentCode ?? string.Empty).Trim().ToUpperInvariant(),
                SourceType: (d.SourceType ?? string.Empty).Trim().ToLowerInvariant(),
                SourceId: d.SourceId,
                SourceVersionId: d.SourceVersionId,
                SourceRuleId: d.SourceRuleId ?? d.PricingRuleId))
            .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
            .OrderBy(d => d.ComponentCode)
            .ToList();
    }

    /// <summary>
    /// Detect ambiguous duplicate candidates in one apply batch (same charge+amount, missing keys).
    /// </summary>
    public static bool IsAmbiguousBatch(
        IReadOnlyList<RatingDetail> seedable,
        Guid? transportLegId,
        Guid? shipmentId,
        Guid? counterpartyId)
    {
        var groups = seedable
            .GroupBy(d => (
                Code: (d.ComponentCode ?? string.Empty).Trim().ToUpperInvariant(),
                Amount: decimal.Round(d.Amount, 4, MidpointRounding.AwayFromZero)))
            .Where(g => g.Count() > 1)
            .ToList();

        if (groups.Count == 0)
        {
            return false;
        }

        return EconomicIdentity.LacksDistinguishingKeys(
            transportLegId,
            shipmentId,
            counterpartyId,
            serviceOccurrenceKey: null);
    }

    public static string CostIdentityKey(Rating rating, RatingDetail detail, Guid tenantId)
    {
        var occurrence = rating.TransportLegId is Guid leg
            ? $"leg:{leg:N}"
            : null;
        return EconomicIdentity.Build(
            tenantId,
            rating.BillId,
            EconomicIdentity.DirectionBuy,
            detail.ComponentCode,
            rating.TransportLegId,
            rating.ShipmentId,
            rating.VendorPartyId ?? detail.PartnerSuggestedId,
            occurrence);
    }

    public static string RevenueIdentityKey(Rating rating, RatingDetail detail, Guid tenantId) =>
        EconomicIdentity.Build(
            tenantId,
            rating.BillId,
            EconomicIdentity.DirectionSell,
            detail.ComponentCode,
            transportLegId: null,
            shipmentId: null,
            counterpartyId: detail.PartnerSuggestedId,
            serviceOccurrenceKey: $"bill:{rating.BillId:N}:{detail.ComponentCode}");

    public static string ManualCostIdentityKey(
        Guid tenantId,
        Guid? billId,
        string? chargeCode,
        Guid? vendorId,
        Guid costId) =>
        EconomicIdentity.Build(
            tenantId,
            billId,
            EconomicIdentity.DirectionBuy,
            chargeCode ?? "COST",
            null,
            null,
            vendorId,
            serviceOccurrenceKey: $"manual:{costId:N}");

    public static string ManualRevenueIdentityKey(
        Guid tenantId,
        Guid billId,
        string? chargeCode,
        Guid? customerId,
        Guid revenueId) =>
        EconomicIdentity.Build(
            tenantId,
            billId,
            EconomicIdentity.DirectionSell,
            chargeCode ?? "REV",
            null,
            null,
            customerId,
            serviceOccurrenceKey: $"manual:{revenueId:N}");

    public static bool HasProtectedMaturity(Cost cost) =>
        !string.Equals(cost.FinancialMaturity, CostMaturities.Expected, StringComparison.OrdinalIgnoreCase)
        || cost.ConfirmedAmount is not null
        || cost.ActualAmount is not null;

    public static bool HasProtectedMaturity(Revenue revenue) =>
        !string.Equals(revenue.FinancialMaturity, RevenueMaturities.Expected, StringComparison.OrdinalIgnoreCase)
        || revenue.ConfirmedAmount is not null
        || revenue.ActualAmount is not null;

    public static bool IsRatingSourced(string? sourceType) =>
        string.Equals(sourceType, CostSourceTypes.RatingDetail, StringComparison.OrdinalIgnoreCase)
        || string.Equals(sourceType, RevenueSourceTypes.RatingDetail, StringComparison.OrdinalIgnoreCase);

    public static bool IsIndependentManualOrImport(string? sourceType) =>
        string.Equals(sourceType, CostSourceTypes.Manual, StringComparison.OrdinalIgnoreCase)
        || string.Equals(sourceType, RevenueSourceTypes.Manual, StringComparison.OrdinalIgnoreCase)
        || string.Equals(sourceType, "import", StringComparison.OrdinalIgnoreCase)
        || string.Equals(sourceType, "api", StringComparison.OrdinalIgnoreCase);

    public static string CostOccurrenceKey(Rating rating) =>
        rating.TransportLegId is Guid leg ? $"leg:{leg:N}" : $"bill:{rating.BillId:N}";

    public static string RevenueOccurrenceKey(Rating rating, RatingDetail detail) =>
        $"bill:{rating.BillId:N}:{detail.ComponentCode}";

    public static string ExclusionReasonVi(string? code) => code switch
    {
        EffectiveExclusionReasons.ShadowedByActual => "Đã có số liệu Thực tế thay thế",
        EffectiveExclusionReasons.ShadowedByConfirmed => "Đã có số liệu Đã xác nhận thay thế",
        EffectiveExclusionReasons.SupersededByNewerExpected => "Bản dự kiến cũ đã được thay thế (Superseded)",
        EffectiveExclusionReasons.ComparisonOnlyUnapplied => "Bản tính thử chỉ dùng để so sánh (Chưa áp dụng)",
        EffectiveExclusionReasons.AmbiguousPendingResolution => "Đang chờ xử lý ngoại lệ mơ hồ",
        EffectiveExclusionReasons.ManualIndependentProtected => "Khoản thủ công độc lập — không bị Apply Rating gộp",
        EffectiveExclusionReasons.InactiveRecord => "Bản ghi không còn hiệu lực",
        _ => code ?? string.Empty
    };
}
