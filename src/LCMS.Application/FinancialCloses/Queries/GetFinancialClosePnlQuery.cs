using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Terminology;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialCloses.Queries;

/// <summary>
/// Derived P&amp;L roll-up from immutable close snapshot metrics — read only (never mutates snapshot / Bill).
/// </summary>
public sealed record GetFinancialClosePnlQuery(Guid FinancialCloseId, Guid? SnapshotId = null)
    : IRequest<FinancialClosePnlDto>;

public sealed record FinancialClosePnlMetricDto(
    string MetricKey,
    decimal MetricValue,
    string? CurrencyCode,
    string? SourceType,
    string? Notes);

public sealed record FinancialClosePnlDto(
    Guid FinancialCloseId,
    Guid SnapshotId,
    int SnapshotVersion,
    string BaseCurrency,
    DateTimeOffset ClosedAt,
    string ImmutableHash,
    decimal RevenueTotal,
    decimal CostTotal,
    decimal ProfitTotal,
    decimal ApOutstandingTotal,
    decimal ArOutstandingTotal,
    IReadOnlyList<FinancialClosePnlMetricDto> Metrics,
    string Note);

public sealed class GetFinancialClosePnlQueryHandler
    : IRequestHandler<GetFinancialClosePnlQuery, FinancialClosePnlDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetFinancialClosePnlQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<FinancialClosePnlDto> Handle(
        GetFinancialClosePnlQuery request,
        CancellationToken cancellationToken)
    {
        ListFinancialClosesQueryHandler.EnsureTenant(_tenantContext);

        var close = await _db.FinancialCloses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.FinancialCloseId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lần chốt tài chính.");

        Domain.Entities.FinancialCloseSnapshot? snapshot;
        if (request.SnapshotId.HasValue)
        {
            snapshot = await _db.FinancialCloseSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(
                    s => s.Id == request.SnapshotId.Value && s.FinancialCloseId == close.Id,
                    cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy bản chốt tài chính.");
        }
        else
        {
            snapshot = await _db.FinancialCloseSnapshots.AsNoTracking()
                .Where(s => s.FinancialCloseId == close.Id)
                .OrderByDescending(s => s.SnapshotVersion)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundAppException(
                    "Chưa có bản chốt tài chính — tạo snapshot trước khi xem P&L.");
        }

        var details = await _db.FinancialCloseSnapshotDetails.AsNoTracking()
            .Where(d => d.SnapshotId == snapshot.Id)
            .OrderBy(d => d.LineNo)
            .ToListAsync(cancellationToken);

        decimal Metric(string key) =>
            details.FirstOrDefault(d => string.Equals(d.MetricKey, key, StringComparison.OrdinalIgnoreCase))
                ?.MetricValue ?? 0m;

        var revenue = Metric("revenue_total");
        var cost = Metric("cost_total");
        var profit = decimal.Round(revenue - cost, 4, MidpointRounding.AwayFromZero);
        var ap = Metric("ap_outstanding_total");
        var ar = Metric("ar_outstanding_total");

        var note =
            $"{VietnameseUiTerms.Get("FINANCIAL_CLOSE_SNAPSHOT")}: P&L stub derive từ metric bất biến " +
            $"(revenue_total − cost_total). Không ghi SoT lên Bill. " +
            $"{VietnameseUiTerms.Get("REPORTING_PROJECTION")}.";

        return new FinancialClosePnlDto(
            close.Id,
            snapshot.Id,
            snapshot.SnapshotVersion,
            snapshot.BaseCurrency,
            snapshot.ClosedAt,
            snapshot.ImmutableHash,
            decimal.Round(revenue, 4, MidpointRounding.AwayFromZero),
            decimal.Round(cost, 4, MidpointRounding.AwayFromZero),
            profit,
            decimal.Round(ap, 4, MidpointRounding.AwayFromZero),
            decimal.Round(ar, 4, MidpointRounding.AwayFromZero),
            details.Select(d => new FinancialClosePnlMetricDto(
                d.MetricKey,
                d.MetricValue,
                d.CurrencyCode,
                d.SourceType,
                d.Notes)).ToList(),
            note);
    }
}
