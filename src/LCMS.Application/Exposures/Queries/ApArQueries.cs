using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
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
    string AgingBucket);

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
    string AgingBucket);

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

    public ListAccountsPayableQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<AccountsPayableDto>> Handle(
        ListAccountsPayableQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.AccountsPayable.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SettlementStatus))
        {
            var status = request.SettlementStatus.Trim().ToLowerInvariant();
            query = query.Where(a => a.SettlementStatus == status);
        }

        // Order by Id (UUIDv7 time-sortable) — SQLite rejects DateTimeOffset in ORDER BY.
        var rows = await query.OrderByDescending(a => a.Id).ToListAsync(cancellationToken);
        return rows.Select(a => MapAp(a, asOf)).ToList();
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
            bucket);
    }
}

public sealed class GetAccountsPayableByIdQueryHandler
    : IRequestHandler<GetAccountsPayableByIdQuery, AccountsPayableDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetAccountsPayableByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<AccountsPayableDto> Handle(
        GetAccountsPayableByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var a = await _db.AccountsPayable.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải trả.");

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return ListAccountsPayableQueryHandler.MapAp(a, asOf);
    }
}

public sealed class ListAccountsReceivableQueryHandler
    : IRequestHandler<ListAccountsReceivableQuery, IReadOnlyList<AccountsReceivableDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListAccountsReceivableQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<AccountsReceivableDto>> Handle(
        ListAccountsReceivableQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.AccountsReceivable.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SettlementStatus))
        {
            var status = request.SettlementStatus.Trim().ToLowerInvariant();
            query = query.Where(a => a.SettlementStatus == status);
        }

        var rows = await query.OrderByDescending(a => a.Id).ToListAsync(cancellationToken);
        return rows.Select(a => MapAr(a, asOf)).ToList();
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
            bucket);
    }
}

public sealed class GetAccountsReceivableByIdQueryHandler
    : IRequestHandler<GetAccountsReceivableByIdQuery, AccountsReceivableDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetAccountsReceivableByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<AccountsReceivableDto> Handle(
        GetAccountsReceivableByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var a = await _db.AccountsReceivable.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy khoản phải thu.");

        var asOf = request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return ListAccountsReceivableQueryHandler.MapAr(a, asOf);
    }
}

public sealed class GetAccountsPayableAgingQueryHandler
    : IRequestHandler<GetAccountsPayableAgingQuery, ApArAgingReportDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public GetAccountsPayableAgingQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
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
        var items = rows
            .Select(a => ListAccountsPayableQueryHandler.MapAp(a, asOf))
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

    public GetAccountsReceivableAgingQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
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
        var items = rows
            .Select(a => ListAccountsReceivableQueryHandler.MapAr(a, asOf))
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
