using LCMS.Application.Abstractions;
using LCMS.Application.Approvals.Queries;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Exceptions.Queries;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Queues.Queries;

/// <summary>
/// Control queue: open exceptions (status=open). Optional severity filter.
/// </summary>
public sealed record ListOpenExceptionQueueQuery(string? Severity = null)
    : IRequest<IReadOnlyList<ExceptionDto>>;

/// <summary>
/// Control queue: pending approvals only.
/// </summary>
public sealed record ListPendingApprovalQueueQuery(string? ObjectType = null)
    : IRequest<IReadOnlyList<ApprovalDto>>;

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

        var query = _db.Exceptions.AsNoTracking()
            .Where(e =>
                e.Status == ExceptionStatuses.Open
                || e.Status == ExceptionStatuses.InProgress
                || e.Status == ExceptionStatuses.Escalated);

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            var severity = request.Severity.Trim().ToLowerInvariant();
            query = query.Where(e => e.Severity == severity);
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

        var query = _db.Approvals.AsNoTracking()
            .Where(a => a.Status == ApprovalStatuses.Pending);

        if (!string.IsNullOrWhiteSpace(request.ObjectType))
        {
            var objectType = request.ObjectType.Trim().ToLowerInvariant();
            query = query.Where(a => a.ObjectType == objectType);
        }

        var list = await query.ToListAsync(cancellationToken);

        return list
            .OrderBy(a => a.RequestedAt)
            .ThenByDescending(a => a.Id)
            .Select(ListApprovalsQueryHandler.Map)
            .ToList();
    }
}
