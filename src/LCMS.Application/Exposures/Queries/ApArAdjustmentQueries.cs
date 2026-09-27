using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
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
    DateTimeOffset CreatedAt,
    Guid? CreatedBy = null,
    Guid? ReversesAdjustmentId = null);

public sealed record ListAccountsPayableAdjustmentsQuery(Guid AccountsPayableId)
    : IRequest<IReadOnlyList<ApArAdjustmentDto>>;

public sealed class ListAccountsPayableAdjustmentsQueryHandler
    : IRequestHandler<ListAccountsPayableAdjustmentsQuery, IReadOnlyList<ApArAdjustmentDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public ListAccountsPayableAdjustmentsQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissions,
        IOrganizationHierarchyService orgHierarchy)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissions = permissions;
        _orgHierarchy = orgHierarchy;
    }

    public async Task<IReadOnlyList<ApArAdjustmentDto>> Handle(
        ListAccountsPayableAdjustmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await ApArLedgerAccess.LoadPayableAsync(
            _db, _userContext, _permissions, _orgHierarchy, request.AccountsPayableId, cancellationToken);

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
                a.CreatedAt,
                a.CreatedBy,
                a.ReversesAdjustmentId))
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
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public ListAccountsReceivableAdjustmentsQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissions,
        IOrganizationHierarchyService orgHierarchy)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissions = permissions;
        _orgHierarchy = orgHierarchy;
    }

    public async Task<IReadOnlyList<ApArAdjustmentDto>> Handle(
        ListAccountsReceivableAdjustmentsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await ApArLedgerAccess.LoadReceivableAsync(
            _db, _userContext, _permissions, _orgHierarchy, request.AccountsReceivableId, cancellationToken);

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
                a.CreatedAt,
                a.CreatedBy,
                a.ReversesAdjustmentId))
            .ToListAsync(cancellationToken);
    }
}
