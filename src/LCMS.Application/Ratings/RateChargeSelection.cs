using LCMS.Application.Surcharges;
using LCMS.Domain.Entities;

namespace LCMS.Application.Ratings;

/// <summary>
/// Legacy published versions still drop keyword surcharges.
/// Explicit versions keep a base line even when the name contains FUEL, REMOTE, or DELIVERY.
/// </summary>
public static class RateChargeSelection
{
    public static bool ExcludeFromBase(
        string? code,
        string? name,
        string? chargeCode,
        string? lineRole,
        bool explicitGroups)
    {
        if (explicitGroups)
        {
            return RateLineRoles.IsSurcharge(lineRole);
        }

        return LegacyComponentClassifier.IsAdditionalCharge(code, name, chargeCode);
    }
}
