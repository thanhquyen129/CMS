using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.FinancialCloses;

/// <summary>
/// Late documents into a locked period: immaterial may adjust the open books; material requires reopen.
/// Locked snapshots are never rewritten.
/// </summary>
public interface ILateDocumentGate
{
    Task EnsureReceiveAllowedAsync(
        Guid? billId,
        DateOnly? documentDate,
        decimal totalAmount,
        CancellationToken cancellationToken);
}

public sealed class LateDocumentGate : ILateDocumentGate
{
    private readonly ILcmsDbContext _db;
    private readonly FinancialCloseOptions _options;

    public LateDocumentGate(ILcmsDbContext db, IOptions<FinancialCloseOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task EnsureReceiveAllowedAsync(
        Guid? billId,
        DateOnly? documentDate,
        decimal totalAmount,
        CancellationToken cancellationToken)
    {
        var locked = await _db.FinancialCloses.AsNoTracking()
            .Where(c => c.Status == FinancialCloseStatuses.Locked)
            .ToListAsync(cancellationToken);
        if (locked.Count == 0)
        {
            return;
        }

        var threshold = _options.Eligibility?.LateDocumentMaterialThreshold ?? 0m;
        var amount = Math.Abs(totalAmount);
        foreach (var close in locked)
        {
            if (!IsInLockedWindow(close, billId, documentDate))
            {
                continue;
            }

            if (amount > threshold)
            {
                throw new ConflictAppException(
                    "Chứng từ trễ trọng yếu: mở lại chốt trước khi nhận. Bản chốt cũ không bị viết lại.");
            }
        }
    }

    private static bool IsInLockedWindow(FinancialClose close, Guid? billId, DateOnly? documentDate)
    {
        if (string.Equals(close.ScopeType, FinancialCloseScopeTypes.Tenant, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(close.ScopeType, FinancialCloseScopeTypes.Bill, StringComparison.OrdinalIgnoreCase))
        {
            return close.ScopeId.HasValue && billId.HasValue && close.ScopeId.Value == billId.Value;
        }

        if (string.Equals(close.ScopeType, FinancialCloseScopeTypes.Period, StringComparison.OrdinalIgnoreCase))
        {
            if (!close.PeriodFrom.HasValue && !close.PeriodTo.HasValue)
            {
                return true;
            }

            if (!documentDate.HasValue)
            {
                return true;
            }

            if (close.PeriodFrom.HasValue && documentDate.Value < close.PeriodFrom.Value)
            {
                return false;
            }

            if (close.PeriodTo.HasValue && documentDate.Value > close.PeriodTo.Value)
            {
                return false;
            }

            return true;
        }

        return false;
    }
}
