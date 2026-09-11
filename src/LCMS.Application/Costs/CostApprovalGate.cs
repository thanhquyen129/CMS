using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Costs;

/// <summary>
/// Optional approval threshold before Expected→Confirmed.
/// When threshold configured and BaseAmount exceeds it, ApprovalStatus must be approved.
/// </summary>
public interface ICostApprovalGate
{
    /// <summary>Sets ApprovalStatus pending when over threshold on create/adjust; else not_required if still default.</summary>
    void RefreshPendingFlag(Cost cost);

    /// <summary>Throws Conflict with VI message when confirm blocked by threshold.</summary>
    void EnsureConfirmAllowed(Cost cost);
}

public sealed class CostApprovalGate : ICostApprovalGate
{
    private readonly CostOptions _options;
    private readonly ICostFxStub _fx;

    public CostApprovalGate(IOptions<CostOptions> options, ICostFxStub fx)
    {
        _options = options.Value;
        _fx = fx;
    }

    public void RefreshPendingFlag(Cost cost)
    {
        if (_options.ConfirmApprovalThresholdBase is not decimal threshold)
        {
            return;
        }

        var baseAmount = cost.BaseAmount ?? _fx.ToBaseAmount(cost.CurrencyCode, cost.Amount);
        if (baseAmount > threshold)
        {
            if (string.Equals(cost.ApprovalStatus, "not_required", StringComparison.OrdinalIgnoreCase)
                || string.Equals(cost.ApprovalStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                cost.ApprovalStatus = "pending";
            }
        }
        else if (string.Equals(cost.ApprovalStatus, "pending", StringComparison.OrdinalIgnoreCase))
        {
            // Dropped under threshold without an approval decision — clear gate.
            cost.ApprovalStatus = "not_required";
        }
    }

    public void EnsureConfirmAllowed(Cost cost)
    {
        if (_options.ConfirmApprovalThresholdBase is not decimal threshold)
        {
            return;
        }

        var baseAmount = cost.BaseAmount ?? _fx.ToBaseAmount(cost.CurrencyCode, cost.Amount);
        if (baseAmount <= threshold)
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
