using LCMS.Application.Common.Exceptions;
using LCMS.Application.Fx;
using LCMS.Domain.Entities;

namespace LCMS.Application.Settlements;

public static class SettlementCurrency
{
    public static async Task StampAsync(
        IFxRateLookup fx,
        string sourceCurrency,
        string targetCurrency,
        decimal sourceAmount,
        DateOnly asOf,
        Action<decimal, decimal, decimal, string, DateOnly, Guid?> write,
        CancellationToken cancellationToken)
    {
        var source = sourceCurrency.Trim().ToUpperInvariant();
        var target = targetCurrency.Trim().ToUpperInvariant();
        var original = decimal.Round(sourceAmount, 4, MidpointRounding.AwayFromZero);
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            write(original, original, 1m, "identity", asOf, null);
            return;
        }

        var rate = await fx.ResolveAsync(source, target, asOf, cancellationToken)
            ?? throw new ConflictAppException("Không phân bổ khác tiền tệ (C-014).");
        var settled = decimal.Round(original * rate.Rate, 4, MidpointRounding.AwayFromZero);
        write(original, settled, rate.Rate, rate.Source, rate.RateDate, rate.FxRateId);
    }

    public static decimal TargetAmount(PaymentAllocation allocation) =>
        allocation.SettledAmount ?? allocation.Amount;

    public static decimal TargetAmount(CollectionAllocation allocation) =>
        allocation.SettledAmount ?? allocation.Amount;
}
