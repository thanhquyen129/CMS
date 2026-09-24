using LCMS.Application.Common.Exceptions;
using LCMS.Application.Fx;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Revenues;

/// <summary>
/// FX conversion for Revenue: dated fx_rates first; StubFxRatesToBase fallback (FxRateId null).
/// </summary>
public interface IRevenueFxStub
{
    string BaseCurrency { get; }

    decimal ToBaseAmount(string currencyCode, decimal amount);

    Task<decimal> ToBaseAmountAsync(
        string currencyCode,
        decimal amount,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    Task ApplyToRevenueAsync(
        Domain.Entities.Revenue revenue,
        decimal amountInTxnCurrency,
        CancellationToken cancellationToken = default);
}

public sealed class RevenueFxStub : IRevenueFxStub
{
    private readonly RevenueOptions _options;
    private readonly IFxRateLookup _lookup;

    public RevenueFxStub(IOptions<RevenueOptions> options, IFxRateLookup lookup)
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

    public async Task<decimal> ToBaseAmountAsync(
        string currencyCode,
        decimal amount,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var currency = currencyCode.Trim().ToUpperInvariant();
        var rounded = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        if (string.Equals(currency, BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return rounded;
        }

        var resolved = await _lookup.ResolveAsync(currency, BaseCurrency, asOf, cancellationToken);
        if (resolved is not null)
        {
            return decimal.Round(rounded * resolved.Rate, 4, MidpointRounding.AwayFromZero);
        }

        return ConvertWithStubOnly(currency, rounded);
    }

    public async Task ApplyToRevenueAsync(
        Domain.Entities.Revenue revenue,
        decimal amountInTxnCurrency,
        CancellationToken cancellationToken = default)
    {
        var currency = revenue.CurrencyCode.Trim().ToUpperInvariant();
        var rounded = decimal.Round(amountInTxnCurrency, 4, MidpointRounding.AwayFromZero);
        if (string.Equals(currency, BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            revenue.BaseAmount = rounded;
            revenue.FxRateId = null;
            return;
        }

        var resolved = await _lookup.ResolveAsync(
            currency,
            BaseCurrency,
            revenue.EffectiveDate,
            cancellationToken);

        if (resolved is not null)
        {
            revenue.BaseAmount = decimal.Round(rounded * resolved.Rate, 4, MidpointRounding.AwayFromZero);
            revenue.FxRateId = resolved.FxRateId;
            return;
        }

        revenue.BaseAmount = ConvertWithStubOnly(currency, rounded);
        revenue.FxRateId = null;
    }

    private decimal ConvertWithStubOnly(string currencyCode, decimal amount)
    {
        var currency = currencyCode.Trim().ToUpperInvariant();
        var rounded = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        if (string.Equals(currency, BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return rounded;
        }

        if (!_options.AllowStubFxFallback)
        {
            throw MissingDatedRate(currency);
        }

        var rates = _options.StubFxRatesToBase ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (!rates.TryGetValue(currency, out var rate) || rate <= 0)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["CurrencyCode"] =
                [
                    $"Chưa có tỷ giá quy đổi từ {currency} sang {BaseCurrency}. Thêm fx_rates hoặc khai báo Revenue:StubFxRatesToBase."
                ]
            });
        }

        return decimal.Round(rounded * rate, 4, MidpointRounding.AwayFromZero);
    }

    private ValidationAppException MissingDatedRate(string currency) =>
        new(new Dictionary<string, string[]>
        {
            ["CurrencyCode"] =
            [
                $"Chưa có tỷ giá ngày hiệu lực từ {currency} sang {BaseCurrency}. Khai báo trên sổ tỷ giá trước khi ghi số tiền."
            ]
        });
}
