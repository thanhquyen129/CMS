using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Reports.Queries;

public sealed record CashSettlementReportRow(
    Guid Id,
    string Kind,
    decimal Amount,
    decimal AllocatedAmount,
    decimal UnappliedAmount,
    string CurrencyCode,
    DateOnly ValueDate,
    Guid? BillId,
    string Status);

public sealed record CashSettlementReportDto(
    DateOnly? AsOf,
    IReadOnlyList<CashSettlementReportRow> Items,
    decimal UnappliedTotal);

/// <summary>Cash and settlement report: payments/collections with unapplied cash at optional as-of.</summary>
public sealed record GetCashSettlementReportQuery(DateOnly? AsOf = null)
    : IRequest<CashSettlementReportDto>;

public sealed class GetCashSettlementReportQueryHandler
    : IRequestHandler<GetCashSettlementReportQuery, CashSettlementReportDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public GetCashSettlementReportQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<CashSettlementReportDto> Handle(
        GetCashSettlementReportQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var asOf = request.AsOf;
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.RecordStatus == "active" && p.Status != PaymentStatuses.Cancelled)
            .ToListAsync(cancellationToken);
        var collections = await _db.Collections.AsNoTracking()
            .Where(c => c.RecordStatus == "active" && c.Status != CollectionStatuses.Cancelled)
            .ToListAsync(cancellationToken);

        if (asOf.HasValue)
        {
            payments = payments.Where(p => p.ValueDate <= asOf.Value).ToList();
            collections = collections.Where(c => c.ValueDate <= asOf.Value).ToList();
        }

        var paymentIds = payments.Select(p => p.Id).ToList();
        var collectionIds = collections.Select(c => c.Id).ToList();
        var payAlloc = await _db.PaymentAllocations.AsNoTracking()
            .Where(a => paymentIds.Contains(a.PaymentId))
            .ToListAsync(cancellationToken);
        var collAlloc = await _db.CollectionAllocations.AsNoTracking()
            .Where(a => collectionIds.Contains(a.CollectionId))
            .ToListAsync(cancellationToken);

        if (asOf.HasValue)
        {
            payAlloc = payAlloc
                .Where(a => CountsAtAsOf(a.AllocationStatus, a.FinalizedAt, a.ReversedAt, a.CreatedAt, asOf.Value))
                .ToList();
            collAlloc = collAlloc
                .Where(a => CountsAtAsOf(a.AllocationStatus, a.FinalizedAt, a.ReversedAt, a.CreatedAt, asOf.Value))
                .ToList();
        }
        else
        {
            payAlloc = payAlloc
                .Where(a => a.AllocationStatus is SettlementAllocationStatuses.Draft
                    or SettlementAllocationStatuses.Finalized)
                .ToList();
            collAlloc = collAlloc
                .Where(a => a.AllocationStatus is SettlementAllocationStatuses.Draft
                    or SettlementAllocationStatuses.Finalized)
                .ToList();
        }

        var payMap = payAlloc.GroupBy(a => a.PaymentId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var collMap = collAlloc.GroupBy(a => a.CollectionId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var rows = new List<CashSettlementReportRow>();
        foreach (var p in payments.OrderByDescending(x => x.ValueDate).ThenByDescending(x => x.Id))
        {
            var allocated = payMap.TryGetValue(p.Id, out var s) ? s : 0m;
            rows.Add(new CashSettlementReportRow(
                p.Id,
                "payment",
                p.Amount,
                allocated,
                p.Amount - allocated,
                p.CurrencyCode,
                p.ValueDate,
                p.BillId,
                p.Status));
        }

        foreach (var c in collections.OrderByDescending(x => x.ValueDate).ThenByDescending(x => x.Id))
        {
            var allocated = collMap.TryGetValue(c.Id, out var s) ? s : 0m;
            rows.Add(new CashSettlementReportRow(
                c.Id,
                "collection",
                c.Amount,
                allocated,
                c.Amount - allocated,
                c.CurrencyCode,
                c.ValueDate,
                c.BillId,
                c.Status));
        }

        return new CashSettlementReportDto(asOf, rows, rows.Sum(r => r.UnappliedAmount));
    }

    private static bool CountsAtAsOf(
        string status,
        DateTimeOffset? finalizedAt,
        DateTimeOffset? reversedAt,
        DateTimeOffset createdAt,
        DateOnly asOf)
    {
        if (status == SettlementAllocationStatuses.Finalized)
        {
            if (!finalizedAt.HasValue || DateOnly.FromDateTime(finalizedAt.Value.UtcDateTime) > asOf)
            {
                return false;
            }

            if (reversedAt.HasValue && DateOnly.FromDateTime(reversedAt.Value.UtcDateTime) <= asOf)
            {
                return false;
            }

            return true;
        }

        if (status == SettlementAllocationStatuses.Draft)
        {
            return DateOnly.FromDateTime(createdAt.UtcDateTime) <= asOf;
        }

        return false;
    }
}
