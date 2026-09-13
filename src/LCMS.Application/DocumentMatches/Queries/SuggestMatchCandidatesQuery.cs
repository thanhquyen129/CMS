using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.FinancialDocuments;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.DocumentMatches.Queries;

/// <summary>
/// Read-only candidate suggestions within session tolerance (C-007). Operator must review and add manually.
/// </summary>
public sealed record MatchSuggestionDto(
    Guid SourceLineId,
    int SourceLineNo,
    decimal SourceOpenAmount,
    string CurrencyCode,
    string TargetKind,
    Guid TargetId,
    string TargetLabel,
    decimal TargetAmount,
    decimal SuggestedMatchedAmount,
    decimal AmountDelta,
    decimal EffectiveTolerance);

public sealed record SuggestMatchCandidatesQuery(Guid MatchId)
    : IRequest<IReadOnlyList<MatchSuggestionDto>>;

public sealed class SuggestMatchCandidatesQueryHandler
    : IRequestHandler<SuggestMatchCandidatesQuery, IReadOnlyList<MatchSuggestionDto>>
{
    private const int MaxSuggestions = 50;

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public SuggestMatchCandidatesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<MatchSuggestionDto>> Handle(
        SuggestMatchCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var match = await _db.DocumentMatches.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MatchId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên khớp chứng từ.");

        if (string.Equals(match.MatchStatus, DocumentMatchStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Không đề xuất khớp cho phiên đã hủy.");
        }

        if (!match.PrimaryDocumentId.HasValue)
        {
            return Array.Empty<MatchSuggestionDto>();
        }

        var primaryDoc = await _db.FinancialDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == match.PrimaryDocumentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chứng từ neo phiên khớp.");

        var sourceLines = await _db.FinancialDocumentLines.AsNoTracking()
            .Where(l => l.DocumentId == primaryDoc.Id)
            .OrderBy(l => l.LineNo)
            .ToListAsync(cancellationToken);

        var sourceLineIds = sourceLines.Select(l => l.Id).ToHashSet();

        var activeDetails = await _db.DocumentMatchDetails.AsNoTracking()
            .Where(d =>
                d.DetailStatus == DocumentMatchDetailStatuses.Active
                && (d.MatchId == match.Id
                    || sourceLineIds.Contains(d.SourceLineId)
                    || (d.TargetLineId.HasValue && sourceLineIds.Contains(d.TargetLineId.Value))))
            .Select(d => new { d.MatchId, d.SourceLineId, d.TargetLineId, d.TargetCostId, d.TargetRevenueId, d.MatchedAmount })
            .ToListAsync(cancellationToken);

        // For line_to_line target opens we may need extra target-line usage; load on demand below via HashSet expand.
        var extraTargetUsage = new Dictionary<Guid, decimal>();

        async Task<decimal> OpenOfAsync(Guid lineId, decimal lineAmount)
        {
            if (sourceLineIds.Contains(lineId))
            {
                var used = activeDetails
                    .Where(d => d.SourceLineId == lineId || d.TargetLineId == lineId)
                    .Sum(d => d.MatchedAmount);
                return lineAmount - used;
            }

            if (!extraTargetUsage.TryGetValue(lineId, out var remoteUsed))
            {
                remoteUsed = await _db.DocumentMatchDetails.AsNoTracking()
                    .Where(d =>
                        d.DetailStatus == DocumentMatchDetailStatuses.Active
                        && (d.SourceLineId == lineId || d.TargetLineId == lineId))
                    .SumAsync(d => (decimal?)d.MatchedAmount, cancellationToken) ?? 0m;
                extraTargetUsage[lineId] = remoteUsed;
            }

            return lineAmount - remoteUsed;
        }

        var usedCostIds = activeDetails
            .Where(d => d.MatchId == match.Id && d.TargetCostId.HasValue)
            .Select(d => d.TargetCostId!.Value)
            .ToHashSet();
        var usedRevenueIds = activeDetails
            .Where(d => d.MatchId == match.Id && d.TargetRevenueId.HasValue)
            .Select(d => d.TargetRevenueId!.Value)
            .ToHashSet();
        var usedLineIds = activeDetails
            .Where(d => d.MatchId == match.Id && d.TargetLineId.HasValue)
            .Select(d => d.TargetLineId!.Value)
            .ToHashSet();

        var suggestions = new List<MatchSuggestionDto>();

        foreach (var line in sourceLines)
        {
            var open = await OpenOfAsync(line.Id, line.Amount);
            if (open <= 0m)
            {
                continue;
            }

            var tol = DocumentMatchTolerance.EffectiveTolerance(
                line.Amount,
                match.ToleranceAmount,
                match.TolerancePercent);

            if (string.Equals(match.MatchMethod, DocumentMatchMethods.LineToCost, StringComparison.OrdinalIgnoreCase))
            {
                if (!primaryDoc.BillId.HasValue)
                {
                    continue;
                }

                var costs = await _db.Costs.AsNoTracking()
                    .Where(c =>
                        c.BillId == primaryDoc.BillId
                        && c.CurrencyCode == line.CurrencyCode
                        && c.RecordStatus == "active"
                        && !usedCostIds.Contains(c.Id))
                    .OrderBy(c => c.Id)
                    .Take(100)
                    .ToListAsync(cancellationToken);

                foreach (var cost in costs)
                {
                    AddIfWithinTolerance(
                        suggestions, line, open, tol, "cost", cost.Id,
                        $"{cost.CostTypeCode ?? "CP"} · {cost.Amount:0.####} {cost.CurrencyCode}",
                        cost.Amount);
                    if (suggestions.Count >= MaxSuggestions)
                    {
                        return suggestions;
                    }
                }
            }
            else if (string.Equals(match.MatchMethod, DocumentMatchMethods.LineToRevenue, StringComparison.OrdinalIgnoreCase))
            {
                if (!primaryDoc.BillId.HasValue)
                {
                    continue;
                }

                var revenues = await _db.Revenues.AsNoTracking()
                    .Where(r =>
                        r.BillId == primaryDoc.BillId
                        && r.CurrencyCode == line.CurrencyCode
                        && r.RecordStatus == "active"
                        && !usedRevenueIds.Contains(r.Id))
                    .OrderBy(r => r.Id)
                    .Take(100)
                    .ToListAsync(cancellationToken);

                foreach (var rev in revenues)
                {
                    AddIfWithinTolerance(
                        suggestions, line, open, tol, "revenue", rev.Id,
                        $"{rev.RevenueTypeCode ?? "DT"} · {rev.Amount:0.####} {rev.CurrencyCode}",
                        rev.Amount);
                    if (suggestions.Count >= MaxSuggestions)
                    {
                        return suggestions;
                    }
                }
            }
            else if (string.Equals(match.MatchMethod, DocumentMatchMethods.LineToLine, StringComparison.OrdinalIgnoreCase))
            {
                IQueryable<FinancialDocumentLine> targetQuery =
                    from l in _db.FinancialDocumentLines.AsNoTracking()
                    join d in _db.FinancialDocuments.AsNoTracking() on l.DocumentId equals d.Id
                    where l.DocumentId != primaryDoc.Id
                          && l.CurrencyCode == line.CurrencyCode
                          && d.RecordStatus == FinancialDocumentRecordStatuses.Active
                          && d.ReceiptStatus == FinancialDocumentReceiptStatuses.Received
                          && d.AcceptanceStatus == FinancialDocumentAcceptanceStatuses.Accepted
                          && !usedLineIds.Contains(l.Id)
                    select l;

                if (primaryDoc.BillId.HasValue)
                {
                    var billId = primaryDoc.BillId.Value;
                    targetQuery =
                        from l in _db.FinancialDocumentLines.AsNoTracking()
                        join d in _db.FinancialDocuments.AsNoTracking() on l.DocumentId equals d.Id
                        where l.DocumentId != primaryDoc.Id
                              && l.CurrencyCode == line.CurrencyCode
                              && d.BillId == billId
                              && d.RecordStatus == FinancialDocumentRecordStatuses.Active
                              && d.ReceiptStatus == FinancialDocumentReceiptStatuses.Received
                              && d.AcceptanceStatus == FinancialDocumentAcceptanceStatuses.Accepted
                              && !usedLineIds.Contains(l.Id)
                        select l;
                }

                var targetLines = await targetQuery
                    .OrderBy(l => l.Id)
                    .Take(100)
                    .ToListAsync(cancellationToken);

                foreach (var target in targetLines)
                {
                    var targetOpen = await OpenOfAsync(target.Id, target.Amount);
                    if (targetOpen <= 0m)
                    {
                        continue;
                    }

                    AddIfWithinTolerance(
                        suggestions, line, open, tol, "line", target.Id,
                        $"#{target.LineNo} · mở {targetOpen:0.####} {target.CurrencyCode}",
                        targetOpen);
                    if (suggestions.Count >= MaxSuggestions)
                    {
                        return suggestions;
                    }
                }
            }
        }

        return suggestions;
    }

    private static void AddIfWithinTolerance(
        List<MatchSuggestionDto> suggestions,
        FinancialDocumentLine source,
        decimal sourceOpen,
        decimal tol,
        string targetKind,
        Guid targetId,
        string label,
        decimal targetAmount)
    {
        var delta = Math.Abs(targetAmount - sourceOpen);
        if (delta > tol)
        {
            return;
        }

        // Within tolerance: post up to min(target, sourceOpen+tol); typically min(target, sourceOpen) when under.
        var suggested = targetAmount <= sourceOpen
            ? targetAmount
            : Math.Min(targetAmount, sourceOpen + tol);
        suggested = decimal.Round(suggested, 4, MidpointRounding.AwayFromZero);
        if (suggested <= 0m)
        {
            return;
        }

        suggestions.Add(new MatchSuggestionDto(
            source.Id,
            source.LineNo,
            sourceOpen,
            source.CurrencyCode,
            targetKind,
            targetId,
            label,
            targetAmount,
            suggested,
            delta,
            tol));
    }
}
