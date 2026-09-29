using LCMS.Domain.Entities;

namespace LCMS.Application.Fx;

/// <summary>One record converted to the reporting currency with its FX trace (FX-UI-06/07).</summary>
public sealed record ReportingLineDto(
    string Kind,
    Guid Id,
    string? Label,
    string CurrencyCode,
    decimal OriginalAmount,
    decimal? FxRate,
    string? FxSourceType,
    string? FxSourceName,
    DateOnly? FxRateDate,
    decimal? ReportingAmount,
    string FxStatus,
    string? FxOverrideReason = null);

/// <summary>
/// Converts stored amounts with the record's own FX snapshot (FX-ARCH-07/08). Never re-converts with a
/// later rate; rows without a snapshot return null and are counted as missing (FX-ARCH-09).
/// </summary>
public static class ReportingValue
{
    public static bool SameCurrency(string currency, string reporting) =>
        string.Equals(currency?.Trim(), reporting, StringComparison.OrdinalIgnoreCase);

    public static decimal? Of(string currency, decimal amount, string reporting, decimal? fxRate, string? fxStatus)
    {
        if (SameCurrency(currency, reporting))
        {
            return decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        }

        return fxStatus == FxStatuses.Converted && fxRate is { } rate && rate > 0
            ? FxMath.ToReporting(amount, rate)
            : null;
    }

    public static decimal? Of(IReportingFx record, decimal amount, string reporting) =>
        Of(record.CurrencyCode, amount, reporting, record.FxRate, record.FxStatus);

    public static string StatusOf(IReportingFx record, string reporting) =>
        SameCurrency(record.CurrencyCode, reporting) ? FxStatuses.Converted
        : record.FxStatus == FxStatuses.Converted && record.FxRate is > 0 ? FxStatuses.Converted
        : record.FxStatus == FxStatuses.RequiresReview ? FxStatuses.RequiresReview
        : FxStatuses.Missing;

    public static ReportingLineDto Line(string kind, Guid id, string? label, IReportingFx record, decimal amount, string reporting)
    {
        var same = SameCurrency(record.CurrencyCode, reporting);
        return new ReportingLineDto(
            kind,
            id,
            label,
            record.CurrencyCode.ToUpperInvariant(),
            decimal.Round(amount, 4, MidpointRounding.AwayFromZero),
            same ? 1m : record.FxRate,
            same ? FxSourceTypes.Identity : record.FxSourceType,
            same ? null : record.FxSourceName,
            same ? null : record.FxRateDate,
            Of(record, amount, reporting),
            StatusOf(record, reporting),
            same ? null : record.FxOverrideReason);
    }
}

public sealed record AllocationDetailSlice(Guid AllocationId, Guid DetailId, decimal Amount);

public sealed record AllocationFx(string CurrencyCode, decimal? FxRate, string? FxStatus);

public static class ReportingAllocation
{
    /// <summary>
    /// Reporting share per allocation detail: the allocation total is converted once, then split by
    /// largest remainder so shares sum exactly to the source reporting amount (AC-FX-017).
    /// Details whose source cost has no FX snapshot are absent from the result.
    /// </summary>
    public static Dictionary<Guid, decimal> Split(
        IEnumerable<AllocationDetailSlice> details,
        IReadOnlyDictionary<Guid, AllocationFx> fxByAllocation,
        string reporting)
    {
        var result = new Dictionary<Guid, decimal>();
        foreach (var group in details.GroupBy(d => d.AllocationId))
        {
            if (!fxByAllocation.TryGetValue(group.Key, out var fx))
            {
                continue;
            }

            var ordered = group.OrderBy(d => d.DetailId).ToList();
            var total = ReportingValue.Of(fx.CurrencyCode, ordered.Sum(d => d.Amount), reporting, fx.FxRate, fx.FxStatus);
            if (total is null)
            {
                continue;
            }

            var parts = FxMath.SplitReporting(total.Value, ordered.Select(d => d.Amount).ToList());
            for (var i = 0; i < ordered.Count; i++)
            {
                result[ordered[i].DetailId] = parts[i];
            }
        }

        return result;
    }
}

/// <summary>Sum in reporting currency that tracks rows excluded for missing FX (FX-AGG-01).</summary>
public sealed class ReportingSum
{
    public decimal Total { get; private set; }
    public int Missing { get; private set; }
    public bool Complete => Missing == 0;

    public void Add(decimal? value)
    {
        if (value is { } v)
        {
            Total += v;
        }
        else
        {
            Missing++;
        }
    }

    public decimal Rounded => decimal.Round(Total, 4, MidpointRounding.AwayFromZero);
}
