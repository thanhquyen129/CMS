using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Revenues;

/// <summary>
/// Optional approval threshold before Expected→Confirmed (parity Cost FULL / ADR-0004).
/// When threshold configured and BaseAmount exceeds it, ApprovalStatus must be approved.
/// </summary>
public interface IRevenueApprovalGate
{
    /// <summary>Sets ApprovalStatus pending when over threshold on create/adjust; else not_required if still default.</summary>
    void RefreshPendingFlag(Revenue revenue);

    /// <summary>Throws Conflict with VI message when confirm blocked by threshold.</summary>
    void EnsureConfirmAllowed(Revenue revenue);
}

public sealed class RevenueApprovalGate : IRevenueApprovalGate
{
    private readonly RevenueOptions _options;
    private readonly IRevenueFxStub _fx;

    public RevenueApprovalGate(IOptions<RevenueOptions> options, IRevenueFxStub fx)
    {
        _options = options.Value;
        _fx = fx;
    }

    public void RefreshPendingFlag(Revenue revenue)
    {
        if (_options.ConfirmApprovalThresholdBase is not decimal threshold)
        {
            return;
        }

        var baseAmount = revenue.BaseAmount ?? _fx.ToBaseAmount(revenue.CurrencyCode, revenue.Amount);
        if (baseAmount > threshold)
        {
            if (string.Equals(revenue.ApprovalStatus, "not_required", StringComparison.OrdinalIgnoreCase)
                || string.Equals(revenue.ApprovalStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                revenue.ApprovalStatus = "pending";
            }
        }
        else if (string.Equals(revenue.ApprovalStatus, "pending", StringComparison.OrdinalIgnoreCase))
        {
            revenue.ApprovalStatus = "not_required";
        }
    }

    public void EnsureConfirmAllowed(Revenue revenue)
    {
        if (_options.ConfirmApprovalThresholdBase is not decimal threshold)
        {
            return;
        }

        var baseAmount = revenue.BaseAmount ?? _fx.ToBaseAmount(revenue.CurrencyCode, revenue.Amount);
        if (baseAmount <= threshold)
        {
            return;
        }

        if (string.Equals(revenue.ApprovalStatus, "approved", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(revenue.ApprovalStatus, "not_required", StringComparison.OrdinalIgnoreCase))
        {
            revenue.ApprovalStatus = "pending";
        }

        throw new ConflictAppException(
            "Doanh thu vượt ngưỡng phê duyệt; cần phê duyệt trước khi xác nhận.");
    }
}
