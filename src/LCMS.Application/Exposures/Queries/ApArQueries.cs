using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Application.Tenancy;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Queries;

/// <summary>
/// AP/AR DTOs always expose Outstanding as a derived field (C-015).
/// Aging fields are derived (ADR-0006) — never user SoT.
/// </summary>
public sealed record AccountsPayableDto(
    Guid Id,
    Guid PayableExposureId,
    decimal RecognizedAmount,
    decimal AdjustmentAmount,
    decimal FinalizedSettledAmount,
    decimal Outstanding,
    string CurrencyCode,
    DateOnly? DueDate,
    string SettlementStatus,
    Guid? BillId,
    Guid? CounterpartyId,
    DateTimeOffset RecognizedAt,
    string? Notes,
    string RecordStatus,
    int? DaysPastDue,
    string AgingBucket,
    byte[]? RowVersion = null,
    string? BillNo = null);

public sealed record AccountsReceivableDto(
    Guid Id,
    Guid ReceivableExposureId,
    decimal RecognizedAmount,
    decimal AdjustmentAmount,
    decimal FinalizedSettledAmount,
    decimal Outstanding,
    string CurrencyCode,
    DateOnly? DueDate,
    string SettlementStatus,
    Guid? BillId,
    Guid? CounterpartyId,
    DateTimeOffset RecognizedAt,
    string? Notes,
    string RecordStatus,
    int? DaysPastDue,
    string AgingBucket,
    byte[]? RowVersion = null,
    string? BillNo = null);

public sealed record AgingBucketSummaryDto(
    string Bucket,
    int Count,
    decimal Outstanding);

public sealed record ApArAgingReportDto(
    DateOnly AsOf,
    IReadOnlyList<AgingBucketSummaryDto> Buckets,
    IReadOnlyList<AccountsPayableDto>? PayableItems,
    IReadOnlyList<AccountsReceivableDto>? ReceivableItems);

public sealed record ListAccountsPayableQuery(string? SettlementStatus, DateOnly? AsOf)
    : IRequest<IReadOnlyList<AccountsPayableDto>>;

public sealed record GetAccountsPayableByIdQuery(Guid Id, DateOnly? AsOf) : IRequest<AccountsPayableDto>;

public sealed record ListAccountsReceivableQuery(string? SettlementStatus, DateOnly? AsOf)
    : IRequest<IReadOnlyList<AccountsReceivableDto>>;

public sealed record GetAccountsReceivableByIdQuery(Guid Id, DateOnly? AsOf) : IRequest<AccountsReceivableDto>;

public sealed record GetAccountsPayableAgingQuery(
    DateOnly? AsOf,
    Guid? CounterpartyId,
    string? CurrencyCode,
    bool IncludeSettled) : IRequest<ApArAgingReportDto>;

public sealed record GetAccountsReceivableAgingQuery(
    DateOnly? AsOf,
    Guid? CounterpartyId,
    string? CurrencyCode,
    bool IncludeSettled) : IRequest<ApArAgingReportDto>;

public sealed class ListAccountsPayableQueryHandler
    : IRequestHandler<ListAccountsPayableQuery, IReadOnlyList<AccountsPayableDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public ListAccountsPayableQueryHandler(
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

    public async Task<IReadOnlyList<AccountsPayableDto>> Handle(
        ListAccountsPayableQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (scope, orgSubtree) = await DataScopeFilter.ResolveAsync(
            _permissions, _userContext, _db, _orgHierarchy,
            PermissionCodes.CostRead, "Bạn không có quyền xem khoản phải trả.", cancellationToken);

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.AccountsPayable.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SettlementStatus))
        {
            var status = request.SettlementStatus.Trim().ToLowerInvariant();
            query = query.Where(a => a.SettlementStatus == status);
        }

        if (scope == DataScopes.Own)
        {
            if (!_userContext.HasUser)
            {
                return [];
            }

            query = query.Where(a => a.CreatedBy == _userContext.UserId);
        }
        else if (scope == DataScopes.Organization)
        {
            var billIds = await DataScopeFilter.BillIdsInOrgSubtreeAsync(_db, orgSubtree, cancellationToken);
            if (billIds.Count == 0)
            {
                return [];
            }

            query = query.Where(a => a.BillId != null && billIds.Contains(a.BillId.Value));
        }

        var rows = await query.OrderByDescending(a => a.Id).ToListAsync(cancellationToken);
        var billNos = await DataScopeFilter.LoadBillNosAsync(_db, rows.Select(a => a.BillId), cancellationToken);
        return rows.Select(a => MapAp(a, asOf) with
        {
            BillNo = a.BillId is Guid billId && billNos.TryGetValue(billId, out var no) ? no : null
        }).ToList();
    }

    internal static AccountsPayableDto MapAp(Domain.Entities.AccountsPayable a, DateOnly asOf)
    {
        var (days, bucket) = AgingBuckets.Classify(a.DueDate, asOf);
        return new(
            a.Id,
            a.PayableExposureId,
            a.RecognizedAmount,
            a.AdjustmentAmount,
            a.FinalizedSettledAmount,
            a.DeriveOutstanding(),
            a.CurrencyCode,
            a.DueDate,
            a.SettlementStatus,
            a.BillId,
            a.CounterpartyId,
            a.RecognizedAt,
            a.Notes,
            a.RecordStatus,
            days,
            bucket,
            a.RowVersion);
    }
}

public sealed class GetAccountsPayableByIdQueryHandler
    : IRequestHandler<GetAccountsPayableByIdQuery, AccountsPayableDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public GetAccountsPayableByIdQueryHandler(
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

    public async Task<AccountsPayableDto> Handle(
        GetAccountsPayableByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (scope, orgSubtree) = await DataScopeFilter.ResolveAsync(
            _permissions, _userContext, _db, _orgHierarchy,
            PermissionCodes.CostRead, "Bạn không có quyền xem khoản phải trả.", cancellationToken);

        var a = await _db.AccountsPayable.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả.");

        var billOrgId = await DataScopeFilter.BillOrganizationIdAsync(_db, a.BillId, cancellationToken);
        if (!DataScopeFilter.AllowsViaBillOrg(
                scope, _userContext.UserId, orgSubtree, a.CreatedBy, billOrgId))
        {
            throw new NotFoundAppException("Không tìm thấy khoản phải trả.");
        }

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var billNos = await DataScopeFilter.LoadBillNosAsync(_db, [a.BillId], cancellationToken);
        var billNo = a.BillId is Guid billId && billNos.TryGetValue(billId, out var no) ? no : null;
        return ListAccountsPayableQueryHandler.MapAp(a, asOf) with { BillNo = billNo };
    }
}

public sealed class ListAccountsReceivableQueryHandler
    : IRequestHandler<ListAccountsReceivableQuery, IReadOnlyList<AccountsReceivableDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public ListAccountsReceivableQueryHandler(
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

    public async Task<IReadOnlyList<AccountsReceivableDto>> Handle(
        ListAccountsReceivableQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (scope, orgSubtree) = await DataScopeFilter.ResolveAsync(
            _permissions, _userContext, _db, _orgHierarchy,
            PermissionCodes.RevenueRead, "Bạn không có quyền xem khoản phải thu.", cancellationToken);

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.AccountsReceivable.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SettlementStatus))
        {
            var status = request.SettlementStatus.Trim().ToLowerInvariant();
            query = query.Where(a => a.SettlementStatus == status);
        }

        if (scope == DataScopes.Own)
        {
            if (!_userContext.HasUser)
            {
                return [];
            }

            query = query.Where(a => a.CreatedBy == _userContext.UserId);
        }
        else if (scope == DataScopes.Organization)
        {
            var billIds = await DataScopeFilter.BillIdsInOrgSubtreeAsync(_db, orgSubtree, cancellationToken);
            if (billIds.Count == 0)
            {
                return [];
            }

            query = query.Where(a => a.BillId != null && billIds.Contains(a.BillId.Value));
        }

        var rows = await query.OrderByDescending(a => a.Id).ToListAsync(cancellationToken);
        var billNos = await DataScopeFilter.LoadBillNosAsync(_db, rows.Select(a => a.BillId), cancellationToken);
        return rows.Select(a => MapAr(a, asOf) with
        {
            BillNo = a.BillId is Guid billId && billNos.TryGetValue(billId, out var no) ? no : null
        }).ToList();
    }

    internal static AccountsReceivableDto MapAr(Domain.Entities.AccountsReceivable a, DateOnly asOf)
    {
        var (days, bucket) = AgingBuckets.Classify(a.DueDate, asOf);
        return new(
            a.Id,
            a.ReceivableExposureId,
            a.RecognizedAmount,
            a.AdjustmentAmount,
            a.FinalizedSettledAmount,
            a.DeriveOutstanding(),
            a.CurrencyCode,
            a.DueDate,
            a.SettlementStatus,
            a.BillId,
            a.CounterpartyId,
            a.RecognizedAt,
            a.Notes,
            a.RecordStatus,
            days,
            bucket,
            a.RowVersion);
    }
}

public sealed class GetAccountsReceivableByIdQueryHandler
    : IRequestHandler<GetAccountsReceivableByIdQuery, AccountsReceivableDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public GetAccountsReceivableByIdQueryHandler(
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

    public async Task<AccountsReceivableDto> Handle(
        GetAccountsReceivableByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (scope, orgSubtree) = await DataScopeFilter.ResolveAsync(
            _permissions, _userContext, _db, _orgHierarchy,
            PermissionCodes.RevenueRead, "Bạn không có quyền xem khoản phải thu.", cancellationToken);

        var a = await _db.AccountsReceivable.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");

        var billOrgId = await DataScopeFilter.BillOrganizationIdAsync(_db, a.BillId, cancellationToken);
        if (!DataScopeFilter.AllowsViaBillOrg(
                scope, _userContext.UserId, orgSubtree, a.CreatedBy, billOrgId))
        {
            throw new NotFoundAppException("Không tìm thấy khoản phải thu.");
        }

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var billNos = await DataScopeFilter.LoadBillNosAsync(_db, [a.BillId], cancellationToken);
        var billNo = a.BillId is Guid billId && billNos.TryGetValue(billId, out var no) ? no : null;
        return ListAccountsReceivableQueryHandler.MapAr(a, asOf) with { BillNo = billNo };
    }
}

public sealed class GetAccountsPayableAgingQueryHandler
    : IRequestHandler<GetAccountsPayableAgingQuery, ApArAgingReportDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly ITenantSettingsService _settings;

    public GetAccountsPayableAgingQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        ITenantSettingsService settings)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _settings = settings;
    }

    public async Task<ApArAgingReportDto> Handle(
        GetAccountsPayableAgingQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.CostRead,
            "Bạn không có quyền xem tuổi nợ phải trả.",
            cancellationToken);

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var bounds = AgingBuckets.ResolveBounds(await _settings.GetFinancialAsync(cancellationToken));
        var query = _db.AccountsPayable.AsNoTracking().AsQueryable();
        if (request.CounterpartyId.HasValue)
        {
            query = query.Where(a => a.CounterpartyId == request.CounterpartyId);
        }

        if (!string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            var cc = request.CurrencyCode.Trim().ToUpperInvariant();
            query = query.Where(a => a.CurrencyCode == cc);
        }

        var rows = await query.OrderByDescending(a => a.Id).ToListAsync(cancellationToken);
        var billNos = await DataScopeFilter.LoadBillNosAsync(_db, rows.Select(a => a.BillId), cancellationToken);
        var settled = await AsOfOutstanding.SettledByPayableAsync(
            _db, rows.Select(r => r.Id).ToList(), asOf, cancellationToken);
        var items = rows
            .Select(a =>
            {
                var settledAmt = settled.TryGetValue(a.Id, out var s) ? s : 0m;
                var outstanding = AsOfOutstanding.Outstanding(a.RecognizedAmount, a.AdjustmentAmount, settledAmt);
                var (days, bucket) = AgingBuckets.Classify(a.DueDate, asOf, bounds);
                return new AccountsPayableDto(
                    a.Id,
                    a.PayableExposureId,
                    a.RecognizedAmount,
                    a.AdjustmentAmount,
                    settledAmt,
                    outstanding,
                    a.CurrencyCode,
                    a.DueDate,
                    a.SettlementStatus,
                    a.BillId,
                    a.CounterpartyId,
                    a.RecognizedAt,
                    a.Notes,
                    a.RecordStatus,
                    days,
                    bucket,
                    BillNo: a.BillId is Guid billId && billNos.TryGetValue(billId, out var no) ? no : null);
            })
            .Where(a => request.IncludeSettled || a.Outstanding > 0m)
            .ToList();

        var buckets = AgingBuckets.Ordered
            .Select(code =>
            {
                var inBucket = items.Where(i => i.AgingBucket == code).ToList();
                return new AgingBucketSummaryDto(
                    code,
                    inBucket.Count,
                    inBucket.Sum(i => i.Outstanding));
            })
            .ToList();

        return new ApArAgingReportDto(asOf, buckets, items, null);
    }
}

public sealed class GetAccountsReceivableAgingQueryHandler
    : IRequestHandler<GetAccountsReceivableAgingQuery, ApArAgingReportDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly ITenantSettingsService _settings;

    public GetAccountsReceivableAgingQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        ITenantSettingsService settings)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _settings = settings;
    }

    public async Task<ApArAgingReportDto> Handle(
        GetAccountsReceivableAgingQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RevenueRead,
            "Bạn không có quyền xem tuổi nợ phải thu.",
            cancellationToken);

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var bounds = AgingBuckets.ResolveBounds(await _settings.GetFinancialAsync(cancellationToken));
        var query = _db.AccountsReceivable.AsNoTracking().AsQueryable();
        if (request.CounterpartyId.HasValue)
        {
            query = query.Where(a => a.CounterpartyId == request.CounterpartyId);
        }

        if (!string.IsNullOrWhiteSpace(request.CurrencyCode))
        {
            var cc = request.CurrencyCode.Trim().ToUpperInvariant();
            query = query.Where(a => a.CurrencyCode == cc);
        }

        var rows = await query.OrderByDescending(a => a.Id).ToListAsync(cancellationToken);
        var billNos = await DataScopeFilter.LoadBillNosAsync(_db, rows.Select(a => a.BillId), cancellationToken);
        var settled = await AsOfOutstanding.SettledByReceivableAsync(
            _db, rows.Select(r => r.Id).ToList(), asOf, cancellationToken);
        var items = rows
            .Select(a =>
            {
                var settledAmt = settled.TryGetValue(a.Id, out var s) ? s : 0m;
                var outstanding = AsOfOutstanding.Outstanding(a.RecognizedAmount, a.AdjustmentAmount, settledAmt);
                var (days, bucket) = AgingBuckets.Classify(a.DueDate, asOf, bounds);
                return new AccountsReceivableDto(
                    a.Id,
                    a.ReceivableExposureId,
                    a.RecognizedAmount,
                    a.AdjustmentAmount,
                    settledAmt,
                    outstanding,
                    a.CurrencyCode,
                    a.DueDate,
                    a.SettlementStatus,
                    a.BillId,
                    a.CounterpartyId,
                    a.RecognizedAt,
                    a.Notes,
                    a.RecordStatus,
                    days,
                    bucket,
                    BillNo: a.BillId is Guid billId && billNos.TryGetValue(billId, out var no) ? no : null);
            })
            .Where(a => request.IncludeSettled || a.Outstanding > 0m)
            .ToList();

        var buckets = AgingBuckets.Ordered
            .Select(code =>
            {
                var inBucket = items.Where(i => i.AgingBucket == code).ToList();
                return new AgingBucketSummaryDto(
                    code,
                    inBucket.Count,
                    inBucket.Sum(i => i.Outstanding));
            })
            .ToList();

        return new ApArAgingReportDto(asOf, buckets, null, items);
    }
}
