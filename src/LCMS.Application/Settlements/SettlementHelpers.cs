using LCMS.Domain.Entities;

namespace LCMS.Application.Settlements;

internal static class SettlementHelpers
{
    /// <summary>Over-settlement policy stub (C-008): no over-allocation allowed.</summary>
    public const decimal OverSettlementTolerance = 0m;

    public static string DeriveApArSettlementStatus(decimal recognized, decimal adjustment, decimal finalizedSettled)
    {
        var obligation = recognized + adjustment;
        if (finalizedSettled <= 0m)
        {
            return ApArSettlementStatuses.Open;
        }

        if (finalizedSettled >= obligation)
        {
            return ApArSettlementStatuses.Settled;
        }

        return ApArSettlementStatuses.PartiallySettled;
    }

    public static bool IsActiveAllocation(string status) =>
        string.Equals(status, SettlementAllocationStatuses.Draft, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, SettlementAllocationStatuses.Finalized, StringComparison.OrdinalIgnoreCase);

    public static bool IsFinalized(string status) =>
        string.Equals(status, SettlementAllocationStatuses.Finalized, StringComparison.OrdinalIgnoreCase);
}
