using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;

namespace LCMS.Application.Ratings;

/// <summary>Shared EconomicIdentity + applied-bucket helpers for seed/apply (ADR-0043).</summary>
public static class RatingFinancialApply
{
    public static IReadOnlyList<RatingDetail> FilterSeedableDetails(
        IEnumerable<RatingDetail> details,
        string financialNature) =>
        details
            .Where(d => string.Equals(d.FinancialNature, financialNature, StringComparison.OrdinalIgnoreCase))
            .Where(d => RatingApplicationBuckets.MaySeedToFinancials(d.ApplicationBucket))
            .GroupBy(d => (
                Code: (d.ComponentCode ?? string.Empty).Trim().ToUpperInvariant(),
                SourceType: d.SourceType ?? string.Empty,
                SourceId: d.SourceId,
                SourceVersionId: d.SourceVersionId,
                SourceRuleId: d.SourceRuleId))
            .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
            .OrderBy(d => d.ComponentCode)
            .ToList();

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

    public static bool HasProtectedMaturity(Cost cost) =>
        !string.Equals(cost.FinancialMaturity, CostMaturities.Expected, StringComparison.OrdinalIgnoreCase)
        || cost.ConfirmedAmount is not null
        || cost.ActualAmount is not null;

    public static bool HasProtectedMaturity(Revenue revenue) =>
        !string.Equals(revenue.FinancialMaturity, RevenueMaturities.Expected, StringComparison.OrdinalIgnoreCase)
        || revenue.ConfirmedAmount is not null
        || revenue.ActualAmount is not null;

    public static string CostOccurrenceKey(Rating rating) =>
        rating.TransportLegId is Guid leg ? $"leg:{leg:N}" : $"bill:{rating.BillId:N}";

    public static string RevenueOccurrenceKey(Rating rating, RatingDetail detail) =>
        $"bill:{rating.BillId:N}:{detail.ComponentCode}";
}
