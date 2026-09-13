using LCMS.Application.Common.Exceptions;
using LCMS.Application.Tenancy;
using LCMS.Domain.Entities;

namespace LCMS.Application.Costs;

/// <summary>
/// Optional approval threshold before Expected→Confirmed.
/// When threshold configured and BaseAmount exceeds it, ApprovalStatus must be approved.
/// Tenant override via TenantFinancialOptionsResolver (P20).
/// </summary>
public interface ICostApprovalGate
{
    /// <summary>Sets ApprovalStatus pending when over threshold on create/adjust; else not_required if still default.</summary>
    Task RefreshPendingFlagAsync(Cost cost, CancellationToken cancellationToken = default);

    /// <summary>Throws Conflict with VI message when confirm blocked by threshold.</summary>
    Task EnsureConfirmAllowedAsync(Cost cost, CancellationToken cancellationToken = default);
}

public sealed class CostApprovalGate : ICostApprovalGate
{
    private readonly TenantFinancialOptionsResolver _financial;
    private readonly ICostFxStub _fx;

    public CostApprovalGate(TenantFinancialOptionsResolver financial, ICostFxStub fx)
    {
        _financial = financial;
        _fx = fx;
    }

    public async Task RefreshPendingFlagAsync(Cost cost, CancellationToken cancellationToken = default)
    {
        var threshold = await _financial.GetConfirmApprovalThresholdBaseAsync(cancellationToken);
        if (threshold is not decimal t)
        {
            return;
        }

        var baseAmount = cost.BaseAmount ?? _fx.ToBaseAmount(cost.CurrencyCode, cost.Amount);
        if (baseAmount > t)
        {
            if (string.Equals(cost.ApprovalStatus, "not_required", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cost.ApprovalStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                cost.ApprovalStatus = "pending";
            }
        }
        else if (string.Equals(cost.ApprovalStatus, "pending", StringComparison.OrdinalIgnoreCase))
        {
            cost.ApprovalStatus = "not_required";
        }
    }

    public async Task EnsureConfirmAllowedAsync(Cost cost, CancellationToken cancellationToken = default)
    {
        var threshold = await _financial.GetConfirmApprovalThresholdBaseAsync(cancellationToken);
        if (threshold is not decimal t)
        {
            return;
        }

        var baseAmount = cost.BaseAmount ?? _fx.ToBaseAmount(cost.CurrencyCode, cost.Amount);
        if (baseAmount <= t)
        {
            return;
        }

        if (string.Equals(cost.ApprovalStatus, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(cost.ApprovalStatus, "not_required", StringComparison.OrdinalIgnoreCase))
        {
            cost.ApprovalStatus = "pending";
        }

        throw new ConflictAppException(
            "Chi phí vượt ngưỡng phê duyệt; cần phê duyệt trước khi xác nhận.");
    }
}
