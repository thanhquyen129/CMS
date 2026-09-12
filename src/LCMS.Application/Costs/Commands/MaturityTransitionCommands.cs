using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Application.FinancialCloses;
using LCMS.Application.FinancialControl;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Commands;

public sealed record ConfirmCostCommand(Guid CostId, decimal? ConfirmedAmount) : IRequest;

public sealed class ConfirmCostCommandValidator : AbstractValidator<ConfirmCostCommand>
{
    public ConfirmCostCommandValidator()
    {
        RuleFor(x => x.CostId).NotEmpty().WithMessage("Chi phí không hợp lệ.");
        RuleFor(x => x.ConfirmedAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền xác nhận không được âm.")
            .When(x => x.ConfirmedAmount.HasValue);
    }
}

/// <summary>
/// Expected → Confirmed. Preserves ExpectedAmount (C-009); writes ConfirmedAmount + audit.
/// Optional approval threshold gate (Pass 2 Sprint 4 FULL).
/// Optional critical-exception block (Pass 2 Sprint 9 FULL).
/// Period lock when financial close is Locked (Pass 2 Sprint 10 FULL).
/// </summary>
public sealed class ConfirmCostCommandHandler : IRequestHandler<ConfirmCostCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly ICostFxStub _fx;
    private readonly ICostApprovalGate _approvalGate;
    private readonly ICriticalExceptionConfirmGate _criticalExceptionGate;
    private readonly IPeriodLockGate _periodLockGate;

    public ConfirmCostCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        ICostFxStub fx,
        ICostApprovalGate approvalGate,
        ICriticalExceptionConfirmGate criticalExceptionGate,
        IPeriodLockGate periodLockGate)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _fx = fx;
        _approvalGate = approvalGate;
        _criticalExceptionGate = criticalExceptionGate;
        _periodLockGate = periodLockGate;
    }

    public async Task Handle(ConfirmCostCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == request.CostId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi phí.");

        if (cost.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được xác nhận chi phí đang hiệu lực.");
        }

        if (!string.Equals(cost.FinancialMaturity, CostMaturities.Expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ chuyển Expected → Confirmed; không ghi đè mức độ trước.");
        }

        EnforceAttributionInvariants(cost);

        await _periodLockGate.EnsureMutationAllowedAsync(
            cost.BillId,
            cost.EffectiveDate,
            "xác nhận chi phí",
            cancellationToken);

        await _criticalExceptionGate.EnsureConfirmAllowedAsync(
            ApprovalObjectTypes.Cost,
            cost.Id,
            cancellationToken);

        try
        {
            _approvalGate.EnsureConfirmAllowed(cost);
        }
        catch (ConflictAppException)
        {
            // Persist pending flag so UI/queue can see the gate.
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }

        var confirmed = decimal.Round(
            request.ConfirmedAmount ?? cost.ExpectedAmount,
            4,
            MidpointRounding.AwayFromZero);

        var beforeJson =
            $"{{\"maturity\":\"{cost.FinancialMaturity}\",\"expected\":{cost.ExpectedAmount},\"amount\":{cost.Amount}}}";

        // Keep ExpectedAmount intact (C-009).
        cost.ConfirmedAmount = confirmed;
        cost.Amount = confirmed;
        cost.FinancialMaturity = CostMaturities.Confirmed;
        cost.ConfirmedAt = DateTimeOffset.UtcNow;
        cost.ConfirmedBy = _user.UserId;
        _fx.ApplyToCost(cost, confirmed);

        _audit.Append(
            AuditActions.CostConfirm,
            AuditObjectTypes.Cost,
            cost.Id,
            beforeJson: beforeJson,
            afterJson: $"{{\"maturity\":\"{CostMaturities.Confirmed}\",\"confirmed\":{confirmed},\"amount\":{confirmed},\"baseAmount\":{cost.BaseAmount}}}");

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static void EnforceAttributionInvariants(Cost cost)
    {
        if (string.Equals(cost.AttributionType, CostAttributionTypes.Direct, StringComparison.OrdinalIgnoreCase)
            && cost.BillId is null)
        {
            throw new ConflictAppException("Chi phí trực tiếp bắt buộc gắn Bill.");
        }

        if (string.Equals(cost.AttributionType, CostAttributionTypes.Shared, StringComparison.OrdinalIgnoreCase)
            && cost.BillId is not null)
        {
            throw new ConflictAppException("Chi phí chung (shared) không gắn Bill trực tiếp; dùng phân bổ.");
        }
    }
}

public sealed record ActualizeCostCommand(Guid CostId, decimal? ActualAmount) : IRequest;

public sealed class ActualizeCostCommandValidator : AbstractValidator<ActualizeCostCommand>
{
    public ActualizeCostCommandValidator()
    {
        RuleFor(x => x.CostId).NotEmpty().WithMessage("Chi phí không hợp lệ.");
        RuleFor(x => x.ActualAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền thực tế không được âm.")
            .When(x => x.ActualAmount.HasValue);
    }
}

/// <summary>
/// Confirmed → Actual. Preserves ExpectedAmount and ConfirmedAmount (C-009).
/// </summary>
public sealed class ActualizeCostCommandHandler : IRequestHandler<ActualizeCostCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly ICostFxStub _fx;

    public ActualizeCostCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        ICostFxStub fx)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _fx = fx;
    }

    public async Task Handle(ActualizeCostCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == request.CostId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi phí.");

        if (cost.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được thực tế hóa chi phí đang hiệu lực.");
        }

        if (!string.Equals(cost.FinancialMaturity, CostMaturities.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ chuyển Confirmed → Actual; không ghi đè mức độ trước.");
        }

        var actual = decimal.Round(
            request.ActualAmount ?? cost.ConfirmedAmount ?? cost.ExpectedAmount,
            4,
            MidpointRounding.AwayFromZero);

        cost.ActualAmount = actual;
        cost.Amount = actual;
        cost.FinancialMaturity = CostMaturities.Actual;
        cost.ActualizedAt = DateTimeOffset.UtcNow;
        cost.ActualizedBy = _user.UserId;
        _fx.ApplyToCost(cost, actual);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
