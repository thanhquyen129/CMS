using LCMS.Application.Fx;
using LCMS.Domain.Entities;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Settlements;

/// <summary>
/// FX snapshot for Payment/Collection to the tenant reporting currency (ADR-0040) and the
/// reporting-currency trace of allocations (cash side vs AP/AR side ⇒ FX difference, FX-ARCH-11).
/// </summary>
public interface ISettlementFxStub
{
    /// <summary>Tenant reporting currency.</summary>
    string BaseCurrency { get; }

    decimal ToBaseAmount(string currencyCode, decimal amount);

    Task ApplyToPaymentAsync(
        Payment payment,
        decimal amountInTxnCurrency,
        FxManualInput? manual = null,
        CancellationToken cancellationToken = default);

    Task ApplyToCollectionAsync(
        Collection collection,
        decimal amountInTxnCurrency,
        FxManualInput? manual = null,
        CancellationToken cancellationToken = default);

    void ApplyToPaymentAllocation(PaymentAllocation allocation, Payment payment, AccountsPayable payable);

    void ApplyToCollectionAllocation(CollectionAllocation allocation, Collection collection, AccountsReceivable receivable);
}

public sealed class SettlementFxStub : ISettlementFxStub
{
    private readonly SettlementOptions _options;
    private readonly IFxSnapshotService _fx;
    private readonly IReportingCurrencyProvider _reporting;

    public SettlementFxStub(
        IOptions<SettlementOptions> options,
        IFxSnapshotService fx,
        IReportingCurrencyProvider reporting)
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
        SettlementOptions.SectionName);

    public decimal ToBaseAmount(string currencyCode, decimal amount) =>
        FxStubMath.ConvertWithStubOnly(currencyCode, amount, BaseCurrency, Stub);

    public async Task ApplyToPaymentAsync(
        Payment payment,
        decimal amountInTxnCurrency,
        FxManualInput? manual = null,
        CancellationToken cancellationToken = default)
    {
        var snap = await ResolveAsync(payment, payment.ValueDate, manual, "payment", payment.Id, cancellationToken);
        payment.BaseAmount = FxMath.ToReporting(Round(amountInTxnCurrency), snap.Rate);
    }

    public async Task ApplyToCollectionAsync(
        Collection collection,
        decimal amountInTxnCurrency,
        FxManualInput? manual = null,
        CancellationToken cancellationToken = default)
    {
        var snap = await ResolveAsync(collection, collection.ValueDate, manual, "collection", collection.Id, cancellationToken);
        collection.BaseAmount = FxMath.ToReporting(Round(amountInTxnCurrency), snap.Rate);
    }

    public void ApplyToPaymentAllocation(PaymentAllocation allocation, Payment payment, AccountsPayable payable)
    {
        var (cash, settled, diff) = Trace(
            allocation.Amount,
            SettlementCurrency.TargetAmount(allocation),
            payment,
            payable);
        allocation.ReportingCurrencyCode = BaseCurrency;
        allocation.BaseAmount = cash;
        allocation.SettledReportingAmount = settled;
        allocation.FxDifferenceAmount = diff;
    }

    public void ApplyToCollectionAllocation(
        CollectionAllocation allocation,
        Collection collection,
        AccountsReceivable receivable)
    {
        var (cash, settled, diff) = Trace(
            allocation.Amount,
            SettlementCurrency.TargetAmount(allocation),
            collection,
            receivable);
        allocation.ReportingCurrencyCode = BaseCurrency;
        allocation.BaseAmount = cash;
        allocation.SettledReportingAmount = settled;
        allocation.FxDifferenceAmount = diff;
    }

    private async Task<FxSnapshotResult> ResolveAsync(
        IReportingFx record,
        DateOnly asOf,
        FxManualInput? manual,
        string objectType,
        Guid objectId,
        CancellationToken cancellationToken)
    {
        var snap = manual is { HasRate: true } ? null : _fx.FromRecord(record);
        if (snap is not null)
        {
            return snap;
        }

        snap = await _fx.ResolveAsync(
            new FxResolveRequest(record.CurrencyCode, asOf, manual?.Rate, manual?.Reason, Stub),
            cancellationToken);
        _fx.Apply(record, snap, objectType, objectId);
        return snap;
    }

    private (decimal? Cash, decimal? Settled, decimal? Difference) Trace(
        decimal cashAmount,
        decimal settledAmount,
        IReportingFx cashRecord,
        IReportingFx openItem)
    {
        var cashRate = _fx.FromRecord(cashRecord)?.Rate;
        var itemRate = _fx.FromRecord(openItem)?.Rate;
        decimal? cash = cashRate is { } cr ? FxMath.ToReporting(cashAmount, cr) : null;
        decimal? settled = itemRate is { } ir ? FxMath.ToReporting(settledAmount, ir) : null;
        decimal? diff = cash is { } c && settled is { } s ? c - s : null;
        return (cash, settled, diff);
    }

    private static decimal Round(decimal amount) =>
        decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
}
