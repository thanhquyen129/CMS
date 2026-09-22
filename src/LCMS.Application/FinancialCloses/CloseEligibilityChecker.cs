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

        if (strict || eligibility.BlockOnOpenAllocations)
        {
            await EnsureNoOpenAllocationsAsync(close, cancellationToken);
        }

        if (strict || eligibility.BlockOnUnallocatedMoneyAboveThreshold)
        {
            await EnsureNoUnallocatedMoneyAboveThresholdAsync(close, cancellationToken);
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
                 || e.Status == ExceptionStatuses.Escalated
                 || e.Status == ExceptionStatuses.Waiting)
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

    private async Task EnsureNoOpenAllocationsAsync(
        FinancialClose close,
        CancellationToken cancellationToken)
    {
        var costQuery = _db.CostAllocations.AsNoTracking()
            .Where(a =>
                a.AllocationStatus == CostAllocationStatuses.Draft
                || a.AllocationStatus == CostAllocationStatuses.Calculated
                || a.AllocationStatus == CostAllocationStatuses.PendingApproval);
        var revenueQuery = _db.RevenueMappings.AsNoTracking()
            .Where(m => m.MappingStatus == CostAllocationStatuses.Draft);

        if (close.ScopeType == FinancialCloseScopeTypes.Bill && close.ScopeId.HasValue)
        {
            var billId = close.ScopeId.Value;
            var costIds = await _db.Costs.AsNoTracking()
                .Where(c => c.BillId == billId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
            costQuery = costQuery.Where(a => costIds.Contains(a.CostId));
            var revenueIds = await _db.Revenues.AsNoTracking()
                .Where(r => r.BillId == billId)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);
            revenueQuery = revenueQuery.Where(m => revenueIds.Contains(m.RevenueId));
        }

        if (await costQuery.AnyAsync(cancellationToken) || await revenueQuery.AnyAsync(cancellationToken))
        {
            throw new ConflictAppException(
                "Không đủ điều kiện chốt: còn phiên phân bổ chi phí hoặc chia doanh thu chưa chốt trong phạm vi.");
        }
    }

    private async Task EnsureNoUnallocatedMoneyAboveThresholdAsync(
        FinancialClose close,
        CancellationToken cancellationToken)
    {
        var threshold = _options.Eligibility?.UnallocatedMoneyThreshold ?? 0m;
        var payments = _db.Payments.AsNoTracking()
            .Where(p => p.Status != PaymentStatuses.Cancelled && p.RecordStatus == "active");
        var collections = _db.Collections.AsNoTracking()
            .Where(c => c.Status != CollectionStatuses.Cancelled && c.RecordStatus == "active");

        if (close.ScopeType == FinancialCloseScopeTypes.Bill && close.ScopeId.HasValue)
        {
            var billId = close.ScopeId.Value;
            payments = payments.Where(p => p.BillId == billId);
            collections = collections.Where(c => c.BillId == billId);
        }

        var paymentList = await payments.ToListAsync(cancellationToken);
        var collectionList = await collections.ToListAsync(cancellationToken);
        var paymentIds = paymentList.Select(p => p.Id).ToList();
        var collectionIds = collectionList.Select(c => c.Id).ToList();

        var payAlloc = paymentIds.Count == 0
            ? []
            : await _db.PaymentAllocations.AsNoTracking()
                .Where(a => paymentIds.Contains(a.PaymentId)
                    && (a.AllocationStatus == SettlementAllocationStatuses.Draft
                        || a.AllocationStatus == SettlementAllocationStatuses.Finalized))
                .Select(a => new { a.PaymentId, a.Amount })
                .ToListAsync(cancellationToken);
        var collAlloc = collectionIds.Count == 0
            ? []
            : await _db.CollectionAllocations.AsNoTracking()
                .Where(a => collectionIds.Contains(a.CollectionId)
                    && (a.AllocationStatus == SettlementAllocationStatuses.Draft
                        || a.AllocationStatus == SettlementAllocationStatuses.Finalized))
                .Select(a => new { a.CollectionId, a.Amount })
                .ToListAsync(cancellationToken);

        var payMap = payAlloc.GroupBy(a => a.PaymentId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var collMap = collAlloc.GroupBy(a => a.CollectionId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var unappliedPay = paymentList.Sum(p => p.Amount - (payMap.TryGetValue(p.Id, out var s) ? s : 0m));
        var unappliedColl = collectionList.Sum(c => c.Amount - (collMap.TryGetValue(c.Id, out var s) ? s : 0m));
        if (unappliedPay > threshold || unappliedColl > threshold)
        {
            throw new ConflictAppException(
                $"Không đủ điều kiện chốt: còn tiền thanh toán/thu chưa gán vượt ngưỡng {threshold} trong phạm vi.");
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
