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
    string Note);

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

        // Economic cost = all active costs (direct + shared). Do not add allocation details (would double-count).
        var costs = await _db.Costs.AsNoTracking()
            .Where(c => c.RecordStatus == "active")
            .Select(c => new { c.CurrencyCode, c.ExpectedAmount, c.ConfirmedAmount, c.ActualAmount })
            .ToListAsync(cancellationToken);

        var revenues = await _db.Revenues.AsNoTracking()
            .Where(r => r.RecordStatus == "active")
            .Select(r => new { r.CurrencyCode, r.ExpectedAmount, r.ConfirmedAmount, r.ActualAmount })
            .ToListAsync(cancellationToken);

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
                costBase += _costFx.ToBaseAmount(code, costTotal);
                revenueBase += _revenueFx.ToBaseAmount(code, revenueTotal);
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
                $"{VietnameseUiTerms.Get("FX_STUB_RATE")}: roll-up stub theo Cost/Revenue StubFxRatesToBase (ADR-0011). Không phải tỷ giá thị trường.");
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
            note);
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
