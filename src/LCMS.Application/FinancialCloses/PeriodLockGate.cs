using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.FinancialCloses;

/// <summary>
/// Rejects ledger mutations in a Locked close period/scope when period lock is enforced.
/// Controlled respects FinancialClose:EnforcePeriodLock; Strict locked closes always enforce.
/// </summary>
public interface IPeriodLockGate
{
    /// <summary>
    /// Ensures Cost/Revenue confirm or Payment/Collection allocate/finalize is allowed.
    /// </summary>
    /// <param name="billId">Bill linked to the mutating object (nullable for shared/unscoped).</param>
    /// <param name="activityDate">EffectiveDate / ValueDate used for period-scope matching.</param>
    Task EnsureMutationAllowedAsync(
        Guid? billId,
        DateOnly? activityDate,
        string mutationLabelVi,
        CancellationToken cancellationToken);
}

public sealed class PeriodLockGate : IPeriodLockGate
{
    private readonly ILcmsDbContext _db;
    private readonly FinancialCloseOptions _options;

    public PeriodLockGate(ILcmsDbContext db, IOptions<FinancialCloseOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task EnsureMutationAllowedAsync(
        Guid? billId,
        DateOnly? activityDate,
        string mutationLabelVi,
        CancellationToken cancellationToken)
    {
        var locked = await _db.FinancialCloses.AsNoTracking()
            .Where(c => c.Status == FinancialCloseStatuses.Locked)
            .ToListAsync(cancellationToken);

        foreach (var close in locked)
        {
            if (!IsInScope(close, billId, activityDate))
            {
                continue;
            }

            var strict = string.Equals(
                close.PolicyVersion,
                FinancialClosePolicies.Strict,
                StringComparison.OrdinalIgnoreCase);
            if (!strict && !_options.EnforcePeriodLock)
            {
                continue;
            }

            throw new PeriodLockedAppException(
                $"Kỳ/phạm vi đã khóa chốt tài chính — không được {mutationLabelVi}. Mở lại chốt tại Chốt tài chính nếu cần điều chỉnh.");
        }
    }

    private static bool IsInScope(FinancialClose close, Guid? billId, DateOnly? activityDate)
    {
        if (string.Equals(close.ScopeType, FinancialCloseScopeTypes.Tenant, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(close.ScopeType, FinancialCloseScopeTypes.Bill, StringComparison.OrdinalIgnoreCase))
        {
            return close.ScopeId.HasValue && billId.HasValue && close.ScopeId.Value == billId.Value;
        }

        // Period scope: optional date window; missing dates ⇒ whole-tenant period lock.
        if (string.Equals(close.ScopeType, FinancialCloseScopeTypes.Period, StringComparison.OrdinalIgnoreCase))
        {
            if (!close.PeriodFrom.HasValue && !close.PeriodTo.HasValue)
            {
                return true;
            }

            if (!activityDate.HasValue)
            {
                // Cannot prove outside the window — treat as in-scope for safety on money path.
                return true;
            }

            if (close.PeriodFrom.HasValue && activityDate.Value < close.PeriodFrom.Value)
            {
                return false;
            }

            if (close.PeriodTo.HasValue && activityDate.Value > close.PeriodTo.Value)
            {
                return false;
            }

            return true;
        }

        return false;
    }
}
