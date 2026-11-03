using LCMS.Domain.Pricing;

namespace LCMS.Application.Financials;

public sealed record EconomicFactCandidate(
    Guid Id,
    string Side,
    string EconomicIdentityKey,
    string PeakMaturity,
    decimal ExpectedAmount,
    decimal? ConfirmedAmount,
    decimal? ActualAmount,
    string RecordStatus,
    string? SourceType,
    Guid? SourceId,
    Guid? AppliedFromRatingId,
    DateTimeOffset CreatedAt,
    bool Ambiguous = false);

public sealed record EffectiveFactLine(
    Guid Id,
    string Side,
    string EconomicIdentityKey,
    string PeakMaturity,
    decimal BestAvailableAmount,
    decimal ExpectedAmount,
    decimal? ConfirmedAmount,
    decimal? ActualAmount,
    bool EffectiveIncluded,
    string? ExclusionReason,
    string? SourceType,
    Guid? SourceId,
    Guid? AppliedFromRatingId,
    bool PartialActualRemaining = false,
    decimal? RemainingExpectedAmount = null);

public static class EffectiveFactSelector
{
    public static IReadOnlyList<EffectiveFactLine> Select(IEnumerable<EconomicFactCandidate> candidates)
    {
        var list = candidates.ToList();
        var results = new List<EffectiveFactLine>(list.Count);

        foreach (var group in list.GroupBy(c => c.EconomicIdentityKey, StringComparer.Ordinal))
        {
            var members = group.ToList();

            if (members.Any(m => m.Ambiguous))
            {
                foreach (var m in members)
                {
                    results.Add(ToLine(m, included: false, EffectiveExclusionReasons.AmbiguousPendingResolution));
                }

                continue;
            }

            var active = members.Where(m => EconomicRecordStatuses.IsActive(m.RecordStatus)).ToList();
            foreach (var m in members.Where(m => !EconomicRecordStatuses.IsActive(m.RecordStatus)))
            {
                var reason = string.Equals(m.RecordStatus, EconomicRecordStatuses.Superseded, StringComparison.OrdinalIgnoreCase)
                    ? EffectiveExclusionReasons.SupersededByNewerExpected
                    : EffectiveExclusionReasons.InactiveRecord;
                results.Add(ToLine(m, included: false, reason));
            }

            if (active.Count == 0)
            {
                continue;
            }

            var withActual = active.Where(m => m.ActualAmount is not null).ToList();
            if (withActual.Count > 0)
            {
                var winner = withActual.OrderByDescending(m => m.CreatedAt).First();
                foreach (var m in active)
                {
                    if (m.Id == winner.Id)
                    {
                        var remaining = winner.ExpectedAmount > winner.ActualAmount
                            ? winner.ExpectedAmount - winner.ActualAmount!.Value
                            : (decimal?)null;
                        results.Add(ToLine(
                            m,
                            included: true,
                            reason: null,
                            partial: remaining is > 0m,
                            remainingExpected: remaining is > 0m ? remaining : null));
                    }
                    else
                    {
                        results.Add(ToLine(m, included: false, EffectiveExclusionReasons.ShadowedByActual));
                    }
                }

                continue;
            }

            var withConfirmed = active.Where(m => m.ConfirmedAmount is not null).ToList();
            if (withConfirmed.Count > 0)
            {
                var winner = withConfirmed.OrderByDescending(m => m.CreatedAt).First();
                foreach (var m in active)
                {
                    results.Add(m.Id == winner.Id
                        ? ToLine(m, included: true, reason: null)
                        : ToLine(m, included: false, EffectiveExclusionReasons.ShadowedByConfirmed));
                }

                continue;
            }

            var winnerExpected = active.OrderByDescending(m => m.CreatedAt).First();
            foreach (var m in active)
            {
                results.Add(m.Id == winnerExpected.Id
                    ? ToLine(m, included: true, reason: null)
                    : ToLine(m, included: false, EffectiveExclusionReasons.SupersededByNewerExpected));
            }
        }

        return results;
    }

    public static decimal SumBestAvailable(IEnumerable<EffectiveFactLine> lines) =>
        decimal.Round(
            lines.Where(l => l.EffectiveIncluded).Sum(l => l.BestAvailableAmount),
            4,
            MidpointRounding.AwayFromZero);

    /// <summary>Expected KPI: active Expected amounts (full Expected on included row; not Actual+Expected).</summary>
    public static decimal SumExpectedLayer(IEnumerable<EffectiveFactLine> lines) =>
        decimal.Round(
            lines.Where(l => l.EffectiveIncluded).Sum(l => l.ExpectedAmount),
            4,
            MidpointRounding.AwayFromZero);

    public static decimal SumActualOnly(IEnumerable<EffectiveFactLine> lines) =>
        decimal.Round(
            lines.Where(l => l.EffectiveIncluded && l.ActualAmount is not null)
                .Sum(l => l.ActualAmount!.Value),
            4,
            MidpointRounding.AwayFromZero);

    public static bool ActualIncomplete(IEnumerable<EffectiveFactLine> lines) =>
        lines.Any(l => l.EffectiveIncluded && l.ActualAmount is null);

    private static decimal ComputeBest(
        decimal expected,
        decimal? confirmed,
        decimal? actual,
        decimal? remainingExpected)
    {
        if (actual is not null)
        {
            return actual.Value + (remainingExpected ?? 0m);
        }

        if (confirmed is not null)
        {
            return confirmed.Value;
        }

        return expected;
    }

    private static EffectiveFactLine ToLine(
        EconomicFactCandidate m,
        bool included,
        string? reason,
        bool partial = false,
        decimal? remainingExpected = null)
    {
        var best = included
            ? ComputeBest(m.ExpectedAmount, m.ConfirmedAmount, m.ActualAmount, remainingExpected)
            : 0m;

        return new EffectiveFactLine(
            m.Id,
            m.Side,
            m.EconomicIdentityKey,
            m.PeakMaturity,
            best,
            m.ExpectedAmount,
            m.ConfirmedAmount,
            m.ActualAmount,
            included,
            reason,
            m.SourceType,
            m.SourceId,
            m.AppliedFromRatingId,
            partial,
            remainingExpected);
    }
}
