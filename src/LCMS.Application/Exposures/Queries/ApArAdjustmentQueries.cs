using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Queries;

public sealed record ApArAdjustmentDto(
    Guid Id,
    string AdjustmentType,
    decimal DeltaAmount,
    string CurrencyCode,
    string Reason,
    DateOnly EffectiveDate,
    decimal AdjustmentAmountBefore,
    decimal AdjustmentAmountAfter,
    decimal OutstandingBefore,
    decimal OutstandingAfter,
    DateTimeOffset CreatedAt);

public sealed record ListAccountsPayableAdjustmentsQuery(Guid AccountsPayableId)
    : IRequest<IReadOnlyList<ApArAdjustmentDto>>;

public sealed class ListAccountsPayableAdjustmentsQueryHandler
    : IRequestHandler<ListAccountsPayableAdjustmentsQuery, IReadOnlyList<ApArAdjustmentDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListAccountsPayableAdjustmentsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ApArAdjustmentDto>> Handle(
        ListAccountsPayableAdjustmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var exists = await _db.AccountsPayable.AsNoTracking()
            .AnyAsync(a => a.Id == request.AccountsPayableId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundAppException("Không tìm thấy khoản phải trả.");
        }

        return await _db.AccountsPayableAdjustments.AsNoTracking()
            .Where(a => a.AccountsPayableId == request.AccountsPayableId)
            .OrderByDescending(a => a.Id)
            .Select(a => new ApArAdjustmentDto(
                a.Id,
                a.AdjustmentType,
                a.DeltaAmount,
                a.CurrencyCode,
                a.Reason,
                a.EffectiveDate,
                a.AdjustmentAmountBefore,
                a.AdjustmentAmountAfter,
                a.OutstandingBefore,
                a.OutstandingAfter,
                a.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed record ListAccountsReceivableAdjustmentsQuery(Guid AccountsReceivableId)
    : IRequest<IReadOnlyList<ApArAdjustmentDto>>;

public sealed class ListAccountsReceivableAdjustmentsQueryHandler
    : IRequestHandler<ListAccountsReceivableAdjustmentsQuery, IReadOnlyList<ApArAdjustmentDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListAccountsReceivableAdjustmentsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ApArAdjustmentDto>> Handle(
        ListAccountsReceivableAdjustmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var exists = await _db.AccountsReceivable.AsNoTracking()
            .AnyAsync(a => a.Id == request.AccountsReceivableId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundAppException("Không tìm thấy khoản phải thu.");
        }

        return await _db.AccountsReceivableAdjustments.AsNoTracking()
            .Where(a => a.AccountsReceivableId == request.AccountsReceivableId)
            .OrderByDescending(a => a.Id)
            .Select(a => new ApArAdjustmentDto(
                a.Id,
                a.AdjustmentType,
                a.DeltaAmount,
                a.CurrencyCode,
                a.Reason,
                a.EffectiveDate,
                a.AdjustmentAmountBefore,
                a.AdjustmentAmountAfter,
                a.OutstandingBefore,
                a.OutstandingAfter,
                a.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
