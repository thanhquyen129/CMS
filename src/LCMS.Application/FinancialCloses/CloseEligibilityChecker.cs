using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.FinancialCloses;

/// <summary>
/// Close snapshot eligibility checklist. Each failing gate throws a clear Vietnamese Conflict reason.
/// Strict policy forces all gates; Controlled respects config flags (default on).
/// </summary>
public interface ICloseEligibilityChecker
{
    Task EnsureEligibleAsync(FinancialClose close, CancellationToken cancellationToken);
}

public sealed class CloseEligibilityChecker : ICloseEligibilityChecker
{
    private readonly ILcmsDbContext _db;
    private readonly FinancialCloseOptions _options;

    public CloseEligibilityChecker(ILcmsDbContext db, IOptions<FinancialCloseOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task EnsureEligibleAsync(FinancialClose close, CancellationToken cancellationToken)
    {
        var strict = string.Equals(
            close.PolicyVersion,
            FinancialClosePolicies.Strict,
            StringComparison.OrdinalIgnoreCase);
        var eligibility = _options.Eligibility ?? new CloseEligibilityOptions();

        if (strict || eligibility.BlockOnCriticalExceptions)
        {
            await EnsureNoCriticalOpenExceptionsAsync(close, cancellationToken);
        }

        if (strict || eligibility.BlockOnUnmatchedAcceptedDocuments)
        {
            await EnsureNoUnmatchedAcceptedDocumentsAsync(close, cancellationToken);
        }

        if (strict || eligibility.BlockOnUnsettledApArAboveThreshold)
        {
            await EnsureNoUnsettledApArAboveThresholdAsync(close, cancellationToken);
        }
    }

    private async Task EnsureNoCriticalOpenExceptionsAsync(
        FinancialClose close,
        CancellationToken cancellationToken)
    {
        var query = _db.Exceptions.AsNoTracking()
            .Where(e =>
                (e.Status == ExceptionStatuses.Open
                 || e.Status == ExceptionStatuses.InProgress
                 || e.Status == ExceptionStatuses.Escalated)
                && e.Severity == ExceptionSeverities.Critical);

        if (close.ScopeType == FinancialCloseScopeTypes.Bill && close.ScopeId.HasValue)
        {
            var billId = close.ScopeId.Value;
            query = query.Where(e => e.BillId == billId);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new ConflictAppException(
                "Không đủ điều kiện chốt: còn ngoại lệ mức nghiêm trọng (critical) đang mở trong phạm vi.");
        }
    }

    private async Task EnsureNoUnmatchedAcceptedDocumentsAsync(
        FinancialClose close,
        CancellationToken cancellationToken)
    {
        var query = _db.FinancialDocuments.AsNoTracking()
            .Where(d =>
                d.RecordStatus == FinancialDocumentRecordStatuses.Active
                && d.AcceptanceStatus == FinancialDocumentAcceptanceStatuses.Accepted
                && (d.MatchingStatus == FinancialDocumentMatchingStatuses.Unmatched
                    || d.MatchingStatus == FinancialDocumentMatchingStatuses.PartiallyMatched));

        if (close.ScopeType == FinancialCloseScopeTypes.Bill && close.ScopeId.HasValue)
        {
            var billId = close.ScopeId.Value;
            query = query.Where(d => d.BillId == billId);
        }

        if (close.ScopeType == FinancialCloseScopeTypes.Period)
        {
            if (close.PeriodFrom.HasValue)
            {
                var from = close.PeriodFrom.Value;
                query = query.Where(d => d.DocumentDate >= from);
            }

            if (close.PeriodTo.HasValue)
            {
                var to = close.PeriodTo.Value;
                query = query.Where(d => d.DocumentDate <= to);
            }
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new ConflictAppException(
                "Không đủ điều kiện chốt: còn chứng từ đã chấp nhận nhưng chưa khớp đủ trong phạm vi.");
        }
    }

    private async Task EnsureNoUnsettledApArAboveThresholdAsync(
        FinancialClose close,
        CancellationToken cancellationToken)
    {
        var threshold = _options.Eligibility?.UnsettledApArOpenBalanceThreshold ?? 0m;

        var apQuery = _db.AccountsPayable.AsNoTracking()
            .Where(a =>
                a.RecordStatus == "active"
                && a.SettlementStatus != ApArSettlementStatuses.Settled);
        var arQuery = _db.AccountsReceivable.AsNoTracking()
            .Where(a =>
                a.RecordStatus == "active"
                && a.SettlementStatus != ApArSettlementStatuses.Settled);

        if (close.ScopeType == FinancialCloseScopeTypes.Bill && close.ScopeId.HasValue)
        {
            var billId = close.ScopeId.Value;
            apQuery = apQuery.Where(a => a.BillId == billId);
            arQuery = arQuery.Where(a => a.BillId == billId);
        }

        var apList = await apQuery.ToListAsync(cancellationToken);
        var arList = await arQuery.ToListAsync(cancellationToken);

        if (apList.Any(a => a.DeriveOutstanding() > threshold)
            || arList.Any(a => a.DeriveOutstanding() > threshold))
        {
            throw new ConflictAppException(
                $"Không đủ điều kiện chốt: còn khoản phải trả/phải thu chưa tất toán với số dư mở vượt ngưỡng {threshold} trong phạm vi.");
        }
    }
}
