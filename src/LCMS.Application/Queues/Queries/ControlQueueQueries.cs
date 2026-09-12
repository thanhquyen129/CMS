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

        var list = await query.ToListAsync(cancellationToken);

        return list
            .OrderBy(a => a.RequestedAt)
            .ThenByDescending(a => a.Id)
            .Select(ListApprovalsQueryHandler.Map)
            .ToList();
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
