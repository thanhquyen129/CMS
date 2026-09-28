namespace LCMS.Application.Settlements;

/// <summary>
/// FIN-DATA-01 / ADR-0038: compares the stored AR/AP aggregates (AdjustmentAmount, FinalizedSettledAmount)
/// with totals re-derived from immutable source rows (adjustment rows, finalized allocations).
/// Pure function — no I/O; used by the reconciliation report and the controlled correction command.
/// </summary>
public static class ApArBalanceAnalyzer
{
    public const decimal Tolerance = 0.0001m;

    public const string CauseLegacyCrossCurrencyReversal = "legacy_cross_currency_reversal";
    public const string CauseUnexplained = "unexplained";

    public sealed record AccountSnapshot(
        decimal RecognizedAmount,
        decimal AdjustmentAmount,
        decimal FinalizedSettledAmount,
        string CurrencyCode);

    public sealed record AllocationSnapshot(
        Guid AllocationId,
        Guid CashId,
        decimal CashAmount,
        string CashCurrencyCode,
        decimal? SettledAmount,
        DateTimeOffset? FinalizedAt,
        DateTimeOffset? ReversedAt);

    public sealed record LegacyAllocation(
        Guid AllocationId,
        Guid CashId,
        decimal CashAmount,
        string CashCurrencyCode,
        decimal SettledAmount,
        DateTimeOffset ReversedAt);

    public sealed record Result(
        decimal CurrentOutstanding,
        decimal LedgerBalance,
        decimal Difference,
        decimal DerivedAdjustmentAmount,
        decimal DerivedSettledAmount,
        bool AdjustmentMatches,
        bool SettledMatches,
        IReadOnlyList<LegacyAllocation> LegacyAllocations)
    {
        public bool Reconciled => AdjustmentMatches && SettledMatches;

        /// <summary>Only the settled total drifted and a pre-fix cross-currency reversal explains it.</summary>
        public bool Correctable => AdjustmentMatches && !SettledMatches && LegacyAllocations.Count > 0;

        public string? Cause => Reconciled ? null : Correctable ? CauseLegacyCrossCurrencyReversal : CauseUnexplained;
    }

    public static Result Analyze(
        AccountSnapshot account,
        IEnumerable<decimal> adjustmentDeltas,
        IEnumerable<AllocationSnapshot> allocations)
    {
        var finalized = allocations.Where(a => a.FinalizedAt.HasValue).ToList();
        var derivedAdjustment = Round(adjustmentDeltas.Sum());
        var derivedSettled = Round(finalized
            .Where(a => !a.ReversedAt.HasValue)
            .Sum(a => a.SettledAmount ?? a.CashAmount));

        var legacy = finalized
            .Where(a => a.ReversedAt.HasValue && IsCrossCurrency(a, account.CurrencyCode))
            .Select(a => new LegacyAllocation(
                a.AllocationId,
                a.CashId,
                a.CashAmount,
                a.CashCurrencyCode,
                a.SettledAmount ?? a.CashAmount,
                a.ReversedAt!.Value))
            .ToList();

        var current = account.RecognizedAmount + account.AdjustmentAmount - account.FinalizedSettledAmount;
        var ledger = Round(account.RecognizedAmount + derivedAdjustment - derivedSettled);
        return new Result(
            current,
            ledger,
            Round(ledger - current),
            derivedAdjustment,
            derivedSettled,
            Math.Abs(derivedAdjustment - account.AdjustmentAmount) <= Tolerance,
            Math.Abs(derivedSettled - account.FinalizedSettledAmount) <= Tolerance,
            legacy);
    }

    private static bool IsCrossCurrency(AllocationSnapshot a, string accountCurrency) =>
        !string.Equals(a.CashCurrencyCode?.Trim(), accountCurrency?.Trim(), StringComparison.OrdinalIgnoreCase)
        || (a.SettledAmount.HasValue && Math.Abs(a.SettledAmount.Value - a.CashAmount) > Tolerance);

    private static decimal Round(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
