using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Application.Revenues;
using LCMS.Application.Tenancy;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Exposures;

/// <summary>
/// Blocks AP/AR recognition when the source line or the exposure ceiling exceeds the confirm threshold
/// and is not approved. Null threshold leaves recognition unchanged.
/// </summary>
public interface IRecognitionApprovalGate
{
    Task EnsurePayableRecognizeAllowedAsync(PayableExposure exposure, CancellationToken cancellationToken);

    Task EnsureReceivableRecognizeAllowedAsync(ReceivableExposure exposure, CancellationToken cancellationToken);
}

public sealed class RecognitionApprovalGate : IRecognitionApprovalGate
{
    private readonly ILcmsDbContext _db;
    private readonly IAuditWriter _audit;
    private readonly ICostApprovalGate _costGate;
    private readonly ICostFxStub _costFx;
    private readonly IRevenueFxStub _revenueFx;
    private readonly RevenueOptions _revenueOptions;
    private readonly TenantFinancialOptionsResolver _financial;

    public RecognitionApprovalGate(
        ILcmsDbContext db,
        IAuditWriter audit,
        ICostApprovalGate costGate,
        ICostFxStub costFx,
        IRevenueFxStub revenueFx,
        IOptions<RevenueOptions> revenueOptions,
        TenantFinancialOptionsResolver financial)
    {
        _db = db;
        _audit = audit;
        _costGate = costGate;
        _costFx = costFx;
        _revenueFx = revenueFx;
        _revenueOptions = revenueOptions.Value;
        _financial = financial;
    }

    public async Task EnsurePayableRecognizeAllowedAsync(
        PayableExposure exposure,
        CancellationToken cancellationToken)
    {
        if (exposure.CostId is Guid costId)
        {
            var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == costId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy chi phí nguồn của exposure.");
            try
            {
                await _costGate.EnsureConfirmAllowedAsync(cost, cancellationToken);
            }
            catch (ConflictAppException)
            {
                await BlockAsync(
                    AuditActions.AccountsPayableRecognizeBlocked,
                    AuditObjectTypes.PayableExposure,
                    exposure.Id,
                    "Chi phí nguồn vượt ngưỡng phê duyệt; cần phê duyệt trước khi ghi nhận phải trả.",
                    cancellationToken);
            }

            return;
        }

        await EnsureCeilingApprovedAsync(
            exposure.Amount,
            exposure.CurrencyCode,
            exposure.EffectiveDate,
            moduleThreshold: null,
            useRevenueFx: false,
            ApprovalObjectTypes.PayableExposure,
            exposure.Id,
            AuditActions.AccountsPayableRecognizeBlocked,
            AuditObjectTypes.PayableExposure,
            "Ghi nhận phải trả vượt ngưỡng phê duyệt; cần phê duyệt exposure trước khi ghi nhận.",
            cancellationToken);
    }

    public async Task EnsureReceivableRecognizeAllowedAsync(
        ReceivableExposure exposure,
        CancellationToken cancellationToken)
    {
        if (exposure.RevenueId is Guid revenueId)
        {
            var revenue = await _db.Revenues.FirstOrDefaultAsync(r => r.Id == revenueId, cancellationToken)
                ?? throw new NotFoundAppException("Không tìm thấy doanh thu nguồn của exposure.");
            if (string.Equals(revenue.ApprovalStatus, "approved", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var threshold = _revenueOptions.ConfirmApprovalThresholdBase
                ?? await _financial.GetConfirmApprovalThresholdBaseAsync(cancellationToken);
            if (threshold is not decimal revenueThreshold)
            {
                return;
            }

            var basis = revenue.BaseAmount
                ?? await _revenueFx.ToBaseAmountAsync(
                    revenue.CurrencyCode,
                    revenue.Amount,
                    revenue.EffectiveDate,
                    cancellationToken);
            if (basis <= revenueThreshold)
            {
                return;
            }

            if (string.Equals(revenue.ApprovalStatus, "not_required", StringComparison.OrdinalIgnoreCase)
                || string.Equals(revenue.ApprovalStatus, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                revenue.ApprovalStatus = "pending";
            }

            await BlockAsync(
                AuditActions.AccountsReceivableRecognizeBlocked,
                AuditObjectTypes.ReceivableExposure,
                exposure.Id,
                "Doanh thu nguồn vượt ngưỡng phê duyệt; cần phê duyệt trước khi ghi nhận phải thu.",
                cancellationToken);
        }

        await EnsureCeilingApprovedAsync(
            exposure.Amount,
            exposure.CurrencyCode,
            exposure.EffectiveDate,
            _revenueOptions.ConfirmApprovalThresholdBase,
            useRevenueFx: true,
            ApprovalObjectTypes.ReceivableExposure,
            exposure.Id,
            AuditActions.AccountsReceivableRecognizeBlocked,
            AuditObjectTypes.ReceivableExposure,
            "Ghi nhận phải thu vượt ngưỡng phê duyệt; cần phê duyệt exposure trước khi ghi nhận.",
            cancellationToken);
    }

    private async Task EnsureCeilingApprovedAsync(
        decimal ceiling,
        string currency,
        DateOnly asOf,
        decimal? moduleThreshold,
        bool useRevenueFx,
        string approvalObjectType,
        Guid objectId,
        string auditAction,
        string auditObjectType,
        string message,
        CancellationToken cancellationToken)
    {
        var threshold = moduleThreshold ?? await _financial.GetConfirmApprovalThresholdBaseAsync(cancellationToken);
        if (threshold is not decimal t)
        {
            return;
        }

        var basis = useRevenueFx
            ? await _revenueFx.ToBaseAmountAsync(currency, ceiling, asOf, cancellationToken)
            : await _costFx.ToBaseAmountAsync(currency, ceiling, asOf, cancellationToken);
        if (basis <= t)
        {
            return;
        }

        var approved = await _db.Approvals.AsNoTracking().AnyAsync(
            a => a.ObjectType == approvalObjectType
                 && a.ObjectId == objectId
                 && a.Status == ApprovalStatuses.Approved,
            cancellationToken);
        if (approved)
        {
            return;
        }

        await BlockAsync(auditAction, auditObjectType, objectId, message, cancellationToken);
    }

    private async Task BlockAsync(
        string action,
        string objectType,
        Guid objectId,
        string message,
        CancellationToken cancellationToken)
    {
        _audit.Append(action, objectType, objectId, reason: message);
        await _db.SaveChangesAsync(cancellationToken);
        throw new ConflictAppException(message);
    }
}
