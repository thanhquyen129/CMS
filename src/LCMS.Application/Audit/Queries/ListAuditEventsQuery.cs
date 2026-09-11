using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Audit.Queries;

public sealed record AuditEventDto(
    Guid Id,
    Guid? ActorId,
    string Action,
    string ObjectType,
    Guid ObjectId,
    string? BeforeJson,
    string? AfterJson,
    string? Reason,
    string? CorrelationId,
    DateTimeOffset OccurredAt);

public sealed record ListAuditEventsQuery(
    string? ObjectType = null,
    Guid? ObjectId = null,
    string? Action = null,
    string? CorrelationId = null,
    int Take = 100) : IRequest<IReadOnlyList<AuditEventDto>>;

public sealed class ListAuditEventsQueryHandler
    : IRequestHandler<ListAuditEventsQuery, IReadOnlyList<AuditEventDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListAuditEventsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<AuditEventDto>> Handle(
        ListAuditEventsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var take = Math.Clamp(request.Take, 1, 500);
        var query = _db.AuditEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ObjectType))
        {
            var objectType = request.ObjectType.Trim();
            query = query.Where(e => e.ObjectType == objectType);
        }

        if (request.ObjectId.HasValue)
        {
            query = query.Where(e => e.ObjectId == request.ObjectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(e => e.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            var correlationId = request.CorrelationId.Trim();
            query = query.Where(e => e.CorrelationId == correlationId);
        }

        return await query
            // Order by Id (UUIDv7 time-sortable) — SQLite rejects DateTimeOffset in ORDER BY.
            .OrderByDescending(e => e.Id)
            .Take(take)
            .Select(e => new AuditEventDto(
                e.Id,
                e.ActorId,
                e.Action,
                e.ObjectType,
                e.ObjectId,
                e.BeforeJson,
                e.AfterJson,
                e.Reason,
                e.CorrelationId,
                e.OccurredAt))
            .ToListAsync(cancellationToken);
    }
}
