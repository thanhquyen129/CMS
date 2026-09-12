using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Application.Revenues;
using LCMS.Domain.Entities;
using LCMS.Domain.Terminology;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Dashboard.Queries;

/// <summary>
/// Tenant-scoped dashboard summary (E13) — derived counts + Best Available totals. Not SoT.
/// Sprint 11 FULL: open variances + overdue exceptions; optional base-currency FX stub roll-up.
/// </summary>
public sealed record GetDashboardSummaryQuery(bool IncludeBaseCurrencyRollUp = true)
    : IRequest<DashboardSummaryDto>;

public sealed record DashboardCurrencyTotalsDto(
    string CurrencyCode,
    decimal CostBestAvailable,
    decimal RevenueBestAvailable,
    decimal ProfitBestAvailable);

public sealed record DashboardBaseCurrencyRollUpDto(
    string BaseCurrency,
    decimal CostBestAvailableBase,
    decimal RevenueBestAvailableBase,
    decimal ProfitBestAvailableBase,
    string FxStubNote);

/// <summary>Document intake pipeline — Received ≠ Accepted ≠ Matched.</summary>
public sealed record DashboardDocumentClusterDto(
    int AwaitingAcceptanceCount,
    int AcceptedUnmatchedCount,
    int DraftMatchCount);

/// <summary>AP/AR open settlement + exposures not fully recognized.</summary>
public sealed record DashboardApArClusterDto(
    int OpenAccountsPayableCount,
    int OpenAccountsReceivableCount,
    int OpenPayableExposureCount,
    int OpenReceivableExposureCount);

/// <summary>Cash settlement transactions still open (not cancelled).</summary>
public sealed record DashboardSettlementClusterDto(
    int OpenPaymentCount,
    int OpenCollectionCount);

/// <summary>
/// Line counts by maturity layer (Actual → Confirmed → Expected). Counts only — not money totals.
/// </summary>
public sealed record DashboardMaturityPipelineDto(
    int CostExpectedOnlyCount,
    int CostConfirmedOnlyCount,
    int CostActualCount,
    int RevenueExpectedOnlyCount,
    int RevenueConfirmedOnlyCount,
    int RevenueActualCount);

public sealed record DashboardSummaryDto(
    DateTimeOffset AsOfTimestamp,
    int BillCount,
    int OpenExceptionCount,
    int PendingApprovalCount,
    int OpenCloseCount,
    int OpenVarianceCount,
    int OverdueExceptionCount,
    IReadOnlyList<DashboardCurrencyTotalsDto> TotalsByCurrency,
    DashboardBaseCurrencyRollUpDto? BaseCurrencyRollUp,
    bool HasMixedCurrencies,
    string Note,
    int OpenReconciliationCount = 0,
    int UnmatchedBankFeedCount = 0,
    DashboardDocumentClusterDto? Documents = null,
    DashboardApArClusterDto? ApAr = null,
    DashboardSettlementClusterDto? Settlements = null,
    DashboardMaturityPipelineDto? MaturityPipeline = null);

public sealed class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICostFxStub _costFx;
    private readonly IRevenueFxStub _revenueFx;

    public GetDashboardSummaryQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICostFxStub costFx,
        IRevenueFxStub revenueFx)
    {
        _db = db;
        _tenantContext = tenantContext;
        _costFx = costFx;
        _revenueFx = revenueFx;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var asOf = DateTimeOffset.UtcNow;
        var asOfDate = DateOnly.FromDateTime(asOf.UtcDateTime);

        var billCount = await _db.Bills.AsNoTracking().CountAsync(cancellationToken);

        var openExceptions = await _db.Exceptions.AsNoTracking()
            .Where(e =>
                e.Status == ExceptionStatuses.Open
                || e.Status == ExceptionStatuses.InProgress
                || e.Status == ExceptionStatuses.Escalated)
            .Select(e => new { e.DueAt })
            .ToListAsync(cancellationToken);

        var openExceptionCount = openExceptions.Count;
        var overdueExceptionCount = openExceptions.Count(e => e.DueAt != null && e.DueAt < asOf);

        var pendingApprovalCount = await _db.Approvals.AsNoTracking()
            .CountAsync(a => a.Status == ApprovalStatuses.Pending, cancellationToken);

        var openVarianceCount = await _db.Variances.AsNoTracking()
            .CountAsync(v => v.Status == VarianceStatuses.Open, cancellationToken);

        // Open closes = not yet locked (open or reopened for work).
        var openCloseCount = await _db.FinancialCloses.AsNoTracking()
            .CountAsync(
                c => c.Status == FinancialCloseStatuses.Open || c.Status == FinancialCloseStatuses.Reopened,
                cancellationToken);

        var openReconciliationCount = await _db.Reconciliations.AsNoTracking()
            .CountAsync(
                r => r.Status == ReconciliationStatuses.Draft
                    || r.Status == ReconciliationStatuses.InProgress,
                cancellationToken);

        var unmatchedBankFeedCount = await _db.BankFeedLines.AsNoTracking()
            .CountAsync(l => l.Status == BankFeedLineStatuses.Unmatched, cancellationToken);

        var activeDocs = _db.FinancialDocuments.AsNoTracking()
            .Where(d => d.RecordStatus == FinancialDocumentRecordStatuses.Active);

        var awaitingAcceptanceCount = await activeDocs.CountAsync(
            d => d.ReceiptStatus == FinancialDocumentReceiptStatuses.Received
                && d.AcceptanceStatus == FinancialDocumentAcceptanceStatuses.NotAccepted,
            cancellationToken);

        var acceptedUnmatchedCount = await activeDocs.CountAsync(
            d => d.AcceptanceStatus == FinancialDocumentAcceptanceStatuses.Accepted
                && (d.MatchingStatus == FinancialDocumentMatchingStatuses.Unmatched
                    || d.MatchingStatus == FinancialDocumentMatchingStatuses.PartiallyMatched),
            cancellationToken);

        var draftMatchCount = await _db.DocumentMatches.AsNoTracking()
            .CountAsync(m => m.MatchStatus == DocumentMatchStatuses.Draft, cancellationToken);

        var openApCount = await _db.AccountsPayable.AsNoTracking()
            .CountAsync(
                a => a.RecordStatus == "active"
                    && a.SettlementStatus != ApArSettlementStatuses.Settled,
                cancellationToken);

        var openArCount = await _db.AccountsReceivable.AsNoTracking()
            .CountAsync(
                a => a.RecordStatus == "active"
                    && a.SettlementStatus != ApArSettlementStatuses.Settled,
                cancellationToken);

        var openPayableExposureCount = await _db.PayableExposures.AsNoTracking()
            .CountAsync(
                e => e.RecordStatus == "active"
                    && e.Status != ExposureStatuses.Cancelled
                    && e.Status != ExposureStatuses.FullyRecognized,
                cancellationToken);

        var openReceivableExposureCount = await _db.ReceivableExposures.AsNoTracking()
            .CountAsync(
                e => e.RecordStatus == "active"
                    && e.Status != ExposureStatuses.Cancelled
                    && e.Status != ExposureStatuses.FullyRecognized,
                cancellationToken);

        var openPaymentCount = await _db.Payments.AsNoTracking()
            .CountAsync(
                p => p.RecordStatus == "active" && p.Status == PaymentStatuses.Open,
                cancellationToken);

        var openCollectionCount = await _db.Collections.AsNoTracking()
            .CountAsync(
                c => c.RecordStatus == "active" && c.Status == CollectionStatuses.Open,
                cancellationToken);

        // Economic cost = all active costs (direct + shared). Do not add allocation details (would double-count).
        var costs = await _db.Costs.AsNoTracking()
            .Where(c => c.RecordStatus == "active")
            .Select(c => new { c.CurrencyCode, c.ExpectedAmount, c.ConfirmedAmount, c.ActualAmount })
            .ToListAsync(cancellationToken);

        var revenues = await _db.Revenues.AsNoTracking()
            .Where(r => r.RecordStatus == "active")
            .Select(r => new { r.CurrencyCode, r.ExpectedAmount, r.ConfirmedAmount, r.ActualAmount })
            .ToListAsync(cancellationToken);

        var maturity = new DashboardMaturityPipelineDto(
            costs.Count(c => !c.ConfirmedAmount.HasValue && !c.ActualAmount.HasValue),
            costs.Count(c => c.ConfirmedAmount.HasValue && !c.ActualAmount.HasValue),
            costs.Count(c => c.ActualAmount.HasValue),
            revenues.Count(r => !r.ConfirmedAmount.HasValue && !r.ActualAmount.HasValue),
            revenues.Count(r => r.ConfirmedAmount.HasValue && !r.ActualAmount.HasValue),
            revenues.Count(r => r.ActualAmount.HasValue));

        var documents = new DashboardDocumentClusterDto(
            awaitingAcceptanceCount,
            acceptedUnmatchedCount,
            draftMatchCount);

        var apAr = new DashboardApArClusterDto(
            openApCount,
            openArCount,
            openPayableExposureCount,
            openReceivableExposureCount);

        var settlements = new DashboardSettlementClusterDto(openPaymentCount, openCollectionCount);

        var currencyCodes = costs.Select(c => c.CurrencyCode)
            .Concat(revenues.Select(r => r.CurrencyCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totals = new List<DashboardCurrencyTotalsDto>();
        decimal costBase = 0m;
        decimal revenueBase = 0m;
        foreach (var code in currencyCodes)
        {
            var costTotal = costs
                .Where(c => string.Equals(c.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .Sum(c => BestAvailable(c.ActualAmount, c.ConfirmedAmount, c.ExpectedAmount));
            var revenueTotal = revenues
                .Where(r => string.Equals(r.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .Sum(r => BestAvailable(r.ActualAmount, r.ConfirmedAmount, r.ExpectedAmount));
            var profit = decimal.Round(revenueTotal - costTotal, 4, MidpointRounding.AwayFromZero);

            totals.Add(new DashboardCurrencyTotalsDto(
                code.ToUpperInvariant(),
                decimal.Round(costTotal, 4, MidpointRounding.AwayFromZero),
                decimal.Round(revenueTotal, 4, MidpointRounding.AwayFromZero),
                profit));

            if (request.IncludeBaseCurrencyRollUp)
            {
                costBase += await _costFx.ToBaseAmountAsync(code, costTotal, asOfDate, cancellationToken);
                revenueBase += await _revenueFx.ToBaseAmountAsync(code, revenueTotal, asOfDate, cancellationToken);
            }
        }

        DashboardBaseCurrencyRollUpDto? rollUp = null;
        if (request.IncludeBaseCurrencyRollUp)
        {
            var baseCurrency = _costFx.BaseCurrency;
            rollUp = new DashboardBaseCurrencyRollUpDto(
                baseCurrency,
                decimal.Round(costBase, 4, MidpointRounding.AwayFromZero),
                decimal.Round(revenueBase, 4, MidpointRounding.AwayFromZero),
                decimal.Round(revenueBase - costBase, 4, MidpointRounding.AwayFromZero),
                $"{VietnameseUiTerms.Get("FX_STUB_RATE")}: roll-up theo fx_rates (ngày asOf) hoặc StubFxRatesToBase fallback (ADR-0004/0011).");
        }

        var mixed = totals.Count > 1;
        var note = mixed
            ? $"{VietnameseUiTerms.Get("DASHBOARD")}: Totals theo currency_code; roll-up base là stub FX. {VietnameseUiTerms.Get("BEST_AVAILABLE")} là projection, không SoT."
            : $"{VietnameseUiTerms.Get("DASHBOARD")}: {VietnameseUiTerms.Get("BEST_AVAILABLE")} = Actual → Confirmed → Expected. Projection read-only.";

        return new DashboardSummaryDto(
            asOf,
            billCount,
            openExceptionCount,
            pendingApprovalCount,
            openCloseCount,
            openVarianceCount,
            overdueExceptionCount,
            totals,
            rollUp,
            mixed,
            note,
            openReconciliationCount,
            unmatchedBankFeedCount,
            documents,
            apAr,
            settlements,
            maturity);
    }

    private static decimal BestAvailable(decimal? actual, decimal? confirmed, decimal expected)
    {
        if (actual.HasValue)
        {
            return actual.Value;
        }

        if (confirmed.HasValue)
        {
            return confirmed.Value;
        }

        return expected;
    }
}
