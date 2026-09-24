using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Revenues;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Commands;

public sealed record AdjustRevenueCommand(
    Guid RevenueId,
    string AdjustmentType,
    decimal DeltaAmount,
    string Reason,
    DateOnly? EffectiveDate,
    string? IdempotencyKey = null,
    string? IfMatch = null) : IRequest<Guid>;

public sealed class AdjustRevenueCommandValidator : AbstractValidator<AdjustRevenueCommand>
{
    public AdjustRevenueCommandValidator()
    {
        RuleFor(x => x.RevenueId).NotEmpty().WithMessage("Doanh thu không hợp lệ.");
        RuleFor(x => x.AdjustmentType)
            .NotEmpty().WithMessage("Loại điều chỉnh không được để trống.")
            .Must(t => t is RevenueAdjustmentTypes.Adjustment or RevenueAdjustmentTypes.Reversal)
            .WithMessage("Loại điều chỉnh phải là adjustment hoặc reversal.");
        RuleFor(x => x.DeltaAmount)
            .NotEqual(0).WithMessage("Số tiền điều chỉnh không được bằng 0.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do điều chỉnh không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do điều chỉnh không được vượt quá 1024 ký tự.");
    }
}

/// <summary>
/// Creates revenue_adjustments row and applies delta to current maturity layer — no silent overwrite (C-009).
/// </summary>
public sealed class AdjustRevenueCommandHandler : IRequestHandler<AdjustRevenueCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IRevenueFxStub _fx;
    private readonly IRevenueApprovalGate _approvalGate;
    private readonly IPermissionService _permissions;
    private readonly IIdempotencyGate _idempotency;
    private readonly IRowVersionGuard _versions;

    public AdjustRevenueCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IRevenueFxStub fx,
        IRevenueApprovalGate approvalGate,
        IPermissionService permissions,
        IIdempotencyGate idempotency,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fx = fx;
        _approvalGate = approvalGate;
        _permissions = permissions;
        _idempotency = idempotency;
        _versions = versions;
    }

    public async Task<Guid> Handle(AdjustRevenueCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var revenue = await _db.Revenues.FirstOrDefaultAsync(r => r.Id == request.RevenueId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy doanh thu.");

        if (revenue.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được điều chỉnh doanh thu đang hiệu lực.");
        }

        var maturity = revenue.FinancialMaturity.ToLowerInvariant();
        var (action, denied) = maturity switch
        {
            RevenueMaturities.Expected => (PermissionCodes.RevenueCreate, "Bạn không có quyền điều chỉnh doanh thu dự kiến."),
            RevenueMaturities.Confirmed => (PermissionCodes.RevenueConfirm, "Bạn không có quyền điều chỉnh doanh thu đã xác nhận."),
            RevenueMaturities.Actual => (PermissionCodes.RevenueActualize, "Bạn không có quyền điều chỉnh doanh thu thực tế."),
            _ => throw new ConflictAppException("Mức độ tài chính của doanh thu không hợp lệ.")
        };
        await _permissions.EnsureAsync(action, denied, cancellationToken);

        var priorId = await _idempotency.FindAsync(
            IdempotencyScopes.RevenueAdjustment,
            request.IdempotencyKey,
            cancellationToken);
        if (priorId.HasValue)
        {
            return priorId.Value;
        }

        _versions.EnsureCurrent(revenue, request.IfMatch);

        var delta = decimal.Round(request.DeltaAmount, 4, MidpointRounding.AwayFromZero);
        var type = request.AdjustmentType.Trim().ToLowerInvariant();
        if (type == RevenueAdjustmentTypes.Reversal && delta > 0)
        {
            delta = -delta;
        }

        var before = revenue.Amount;
        var after = decimal.Round(before + delta, 4, MidpointRounding.AwayFromZero);
        if (after < 0)
        {
            throw new ConflictAppException("Số tiền doanh thu sau điều chỉnh không được âm.");
        }

        switch (maturity)
        {
            case RevenueMaturities.Expected:
                revenue.ExpectedAmount = after;
                break;
            case RevenueMaturities.Confirmed:
                revenue.ConfirmedAmount = after;
                break;
            case RevenueMaturities.Actual:
                revenue.ActualAmount = after;
                break;
            default:
                throw new ConflictAppException("Mức độ tài chính của doanh thu không hợp lệ.");
        }

        revenue.Amount = after;
        await _fx.ApplyToRevenueAsync(revenue, after, cancellationToken);
        _approvalGate.RefreshPendingFlag(revenue);

        var adj = new RevenueAdjustment
        {
            TenantId = tenantId,
            RevenueId = revenue.Id,
            AdjustmentType = type,
            DeltaAmount = delta,
            CurrencyCode = revenue.CurrencyCode,
            Reason = request.Reason.Trim(),
            EffectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            AppliedToMaturity = maturity,
            AmountBefore = before,
            AmountAfter = after
        };

        _db.RevenueAdjustments.Add(adj);
        _idempotency.Remember(
            IdempotencyScopes.RevenueAdjustment,
            request.IdempotencyKey ?? string.Empty,
            adj.Id,
            tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return adj.Id;
    }
}
