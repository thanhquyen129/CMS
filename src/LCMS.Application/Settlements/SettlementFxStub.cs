using LCMS.Application.Common.Exceptions;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Settlements;

/// <summary>
/// Stub FX: fills Payment/Collection/allocation BaseAmount (+ leaves FxRateId null until real FX table).
/// Same-currency ⇒ BaseAmount = amount; else amount × configured stub rate (ADR-0004).
/// </summary>
public interface ISettlementFxStub
{
    string BaseCurrency { get; }

    /// <summary>Returns base equivalent; throws VI validation when rate missing.</summary>
    decimal ToBaseAmount(string currencyCode, decimal amount);

    void ApplyToPayment(Domain.Entities.Payment payment, decimal amountInTxnCurrency);

    void ApplyToCollection(Domain.Entities.Collection collection, decimal amountInTxnCurrency);

    void ApplyToPaymentAllocation(Domain.Entities.PaymentAllocation allocation, string currencyCode, decimal amountInTxnCurrency);

    void ApplyToCollectionAllocation(Domain.Entities.CollectionAllocation allocation, string currencyCode, decimal amountInTxnCurrency);
}

public sealed class SettlementFxStub : ISettlementFxStub
{
    private readonly SettlementOptions _options;

    public SettlementFxStub(IOptions<SettlementOptions> options)
    {
        _options = options.Value;
    }

    public string BaseCurrency =>
        string.IsNullOrWhiteSpace(_options.BaseCurrency)
            ? "VND"
            : _options.BaseCurrency.Trim().ToUpperInvariant();

    public decimal ToBaseAmount(string currencyCode, decimal amount)
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
                    $"Chưa có tỷ giá stub quy đổi từ {currency} sang {BaseCurrency}. Khai báo Settlement:StubFxRatesToBase."
                ]
            });
        }

        return decimal.Round(rounded * rate, 4, MidpointRounding.AwayFromZero);
    }

    public void ApplyToPayment(Domain.Entities.Payment payment, decimal amountInTxnCurrency)
    {
        payment.BaseAmount = ToBaseAmount(payment.CurrencyCode, amountInTxnCurrency);
        payment.FxRateId = null;
    }

    public void ApplyToCollection(Domain.Entities.Collection collection, decimal amountInTxnCurrency)
    {
        collection.BaseAmount = ToBaseAmount(collection.CurrencyCode, amountInTxnCurrency);
        collection.FxRateId = null;
    }

    public void ApplyToPaymentAllocation(
        Domain.Entities.PaymentAllocation allocation,
        string currencyCode,
        decimal amountInTxnCurrency)
    {
        allocation.CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        allocation.BaseAmount = ToBaseAmount(allocation.CurrencyCode, amountInTxnCurrency);
        allocation.FxRateId = null;
    }

    public void ApplyToCollectionAllocation(
        Domain.Entities.CollectionAllocation allocation,
        string currencyCode,
        decimal amountInTxnCurrency)
    {
        allocation.CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        allocation.BaseAmount = ToBaseAmount(allocation.CurrencyCode, amountInTxnCurrency);
        allocation.FxRateId = null;
    }
}
