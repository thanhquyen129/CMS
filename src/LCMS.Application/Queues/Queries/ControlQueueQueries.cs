using LCMS.Application.Abstractions;
using LCMS.Application.Approvals.Queries;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Exceptions.Queries;
using LCMS.Domain.Entities;
using LCMS.Domain.Terminology;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Queues.Queries;

/// <summary>
/// Control queue: exceptions with status/severity/overdue/objectType filters.
/// Default status set = open | in_progress | escalated when status omitted.
/// </summary>
public sealed record ListOpenExceptionQueueQuery(
    string? Status = null,
    string? Severity = null,
    bool? OverdueOnly = null,
    string? ObjectType = null) : IRequest<IReadOnlyList<ExceptionDto>>;

/// <summary>
/// Control queue: approvals with status/objectType/requiredLevel filters.
/// Default status = pending when status omitted.
/// </summary>
public sealed record ListPendingApprovalQueueQuery(
    string? Status = null,
    string? ObjectType = null,
    int? RequiredLevel = null) : IRequest<IReadOnlyList<ApprovalDto>>;

/// <summary>
/// Optional stub: open reconciliations (draft | in_progress by default).
/// </summary>
public sealed record ListOpenReconciliationQueueQuery(string? Status = null)
    : IRequest<IReadOnlyList<ReconciliationQueueItemDto>>;

public sealed record ReconciliationQueueItemDto(
    Guid Id,
    string ReconciliationType,
    string? RuleCode,
    int VersionNo,
    string Status,
    Guid? BillId,
    string? Notes,
    DateTimeOffset? StartedAt,
    string QueueLabel);

public sealed class ListOpenExceptionQueueQueryHandler
    : IRequestHandler<ListOpenExceptionQueueQuery, IReadOnlyList<ExceptionDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListOpenExceptionQueueQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ExceptionDto>> Handle(
        ListOpenExceptionQueueQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.Exceptions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(e => e.Status == status);
        }
        else
        {
            query = query.Where(e =>
                e.Status == ExceptionStatuses.Open
                || e.Status == ExceptionStatuses.InProgress
                || e.Status == ExceptionStatuses.Escalated);
        }

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            var severity = request.Severity.Trim().ToLowerInvariant();
            query = query.Where(e => e.Severity == severity);
        }

        if (!string.IsNullOrWhiteSpace(request.ObjectType))
        {
            var objectType = request.ObjectType.Trim().ToLowerInvariant();
            query = query.Where(e => e.ObjectType == objectType);
        }

        if (request.OverdueOnly == true)
        {
            var now = DateTimeOffset.UtcNow;
            // SQLite cannot reliably translate DateTimeOffset comparisons — filter in memory.
            var materialized = await query.ToListAsync(cancellationToken);
            return materialized
                .Where(e => e.DueAt != null && e.DueAt < now)
                .OrderBy(e => e.DueAt ?? DateTimeOffset.MaxValue)
                .ThenByDescending(e => e.Id)
                .Select(ListExceptionsQueryHandler.Map)
                .ToList();
        }

        var list = await query.ToListAsync(cancellationToken);

        return list
            .OrderBy(e => e.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(e => e.Id)
            .Select(ListExceptionsQueryHandler.Map)
            .ToList();
    }
}

public sealed class ListPendingApprovalQueueQueryHandler
    : IRequestHandler<ListPendingApprovalQueueQuery, IReadOnlyList<ApprovalDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListPendingApprovalQueueQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ApprovalDto>> Handle(
        ListPendingApprovalQueueQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.Approvals.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(a => a.Status == status);
        }
        else
        {
            query = query.Where(a => a.Status == ApprovalStatuses.Pending);
        }

        if (!string.IsNullOrWhiteSpace(request.ObjectType))
        {
            var objectType = request.ObjectType.Trim().ToLowerInvariant();
            query = query.Where(a => a.ObjectType == objectType);
        }

        if (request.RequiredLevel.HasValue)
        {
            var level = request.RequiredLevel.Value;
            query = query.Where(a => a.RequiredLevel == level);
        }

        var list = await query
            .OrderBy(a => a.RequestedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        return await EnrichAsync(list.Select(ListApprovalsQueryHandler.Map).ToList(), cancellationToken);
    }

    private async Task<IReadOnlyList<ApprovalDto>> EnrichAsync(
        List<ApprovalDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var userIds = items.Where(i => i.RequestedBy.HasValue).Select(i => i.RequestedBy!.Value).Distinct().ToList();
        var names = userIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        var docIds = Ids(items, ApprovalObjectTypes.Document, "financial_document");
        var docs = await _db.FinancialDocuments.AsNoTracking()
            .Where(d => docIds.Contains(d.Id))
            .Select(d => new { d.Id, d.DocumentNo, d.TotalAmount, d.CurrencyCode })
            .ToListAsync(cancellationToken);

        var billIds = Ids(items, "bill");
        var bills = await _db.Bills.AsNoTracking()
            .Where(b => billIds.Contains(b.Id))
            .Select(b => new { b.Id, b.BillNo })
            .ToListAsync(cancellationToken);

        var allocIds = Ids(items, ApprovalObjectTypes.CostAllocation);
        var allocs = await (
            from a in _db.CostAllocations.AsNoTracking()
            join c in _db.Costs.AsNoTracking() on a.CostId equals c.Id
            where allocIds.Contains(a.Id)
            select new
            {
                a.Id,
                a.CostId,
                a.VersionNo,
                a.AllocatableAmount,
                c.CurrencyCode,
                c.CostTypeCode
            }).ToListAsync(cancellationToken);

        var payIds = Ids(items, ApprovalObjectTypes.Payment);
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => payIds.Contains(p.Id))
            .Select(p => new { p.Id, p.ReferenceNo, p.Amount, p.CurrencyCode })
            .ToListAsync(cancellationToken);

        var colIds = Ids(items, ApprovalObjectTypes.Collection);
        var collections = await _db.Collections.AsNoTracking()
            .Where(c => colIds.Contains(c.Id))
            .Select(c => new { c.Id, c.ReferenceNo, c.Amount, c.CurrencyCode })
            .ToListAsync(cancellationToken);

        return items.Select(item =>
        {
            var type = item.ObjectType.ToLowerInvariant();
            string? code = null;
            decimal? amount = null;
            string? currency = null;
            string? path = null;

            var doc = docs.FirstOrDefault(d => d.Id == item.ObjectId);
            if (doc is not null && (type is "document" or "financial_document"))
            {
                code = doc.DocumentNo;
                amount = doc.TotalAmount;
                currency = doc.CurrencyCode;
                path = $"/documents/{doc.Id}";
            }

            var bill = bills.FirstOrDefault(b => b.Id == item.ObjectId);
            if (bill is not null && type == "bill")
            {
                code = bill.BillNo;
                path = $"/bills/{bill.Id}";
            }

            var alloc = allocs.FirstOrDefault(a => a.Id == item.ObjectId);
            if (alloc is not null && type == ApprovalObjectTypes.CostAllocation)
            {
                code = string.IsNullOrWhiteSpace(alloc.CostTypeCode)
                    ? $"Phân bổ v{alloc.VersionNo}"
                    : $"{alloc.CostTypeCode} · v{alloc.VersionNo}";
                amount = alloc.AllocatableAmount;
                currency = alloc.CurrencyCode;
                path = $"/costs/shared/{alloc.CostId}";
            }

            var payment = payments.FirstOrDefault(p => p.Id == item.ObjectId);
            if (payment is not null && type == ApprovalObjectTypes.Payment)
            {
                code = string.IsNullOrWhiteSpace(payment.ReferenceNo) ? "Thanh toán" : payment.ReferenceNo;
                amount = payment.Amount;
                currency = payment.CurrencyCode;
                path = $"/settlements/payments/{payment.Id}";
            }

            var collection = collections.FirstOrDefault(c => c.Id == item.ObjectId);
            if (collection is not null && type == ApprovalObjectTypes.Collection)
            {
                code = string.IsNullOrWhiteSpace(collection.ReferenceNo) ? "Thu tiền" : collection.ReferenceNo;
                amount = collection.Amount;
                currency = collection.CurrencyCode;
                path = $"/settlements/collections/{collection.Id}";
            }

            names.TryGetValue(item.RequestedBy ?? Guid.Empty, out var name);
            return item with
            {
                BusinessCode = code,
                Amount = amount,
                CurrencyCode = currency,
                RequestedByName = string.IsNullOrWhiteSpace(name) ? null : name,
                DetailPath = path
            };
        }).ToList();
    }

    private static List<Guid> Ids(IEnumerable<ApprovalDto> items, params string[] types)
    {
        var set = new HashSet<string>(types, StringComparer.OrdinalIgnoreCase);
        return items.Where(i => set.Contains(i.ObjectType)).Select(i => i.ObjectId).Distinct().ToList();
    }
}

public sealed class ListOpenReconciliationQueueQueryHandler
    : IRequestHandler<ListOpenReconciliationQueueQuery, IReadOnlyList<ReconciliationQueueItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListOpenReconciliationQueueQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ReconciliationQueueItemDto>> Handle(
        ListOpenReconciliationQueueQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.Reconciliations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(r => r.Status == status);
        }
        else
        {
            query = query.Where(r =>
                r.Status == ReconciliationStatuses.Draft
                || r.Status == ReconciliationStatuses.InProgress);
        }

        var list = await query.ToListAsync(cancellationToken);
        var label = VietnameseUiTerms.Get("RECONCILIATION");
        return list
            .OrderByDescending(r => r.StartedAt ?? DateTimeOffset.MinValue)
            .ThenByDescending(r => r.Id)
            .Select(r => new ReconciliationQueueItemDto(
                r.Id,
                r.ReconciliationType,
                r.RuleCode,
                r.VersionNo,
                r.Status,
                r.BillId,
                r.Notes,
                r.StartedAt,
                label))
            .ToList();
    }
}
