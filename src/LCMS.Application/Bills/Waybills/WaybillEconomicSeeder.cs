using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Application.Revenues;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Waybills;

/// <summary>Seeds Expected Cost/Revenue from waybill postage components (single economic layer).</summary>
public sealed class WaybillEconomicSeeder
{
    private readonly ILcmsDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly ICostFxStub _costFx;
    private readonly ICostApprovalGate _costApproval;
    private readonly IRevenueFxStub _revenueFx;
    private readonly IRevenueApprovalGate _revenueApproval;
    private readonly IAuditWriter _audit;

    public WaybillEconomicSeeder(
        ILcmsDbContext db,
        IPermissionService permissions,
        ICostFxStub costFx,
        ICostApprovalGate costApproval,
        IRevenueFxStub revenueFx,
        IRevenueApprovalGate revenueApproval,
        IAuditWriter audit)
    {
        _db = db;
        _permissions = permissions;
        _costFx = costFx;
        _costApproval = costApproval;
        _revenueFx = revenueFx;
        _revenueApproval = revenueApproval;
        _audit = audit;
    }

    /// <summary>
    /// Creates or updates Expected lines. Confirmed/Actual lines are never overwritten.
    /// </summary>
    public async Task SeedAsync(Bill bill, BillWaybill waybill, CancellationToken cancellationToken)
    {
        if (!WaybillChargeMath.HasAnyCharge(waybill))
        {
            return;
        }

        var currency = await _db.Currencies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == waybill.CurrencyCode, cancellationToken);
        if (currency is null)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["CurrencyCode"] = ["Mã tiền tệ chưa có trong danh mục. Vui lòng khai báo trước."]
            });
        }

        if (!currency.IsActive)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["CurrencyCode"] = ["Mã tiền tệ đã ngừng hiệu lực."]
            });
        }

        var isRevenue = string.Equals(
            waybill.ChargeEconomicRole,
            WaybillChargeEconomicRoles.Revenue,
            StringComparison.OrdinalIgnoreCase);

        if (isRevenue)
        {
            await SeedRevenuesAsync(bill, waybill, cancellationToken);
            return;
        }

        await _permissions.EnsureAsync(
            PermissionCodes.CostCreate,
            "Bạn không có quyền ghi chi phí dự kiến từ cước vận đơn.",
            cancellationToken);
        await SeedCostsAsync(bill, waybill, cancellationToken);
    }

    private async Task SeedCostsAsync(Bill bill, BillWaybill waybill, CancellationToken cancellationToken)
    {
        var effective = DateOnly.FromDateTime((waybill.SentAt ?? waybill.AcceptedAt ?? DateTimeOffset.UtcNow).UtcDateTime);
        foreach (var (code, _, amountFn) in WaybillChargeMath.ComponentLines)
        {
            var amount = WaybillChargeMath.RoundMoney(amountFn(waybill));
            var sourceType = WaybillChargeMath.SourceTypeFor(code);
            var existing = await _db.Costs.FirstOrDefaultAsync(
                c => c.SourceType == sourceType && c.SourceId == waybill.Id,
                cancellationToken);

            if (WaybillChargeMath.IsZero(amount))
            {
                if (existing is not null
                    && existing.FinancialMaturity == CostMaturities.Expected
                    && existing.RecordStatus == "active")
                {
                    existing.SoftDelete(null);
                }

                continue;
            }

            if (existing is not null)
            {
                if (existing.FinancialMaturity != CostMaturities.Expected)
                {
                    throw new ConflictAppException(
                        "Không sửa số cước đã xác nhận/thực tế. Dùng điều chỉnh chi phí trên Bill.");
                }

                existing.ExpectedAmount = amount;
                existing.Amount = amount;
                existing.CurrencyCode = waybill.CurrencyCode;
                existing.CostTypeCode = code;
                existing.EffectiveDate = effective;
                existing.OrganizationId ??= bill.OrganizationId;
                await _costFx.ApplyToCostAsync(existing, amount, cancellationToken);
                await _costApproval.RefreshPendingFlagAsync(existing, cancellationToken);
                continue;
            }

            var cost = new Cost
            {
                TenantId = waybill.TenantId,
                BillId = bill.Id,
                AttributionType = CostAttributionTypes.Direct,
                FinancialMaturity = CostMaturities.Expected,
                ExpectedAmount = amount,
                Amount = amount,
                CurrencyCode = waybill.CurrencyCode,
                CostTypeCode = code,
                SourceType = sourceType,
                SourceId = waybill.Id,
                RecordStatus = "active",
                ApprovalStatus = "not_required",
                EffectiveDate = effective,
                OrganizationId = bill.OrganizationId
            };
            await _costFx.ApplyToCostAsync(cost, amount, cancellationToken);
            await _costApproval.RefreshPendingFlagAsync(cost, cancellationToken);
            _db.Costs.Add(cost);
            _audit.Append(
                AuditActions.CostCreate,
                AuditObjectTypes.Cost,
                cost.Id,
                afterJson: AuditJson.Serialize(new
                {
                    id = cost.Id,
                    billId = bill.Id,
                    amount,
                    currency = waybill.CurrencyCode,
                    costTypeCode = code,
                    sourceType
                }));
        }
    }

    private async Task SeedRevenuesAsync(Bill bill, BillWaybill waybill, CancellationToken cancellationToken)
    {
        var effective = DateOnly.FromDateTime((waybill.SentAt ?? waybill.AcceptedAt ?? DateTimeOffset.UtcNow).UtcDateTime);
        foreach (var (code, _, amountFn) in WaybillChargeMath.ComponentLines)
        {
            var amount = WaybillChargeMath.RoundMoney(amountFn(waybill));
            var sourceType = WaybillChargeMath.SourceTypeFor(code);
            var existing = await _db.Revenues.FirstOrDefaultAsync(
                r => r.SourceType == sourceType && r.SourceId == waybill.Id,
                cancellationToken);

            if (WaybillChargeMath.IsZero(amount))
            {
                if (existing is not null
                    && existing.FinancialMaturity == RevenueMaturities.Expected
                    && existing.RecordStatus == "active")
                {
                    existing.SoftDelete(null);
                }

                continue;
            }

            if (existing is not null)
            {
                if (existing.FinancialMaturity != RevenueMaturities.Expected)
                {
                    throw new ConflictAppException(
                        "Không sửa số cước đã xác nhận/thực tế. Dùng điều chỉnh doanh thu trên Bill.");
                }

                existing.ExpectedAmount = amount;
                existing.Amount = amount;
                existing.CurrencyCode = waybill.CurrencyCode;
                existing.RevenueTypeCode = code;
                existing.EffectiveDate = effective;
                await _revenueFx.ApplyToRevenueAsync(existing, amount, cancellationToken);
                _revenueApproval.RefreshPendingFlag(existing);
                continue;
            }

            var revenue = new Revenue
            {
                TenantId = waybill.TenantId,
                BillId = bill.Id,
                FinancialMaturity = RevenueMaturities.Expected,
                ExpectedAmount = amount,
                Amount = amount,
                CurrencyCode = waybill.CurrencyCode,
                RevenueTypeCode = code,
                SourceType = sourceType,
                SourceId = waybill.Id,
                RecordStatus = "active",
                ApprovalStatus = "not_required",
                EffectiveDate = effective
            };
            await _revenueFx.ApplyToRevenueAsync(revenue, amount, cancellationToken);
            _revenueApproval.RefreshPendingFlag(revenue);
            _db.Revenues.Add(revenue);
            _audit.Append(
                AuditActions.RevenueCreate,
                AuditObjectTypes.Revenue,
                revenue.Id,
                afterJson: AuditJson.Serialize(new
                {
                    id = revenue.Id,
                    billId = bill.Id,
                    amount,
                    currency = waybill.CurrencyCode,
                    revenueTypeCode = code,
                    sourceType
                }));
        }
    }
}
