using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.FinancialCloses;
using LCMS.Application.Fx;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record AllocatePaymentCommand(
    Guid PaymentId,
    Guid AccountsPayableId,
    decimal Amount,
    string? Notes,
    string? IdempotencyKey = null,
    string? IfMatch = null) : IRequest<Guid>;

public sealed class AllocatePaymentCommandValidator : AbstractValidator<AllocatePaymentCommand>
{
    public AllocatePaymentCommandValidator()
    {
        RuleFor(x => x.PaymentId).NotEmpty().WithMessage("Thanh toán không hợp lệ.");
        RuleFor(x => x.AccountsPayableId).NotEmpty().WithMessage("Khoản phải trả không hợp lệ.");
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền phân bổ thanh toán phải lớn hơn 0.");
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
    }
}

/// <summary>
/// Draft allocation Payment → AP. Does NOT change outstanding (AC-007).
/// Enforces C-008 ceilings against payment amount and AP open obligation (over policy stub = 0).
/// Supports multi-allocation until Unapplied/AvailableToAllocate is exhausted.
/// Period lock when financial close is Locked (Pass 2 Sprint 10 FULL).
/// </summary>
public sealed class AllocatePaymentCommandHandler : IRequestHandler<AllocatePaymentCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISettlementFxStub _fx;
    private readonly IFxRateLookup _rates;
    private readonly IPeriodLockGate _periodLockGate;
    private readonly IIdempotencyGate _idempotency;
    private readonly IRowVersionGuard _versions;

    public AllocatePaymentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ISettlementFxStub fx,
        IFxRateLookup rates,
        IPeriodLockGate periodLockGate,
        IIdempotencyGate idempotency,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fx = fx;
        _rates = rates;
        _periodLockGate = periodLockGate;
        _idempotency = idempotency;
        _versions = versions;
    }

    public async Task<Guid> Handle(AllocatePaymentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var priorId = await _idempotency.FindAsync(
            IdempotencyScopes.PaymentAllocation,
            request.IdempotencyKey,
            cancellationToken);
        if (priorId.HasValue)
        {
            return priorId.Value;
        }

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);

        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thanh toán.");
        _versions.EnsureCurrent(payment, request.IfMatch);
        payment.TouchRowVersion();

        if (payment.Status == PaymentStatuses.Cancelled)
        {
            throw new ConflictAppException("Không thể phân bổ thanh toán đã hủy.");
        }

        var ap = await _db.AccountsPayable
            .FirstOrDefaultAsync(a => a.Id == request.AccountsPayableId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả.");

        await _periodLockGate.EnsureMutationAllowedAsync(
            payment.BillId ?? ap.BillId,
            payment.ValueDate,
            "phân bổ thanh toán",
            cancellationToken);

        var paymentActiveAmounts = await _db.PaymentAllocations.AsNoTracking()
            .Where(a => a.PaymentId == payment.Id
                && (a.AllocationStatus == SettlementAllocationStatuses.Draft
                    || a.AllocationStatus == SettlementAllocationStatuses.Finalized))
            .Select(a => a.Amount)
            .ToListAsync(cancellationToken);
        var paymentActive = paymentActiveAmounts.Sum();

        var paymentRemaining = payment.Amount - paymentActive + SettlementHelpers.OverSettlementTolerance;
        if (amount > paymentRemaining)
        {
            throw new ConflictAppException(
                $"Tổng phân bổ vượt số tiền thanh toán (còn lại {paymentRemaining}) (C-008).");
        }

        var apActiveAmounts = await _db.PaymentAllocations.AsNoTracking()
            .Where(a => a.AccountsPayableId == ap.Id
                && (a.AllocationStatus == SettlementAllocationStatuses.Draft
                    || a.AllocationStatus == SettlementAllocationStatuses.Finalized))
            .Select(a => a.SettledAmount ?? a.Amount)
            .ToListAsync(cancellationToken);
        var apActive = apActiveAmounts.Sum();

        decimal settled = amount;
        await SettlementCurrency.StampAsync(
            _rates,
            payment.CurrencyCode,
            ap.CurrencyCode,
            amount,
            payment.ValueDate,
            (original, target, rate, source, rateDate, rateId) =>
            {
                settled = target;
            },
            cancellationToken);

        var apCeiling = ap.RecognizedAmount + ap.AdjustmentAmount + SettlementHelpers.OverSettlementTolerance;
        if (apActive + settled > apCeiling)
        {
            throw new ConflictAppException(
                "Tổng phân bổ vượt số dư còn lại của khoản phải trả (C-008).");
        }

        var costCountBefore = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountBefore = await _db.Revenues.CountAsync(cancellationToken);

        var allocation = new PaymentAllocation
        {
            TenantId = tenantId,
            PaymentId = payment.Id,
            AccountsPayableId = ap.Id,
            Amount = amount,
            AllocationStatus = SettlementAllocationStatuses.Draft,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
        };
        await SettlementCurrency.StampAsync(
            _rates,
            payment.CurrencyCode,
            ap.CurrencyCode,
            amount,
            payment.ValueDate,
            (original, target, rate, source, rateDate, rateId) =>
            {
                allocation.OriginalAmount = original;
                allocation.SettledAmount = target;
                allocation.FxRate = rate;
                allocation.FxSource = source;
                allocation.FxRateDate = rateDate;
                allocation.FxRateId = rateId ?? allocation.FxRateId;
            },
            cancellationToken);
        await _fx.ApplyToPaymentAllocationAsync(
            allocation,
            payment.CurrencyCode,
            amount,
            payment.ValueDate,
            cancellationToken);

        _db.PaymentAllocations.Add(allocation);
        _idempotency.Remember(
            IdempotencyScopes.PaymentAllocation,
            request.IdempotencyKey ?? string.Empty,
            allocation.Id,
            tenantId);
        await _db.SaveChangesAsync(cancellationToken);

        var costCountAfter = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (costCountAfter != costCountBefore || revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException("Phân bổ thanh toán không được tạo Chi phí hoặc Doanh thu mới (C-003/C-004).");
        }

        return allocation.Id;
    }
}
