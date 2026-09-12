using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Terminology;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Dashboard.Queries;

/// <summary>
/// Tenant-scoped dashboard summary (E13) — derived counts + Best Available totals. Not SoT.
/// </summary>
public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

public sealed record DashboardCurrencyTotalsDto(
    string CurrencyCode,
    decimal CostBestAvailable,
    decimal RevenueBestAvailable,
    decimal ProfitBestAvailable);

public sealed record DashboardSummaryDto(
    DateTimeOffset AsOfTimestamp,
    int BillCount,
    int OpenExceptionCount,
    int PendingApprovalCount,
    int OpenCloseCount,
    IReadOnlyList<DashboardCurrencyTotalsDto> TotalsByCurrency,
    bool HasMixedCurrencies,
    string Note);

public sealed class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetDashboardSummaryQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
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

        var openExceptionCount = await _db.Exceptions.AsNoTracking()
            .CountAsync(
                e => e.Status == ExceptionStatuses.Open
                     || e.Status == ExceptionStatuses.InProgress
                     || e.Status == ExceptionStatuses.Escalated,
                cancellationToken);

        var pendingApprovalCount = await _db.Approvals.AsNoTracking()
            .CountAsync(a => a.Status == ApprovalStatuses.Pending, cancellationToken);

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
        }

        var mixed = totals.Count > 1;
        var note = mixed
            ? $"{VietnameseUiTerms.Get("DASHBOARD")}: Totals theo currency_code; không cộng gộp FX. {VietnameseUiTerms.Get("BEST_AVAILABLE")} là projection, không SoT."
            : $"{VietnameseUiTerms.Get("DASHBOARD")}: {VietnameseUiTerms.Get("BEST_AVAILABLE")} = Actual → Confirmed → Expected. Projection read-only.";

        return new DashboardSummaryDto(
            asOf,
            billCount,
            openExceptionCount,
            pendingApprovalCount,
            openCloseCount,
            totals,
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
