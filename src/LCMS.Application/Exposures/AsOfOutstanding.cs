using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures;

/// <summary>
/// Reconstructs AP/AR outstanding at an as-of date so settlements finalized later are excluded (CR-12/13).
/// </summary>
public static class AsOfOutstanding
{
    public static async Task<Dictionary<Guid, decimal>> SettledByPayableAsync(
        ILcmsDbContext db,
        IReadOnlyCollection<Guid> payableIds,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        if (payableIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var rows = await db.PaymentAllocations.AsNoTracking()
            .Where(a => payableIds.Contains(a.AccountsPayableId)
                && a.AllocationStatus == SettlementAllocationStatuses.Finalized
                && a.FinalizedAt != null)
            .Select(a => new
            {
                a.AccountsPayableId,
                Amount = a.SettledAmount ?? a.Amount,
                a.FinalizedAt,
                a.ReversedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(a => WasSettledAt(a.FinalizedAt, a.ReversedAt, asOf))
            .GroupBy(a => a.AccountsPayableId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
    }

    public static async Task<Dictionary<Guid, decimal>> SettledByReceivableAsync(
        ILcmsDbContext db,
        IReadOnlyCollection<Guid> receivableIds,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        if (receivableIds.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var rows = await db.CollectionAllocations.AsNoTracking()
            .Where(a => receivableIds.Contains(a.AccountsReceivableId)
                && a.AllocationStatus == SettlementAllocationStatuses.Finalized
                && a.FinalizedAt != null)
            .Select(a => new
            {
                a.AccountsReceivableId,
                Amount = a.SettledAmount ?? a.Amount,
                a.FinalizedAt,
                a.ReversedAt
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(a => WasSettledAt(a.FinalizedAt, a.ReversedAt, asOf))
            .GroupBy(a => a.AccountsReceivableId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
    }

    public static decimal Outstanding(
        decimal recognized,
        decimal adjustment,
        decimal settledAtAsOf) =>
        decimal.Round(recognized + adjustment - settledAtAsOf, 4, MidpointRounding.AwayFromZero);

    private static bool WasSettledAt(DateTimeOffset? finalizedAt, DateTimeOffset? reversedAt, DateOnly asOf)
    {
        if (!finalizedAt.HasValue)
        {
            return false;
        }

        if (DateOnly.FromDateTime(finalizedAt.Value.UtcDateTime) > asOf)
        {
            return false;
        }

        if (reversedAt.HasValue && DateOnly.FromDateTime(reversedAt.Value.UtcDateTime) <= asOf)
        {
            return false;
        }

        return true;
    }
}
