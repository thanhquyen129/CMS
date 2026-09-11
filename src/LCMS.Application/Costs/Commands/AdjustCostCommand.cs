using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Commands;

public sealed record AdjustCostCommand(
    Guid CostId,
    string AdjustmentType,
    decimal DeltaAmount,
    string Reason,
    DateOnly? EffectiveDate) : IRequest<Guid>;

public sealed class AdjustCostCommandValidator : AbstractValidator<AdjustCostCommand>
{
    public AdjustCostCommandValidator()
    {
        RuleFor(x => x.CostId).NotEmpty().WithMessage("Chi phí không hợp lệ.");
        RuleFor(x => x.AdjustmentType)
            .NotEmpty().WithMessage("Loại điều chỉnh không được để trống.")
            .Must(t => t is CostAdjustmentTypes.Adjustment or CostAdjustmentTypes.Reversal)
            .WithMessage("Loại điều chỉnh phải là adjustment hoặc reversal.");
        RuleFor(x => x.DeltaAmount)
            .NotEqual(0).WithMessage("Số tiền điều chỉnh không được bằng 0.");
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do điều chỉnh không được để trống.")
            .MaximumLength(1024).WithMessage("Lý do điều chỉnh không được vượt quá 1024 ký tự.");
    }
}

/// <summary>
/// Creates cost_adjustments row and applies delta to current maturity layer — no silent overwrite (C-009/C-013).
/// </summary>
public sealed class AdjustCostCommandHandler : IRequestHandler<AdjustCostCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICostFxStub _fx;
    private readonly ICostApprovalGate _approvalGate;

    public AdjustCostCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICostFxStub fx,
        ICostApprovalGate approvalGate)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fx = fx;
        _approvalGate = approvalGate;
    }

    public async Task<Guid> Handle(AdjustCostCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == request.CostId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi phí.");

        if (cost.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được điều chỉnh chi phí đang hiệu lực.");
        }

        var delta = decimal.Round(request.DeltaAmount, 4, MidpointRounding.AwayFromZero);
        var type = request.AdjustmentType.Trim().ToLowerInvariant();
        if (type == CostAdjustmentTypes.Reversal && delta > 0)
        {
            delta = -delta;
        }

        var before = cost.Amount;
        var after = decimal.Round(before + delta, 4, MidpointRounding.AwayFromZero);
        if (after < 0)
        {
            throw new ConflictAppException("Số tiền chi phí sau điều chỉnh không được âm.");
        }

        var maturity = cost.FinancialMaturity.ToLowerInvariant();
        switch (maturity)
        {
            case CostMaturities.Expected:
                cost.ExpectedAmount = after;
                break;
            case CostMaturities.Confirmed:
                cost.ConfirmedAmount = after;
                break;
            case CostMaturities.Actual:
                cost.ActualAmount = after;
                break;
            default:
                throw new ConflictAppException("Mức độ tài chính của chi phí không hợp lệ.");
        }

        cost.Amount = after;
        _fx.ApplyToCost(cost, after);
        _approvalGate.RefreshPendingFlag(cost);

        var adj = new CostAdjustment
        {
            TenantId = tenantId,
            CostId = cost.Id,
            AdjustmentType = type,
            DeltaAmount = delta,
            CurrencyCode = cost.CurrencyCode,
            Reason = request.Reason.Trim(),
            EffectiveDate = request.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            AppliedToMaturity = maturity,
            AmountBefore = before,
            AmountAfter = after
        };

        _db.CostAdjustments.Add(adj);
        await _db.SaveChangesAsync(cancellationToken);
        return adj.Id;
    }
}
