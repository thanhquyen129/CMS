using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Settlements.Commands;

public sealed record CreateCollectionCommand(
    decimal Amount,
    string CurrencyCode,
    DateOnly? ValueDate,
    Guid? CounterpartyId,
    Guid? BillId,
    string? ReferenceNo,
    string? Notes,
    string? IdempotencyKey = null) : IRequest<Guid>;

public sealed class CreateCollectionCommandValidator : AbstractValidator<CreateCollectionCommand>
{
    public CreateCollectionCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền thu phải lớn hơn 0.");
        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Mã tiền tệ không được để trống.")
            .Length(3).WithMessage("Mã tiền tệ phải gồm 3 ký tự.");
        RuleFor(x => x.ReferenceNo).MaximumLength(128).When(x => x.ReferenceNo is not null);
        RuleFor(x => x.Notes).MaximumLength(2048).When(x => x.Notes is not null);
    }
}

/// <summary>
/// Creates a cash-in collection. Does not create Revenue (C-004) and does not touch AR outstanding.
/// Fills BaseAmount via FX stub (ADR-0004).
/// </summary>
public sealed class CreateCollectionCommandHandler : IRequestHandler<CreateCollectionCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISettlementFxStub _fx;
    private readonly IIdempotencyGate _idempotency;

    public CreateCollectionCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ISettlementFxStub fx,
        IIdempotencyGate idempotency)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fx = fx;
        _idempotency = idempotency;
    }

    public async Task<Guid> Handle(CreateCollectionCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var priorId = await _idempotency.FindAsync(
            IdempotencyScopes.Collection,
            request.IdempotencyKey,
            cancellationToken);
        if (priorId.HasValue)
        {
            return priorId.Value;
        }

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
        var collection = new Collection
        {
            TenantId = tenantId,
            Amount = amount,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            ValueDate = request.ValueDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            CounterpartyId = request.CounterpartyId,
            BillId = request.BillId,
            ReferenceNo = string.IsNullOrWhiteSpace(request.ReferenceNo) ? null : request.ReferenceNo.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = CollectionStatuses.Open,
            RecordStatus = "active"
        };
        await _fx.ApplyToCollectionAsync(collection, amount, cancellationToken);

        _db.Collections.Add(collection);
        _idempotency.Remember(
            IdempotencyScopes.Collection,
            request.IdempotencyKey ?? string.Empty,
            collection.Id,
            tenantId);
        await _db.SaveChangesAsync(cancellationToken);

        var costCountAfter = await _db.Costs.CountAsync(cancellationToken);
        var revenueCountAfter = await _db.Revenues.CountAsync(cancellationToken);
        if (costCountAfter != costCountBefore || revenueCountAfter != revenueCountBefore)
        {
            throw new ConflictAppException(
                "Thu tiền không được tạo Chi phí hoặc Doanh thu mới (C-003/C-004).");
        }

        return collection.Id;
    }
}
