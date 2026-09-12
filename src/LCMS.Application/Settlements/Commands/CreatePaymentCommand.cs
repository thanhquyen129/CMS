using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record CreatePaymentCommand(
    decimal Amount,
    string CurrencyCode,
    DateOnly? ValueDate,
    Guid? CounterpartyId,
    Guid? BillId,
    string? ReferenceNo,
    string? Notes) : IRequest<Guid>;

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền thanh toán phải lớn hơn 0.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.ReferenceNo).MaximumLength(128).When(x => x.ReferenceNo is not null);
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
    }
}

/// <summary>
/// Creates a cash-out payment. Does not create Cost (C-003) and does not touch AP outstanding.
/// Fills BaseAmount via FX stub (ADR-0004).
/// </summary>
public sealed class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISettlementFxStub _fx;

    public CreatePaymentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ISettlementFxStub fx)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fx = fx;
    }

    public async Task<Guid> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        if (request.BillId.HasValue)
        {
            var billExists = await _db.Bills.AsNoTracking()
                .AnyAsync(b => b.Id == request.BillId, cancellationToken);
            if (!billExists)
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }
        }

        var costCountBefore = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountBefore = await _db.Revenues.CountAsync(cancellationToken);

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);
        var payment = new Payment
        {
            TenantId = tenantId,
            Amount = amount,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            ValueDate = request.ValueDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            CounterpartyId = request.CounterpartyId,
            BillId = request.BillId,
            ReferenceNo = string.IsNullOrWhiteSpace(request.ReferenceNo) ? null : request.ReferenceNo.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = PaymentStatuses.Open,
            RecordStatus = "active"
        };
        await _fx.ApplyToPaymentAsync(payment, amount, cancellationToken);

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        var costCountAfter = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (costCountAfter != costCountBefore || revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException(
                "Thanh toán không được tạo Chi phí hoặc Doanh thu mới (C-003/C-004).");
        }

        return payment.Id;
    }
}
