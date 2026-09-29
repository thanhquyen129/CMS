using LCMS.Application.Fx;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Costs;

/// <summary>
/// FX snapshot for Cost to the tenant reporting currency (ADR-0040): identity, dated fx_rates,
/// config policy fallback (non-production), or a user-entered manual/override rate.
/// </summary>
public interface ICostFxStub
{
    /// <summary>Tenant reporting currency.</summary>
    string BaseCurrency { get; }

    /// <summary>Sync stub-only (approval gate fallback when BaseAmount unset).</summary>
    decimal ToBaseAmount(string currencyCode, decimal amount);

    Task<decimal> ToBaseAmountAsync(
        string currencyCode,
        decimal amount,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    /// <summary>Keeps the existing snapshot when valid (FX-ARCH-07); resolves one otherwise.</summary>
    Task ApplyToCostAsync(
        Domain.Entities.Cost cost,
        decimal amountInTxnCurrency,
        CancellationToken cancellationToken = default);

    Task ApplyToCostAsync(
        Domain.Entities.Cost cost,
        decimal amountInTxnCurrency,
        FxManualInput? manual,
        CancellationToken cancellationToken = default);
}

public sealed class CostFxStub : ICostFxStub
{
    private readonly CostOptions _options;
    private readonly IFxSnapshotService _fx;
    private readonly IReportingCurrencyProvider _reporting;

    public CostFxStub(IOptions<CostOptions> options, IFxSnapshotService fx, IReportingCurrencyProvider reporting)
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
        CostOptions.SectionName);

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

    public Task ApplyToCostAsync(
        Domain.Entities.Cost cost,
        decimal amountInTxnCurrency,
        CancellationToken cancellationToken = default) =>
        ApplyToCostAsync(cost, amountInTxnCurrency, null, cancellationToken);

    public async Task ApplyToCostAsync(
        Domain.Entities.Cost cost,
        decimal amountInTxnCurrency,
        FxManualInput? manual,
        CancellationToken cancellationToken = default)
    {
        var rounded = decimal.Round(amountInTxnCurrency, 4, MidpointRounding.AwayFromZero);
        var snap = manual is { HasRate: true } ? null : _fx.FromRecord(cost);
        if (snap is null)
        {
            snap = await _fx.ResolveAsync(
                new FxResolveRequest(cost.CurrencyCode, cost.EffectiveDate, manual?.Rate, manual?.Reason, Stub),
                cancellationToken);
            _fx.Apply(cost, snap, "cost", cost.Id);
        }

        cost.BaseAmount = FxMath.ToReporting(rounded, snap.Rate);
    }
}
