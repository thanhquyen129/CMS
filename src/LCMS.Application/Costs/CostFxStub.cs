using LCMS.Application.Common.Exceptions;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Costs;

/// <summary>
/// Stub FX: fills Cost.BaseAmount (+ leaves FxRateId null until real FX table).
/// Same-currency ⇒ BaseAmount = amount; else amount × configured stub rate.
/// </summary>
public interface ICostFxStub
{
    string BaseCurrency { get; }

    /// <summary>Returns base equivalent; throws VI validation when rate missing.</summary>
    decimal ToBaseAmount(string currencyCode, decimal amount);

    void ApplyToCost(Domain.Entities.Cost cost, decimal amountInTxnCurrency);
}

public sealed class CostFxStub : ICostFxStub
{
    private readonly CostOptions _options;

    public CostFxStub(IOptions<CostOptions> options)
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
                    $"Chưa có tỷ giá stub quy đổi từ {currency} sang {BaseCurrency}. Khai báo Cost:StubFxRatesToBase."
                ]
            });
        }

        return decimal.Round(rounded * rate, 4, MidpointRounding.AwayFromZero);
    }

    public void ApplyToCost(Domain.Entities.Cost cost, decimal amountInTxnCurrency)
    {
        cost.BaseAmount = ToBaseAmount(cost.CurrencyCode, amountInTxnCurrency);
        // Stub: no persisted fx_rates row yet — leave FxRateId null (ADR-0004).
        cost.FxRateId = null;
    }
}
