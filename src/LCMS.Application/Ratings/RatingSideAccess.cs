using LCMS.Application.Abstractions;
using LCMS.Domain.Identity;

namespace LCMS.Application.Ratings;

internal static class RatingSideAccess
{
    public static async Task<(bool Buy, bool Sell, bool Margin)> ResolveAsync(
        IPermissionService permissions,
        CancellationToken cancellationToken)
    {
        var cost = await permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken);
        var revenue = await permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken);
        var buyRate = await permissions.HasPermissionAsync(PermissionCodes.RateBuyRead, cancellationToken);
        var sellRate = await permissions.HasPermissionAsync(PermissionCodes.RateSellRead, cancellationToken);
        return (cost || buyRate, revenue || sellRate, cost && revenue);
    }

    public static bool CanSee(string? partyTypeCode, bool buy, bool sell)
    {
        if (string.Equals(partyTypeCode, "vendor", StringComparison.OrdinalIgnoreCase))
        {
            return buy;
        }

        if (string.Equals(partyTypeCode, "customer", StringComparison.OrdinalIgnoreCase))
        {
            return sell;
        }

        return buy && sell;
    }
}
