using LCMS.Application.Common.Exceptions;
using LCMS.Application.Fx;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Settlements;

/// <summary>
/// FX conversion for Settlement: dated fx_rates first; StubFxRatesToBase fallback (FxRateId null).
/// </summary>
public interface ISettlementFxStub
{
    string BaseCurrency { get; }

    decimal ToBaseAmount(string currencyCode, decimal amount);

    Task ApplyToPaymentAsync(
        Domain.Entities.Payment payment,
        decimal amountInTxnCurrency,
        CancellationToken cancellationToken = default);

    Task ApplyToCollectionAsync(
        Domain.Entities.Collection collection,
        decimal amountInTxnCurrency,
        CancellationToken cancellationToken = default);

    Task ApplyToPaymentAllocationAsync(
        Domain.Entities.PaymentAllocation allocation,
        string currencyCode,
        decimal amountInTxnCurrency,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    Task ApplyToCollectionAllocationAsync(
        Domain.Entities.CollectionAllocation allocation,
        string currencyCode,
        decimal amountInTxnCurrency,
        DateOnly asOf,
        CancellationToken cancellationToken = default);
}

public sealed class SettlementFxStub : ISettlementFxStub
{
    private readonly SettlementOptions _options;
    private readonly IFxRateLookup _lookup;

    public SettlementFxStub(IOptions<SettlementOptions> options, IFxRateLookup lookup)
    {
        _options = options.Value;
        _lookup = lookup;
    }

    public string BaseCurrency =>
        string.IsNullOrWhiteSpace(_options.BaseCurrency)
            ? "VND"
            : _options.BaseCurrency.Trim().ToUpperInvariant();

    public decimal ToBaseAmount(string currencyCode, decimal amount) =>
        ConvertWithStubOnly(currencyCode, amount);

    public Task ApplyToPaymentAsync(
        Domain.Entities.Payment payment,
        decimal amountInTxnCurrency,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            payment.CurrencyCode,
            amountInTxnCurrency,
            payment.ValueDate,
            (baseAmount, fxRateId) =>
            {
                payment.BaseAmount = baseAmount;
                payment.FxRateId = fxRateId;
            },
            cancellationToken);

    public Task ApplyToCollectionAsync(
        Domain.Entities.Collection collection,
        decimal amountInTxnCurrency,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            collection.CurrencyCode,
            amountInTxnCurrency,
            collection.ValueDate,
            (baseAmount, fxRateId) =>
            {
                collection.BaseAmount = baseAmount;
                collection.FxRateId = fxRateId;
            },
            cancellationToken);

    public Task ApplyToPaymentAllocationAsync(
        Domain.Entities.PaymentAllocation allocation,
        string currencyCode,
        decimal amountInTxnCurrency,
        DateOnly asOf,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            currencyCode,
            amountInTxnCurrency,
            asOf,
            (baseAmount, fxRateId) =>
            {
                allocation.BaseAmount = baseAmount;
                allocation.FxRateId = fxRateId;
            },
            cancellationToken);

    public Task ApplyToCollectionAllocationAsync(
        Domain.Entities.CollectionAllocation allocation,
        string currencyCode,
        decimal amountInTxnCurrency,
        DateOnly asOf,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            currencyCode,
            amountInTxnCurrency,
            asOf,
            (baseAmount, fxRateId) =>
            {
                allocation.BaseAmount = baseAmount;
                allocation.FxRateId = fxRateId;
            },
            cancellationToken);

    private async Task ApplyAsync(
        string currencyCode,
        decimal amountInTxnCurrency,
        DateOnly asOf,
        Action<decimal, Guid?> assign,
        CancellationToken cancellationToken)
    {
        var currency = currencyCode.Trim().ToUpperInvariant();
        var rounded = decimal.Round(amountInTxnCurrency, 4, MidpointRounding.AwayFromZero);
        if (string.Equals(currency, BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            assign(rounded, null);
            return;
        }

        var resolved = await _lookup.ResolveAsync(currency, BaseCurrency, asOf, cancellationToken);
        if (resolved is not null)
        {
            assign(
                decimal.Round(rounded * resolved.Rate, 4, MidpointRounding.AwayFromZero),
                resolved.FxRateId);
            return;
        }

        assign(ConvertWithStubOnly(currency, rounded), null);
    }

    private decimal ConvertWithStubOnly(string currencyCode, decimal amount)
    {
        var currency = currencyCode.Trim().ToUpperInvariant();
        var rounded = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        if (string.Equals(currency, BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return rounded;
        }

        var rates = _options.StubFxRatesToBase ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (!rates.TryGetValue(currency, out var rate) || rate <= 0)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["CurrencyCode"] =
                [
                    $"Chưa có tỷ giá quy đổi từ {currency} sang {BaseCurrency}. Thêm fx_rates hoặc khai báo Settlement:StubFxRatesToBase."
                ]
            });
        }

        return decimal.Round(rounded * rate, 4, MidpointRounding.AwayFromZero);
    }
}
