using LCMS.Application.Fx;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Revenues;

/// <summary>
/// FX snapshot for Revenue to the tenant reporting currency (ADR-0040): identity, dated fx_rates,
/// config policy fallback (non-production), or a user-entered manual/override rate.
/// </summary>
public interface IRevenueFxStub
{
    /// <summary>Tenant reporting currency.</summary>
    string BaseCurrency { get; }

    decimal ToBaseAmount(string currencyCode, decimal amount);

    Task<decimal> ToBaseAmountAsync(
        string currencyCode,
        decimal amount,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    /// <summary>Keeps the existing snapshot when valid (FX-ARCH-07); resolves one otherwise.</summary>
    Task ApplyToRevenueAsync(
        Domain.Entities.Revenue revenue,
        decimal amountInTxnCurrency,
        CancellationToken cancellationToken = default);

    Task ApplyToRevenueAsync(
        Domain.Entities.Revenue revenue,
        decimal amountInTxnCurrency,
        FxManualInput? manual,
        CancellationToken cancellationToken = default);
}

public sealed class RevenueFxStub : IRevenueFxStub
{
    private readonly RevenueOptions _options;
    private readonly IFxSnapshotService _fx;
    private readonly IReportingCurrencyProvider _reporting;

    public RevenueFxStub(IOptions<RevenueOptions> options, IFxSnapshotService fx, IReportingCurrencyProvider reporting)
    {
        _options = options.Value;
        _fx = fx;
        _reporting = reporting;
    }

    public string BaseCurrency => _reporting.Get();

    private FxStubPolicy Stub => new(
        string.IsNullOrWhiteSpace(_options.BaseCurrency) ? "VND" : _options.BaseCurrency.Trim().ToUpperInvariant(),
        _options.AllowStubFxFallback,
        _options.StubFxRatesToBase,
        RevenueOptions.SectionName);

    public decimal ToBaseAmount(string currencyCode, decimal amount) =>
        FxStubMath.ConvertWithStubOnly(currencyCode, amount, BaseCurrency, Stub);

    public async Task<decimal> ToBaseAmountAsync(
        string currencyCode,
        decimal amount,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var snap = await _fx.ResolveAsync(new FxResolveRequest(currencyCode, asOf, Stub: Stub), cancellationToken);
        return FxMath.ToReporting(decimal.Round(amount, 4, MidpointRounding.AwayFromZero), snap.Rate);
    }

    public Task ApplyToRevenueAsync(
        Domain.Entities.Revenue revenue,
        decimal amountInTxnCurrency,
        CancellationToken cancellationToken = default) =>
        ApplyToRevenueAsync(revenue, amountInTxnCurrency, null, cancellationToken);

    public async Task ApplyToRevenueAsync(
        Domain.Entities.Revenue revenue,
        decimal amountInTxnCurrency,
        FxManualInput? manual,
        CancellationToken cancellationToken = default)
    {
        var rounded = decimal.Round(amountInTxnCurrency, 4, MidpointRounding.AwayFromZero);
        var snap = manual is { HasRate: true } ? null : _fx.FromRecord(revenue);
        if (snap is null)
        {
            snap = await _fx.ResolveAsync(
                new FxResolveRequest(revenue.CurrencyCode, revenue.EffectiveDate, manual?.Rate, manual?.Reason, Stub),
                cancellationToken);
            _fx.Apply(revenue, snap, "revenue", revenue.Id);
        }

        revenue.BaseAmount = FxMath.ToReporting(rounded, snap.Rate);
    }
}
