using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exposures.Queries;

/// <summary>
/// AP/AR DTOs always expose Outstanding as a derived field (C-015).
/// There is no write path that accepts outstanding as source of truth.
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
    string RecordStatus);

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
    string RecordStatus);

public sealed record ListAccountsPayableQuery(string? SettlementStatus)
    : IRequest<IReadOnlyList<AccountsPayableDto>>;

public sealed record GetAccountsPayableByIdQuery(Guid Id) : IRequest<AccountsPayableDto>;

public sealed record ListAccountsReceivableQuery(string? SettlementStatus)
    : IRequest<IReadOnlyList<AccountsReceivableDto>>;

public sealed record GetAccountsReceivableByIdQuery(Guid Id) : IRequest<AccountsReceivableDto>;

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

        var query = _db.AccountsPayable.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SettlementStatus))
        {
            var status = request.SettlementStatus.Trim().ToLowerInvariant();
            query = query.Where(a => a.SettlementStatus == status);
        }

        // Order by Id (UUIDv7 time-sortable) — SQLite rejects DateTimeOffset in ORDER BY.
        var rows = await query.OrderByDescending(a => a.Id).ToListAsync(cancellationToken);
        return rows.Select(MapAp).ToList();
    }

    private static AccountsPayableDto MapAp(Domain.Entities.AccountsPayable a) =>
        new(
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
            a.RecordStatus);
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

        return new AccountsPayableDto(
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
            a.RecordStatus);
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

        var query = _db.AccountsReceivable.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.SettlementStatus))
        {
            var status = request.SettlementStatus.Trim().ToLowerInvariant();
            query = query.Where(a => a.SettlementStatus == status);
        }

        // Order by Id (UUIDv7 time-sortable) — SQLite rejects DateTimeOffset in ORDER BY.
        var rows = await query.OrderByDescending(a => a.Id).ToListAsync(cancellationToken);
        return rows.Select(a => new AccountsReceivableDto(
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
            a.RecordStatus)).ToList();
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

        return new AccountsReceivableDto(
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
            a.RecordStatus);
    }
}
