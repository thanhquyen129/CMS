using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Audit.Queries;

public sealed record AuditEventDto(
    Guid Id,
    Guid? ActorId,
    string? ActorDisplayName,
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
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Skip = 0,
    int Take = 100) : IRequest<IReadOnlyList<AuditEventDto>>;

public sealed class ListAuditEventsQueryHandler
    : IRequestHandler<ListAuditEventsQuery, IReadOnlyList<AuditEventDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ListAuditEventsQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<AuditEventDto>> Handle(
        ListAuditEventsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.AuditRead,
            "Bạn không có quyền xem nhật ký hệ thống.",
            cancellationToken);

        var take = Math.Clamp(request.Take, 1, 500);
        var skip = Math.Max(0, request.Skip);
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

        var isNpgsql = _db is DbContext ef
            && ef.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

        List<AuditEventDto> rows;
        if (isNpgsql)
        {
            if (request.From.HasValue)
            {
                var from = request.From.Value;
                query = query.Where(e => e.OccurredAt >= from);
            }

            if (request.To.HasValue)
            {
                var to = request.To.Value;
                query = query.Where(e => e.OccurredAt <= to);
            }

            rows = await query
                .OrderByDescending(e => e.OccurredAt)
                .ThenByDescending(e => e.Id)
                .Skip(skip)
                .Take(take)
                .Select(e => new AuditEventDto(
                    e.Id,
                    e.ActorId,
                    null,
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
        else
        {
            var hasDateFilter = request.From.HasValue || request.To.HasValue;
            var fetchTake = hasDateFilter ? Math.Min(2000, Math.Max((take + skip) * 20, 200)) : take + skip;

            var fetched = await query
                .OrderByDescending(e => e.Id)
                .Take(fetchTake)
                .Select(e => new AuditEventDto(
                    e.Id,
                    e.ActorId,
                    null,
                    e.Action,
                    e.ObjectType,
                    e.ObjectId,
                    e.BeforeJson,
                    e.AfterJson,
                    e.Reason,
                    e.CorrelationId,
                    e.OccurredAt))
                .ToListAsync(cancellationToken);

            IEnumerable<AuditEventDto> filtered = fetched;
            if (request.From.HasValue)
            {
                var from = request.From.Value;
                filtered = filtered.Where(e => e.OccurredAt >= from);
            }

            if (request.To.HasValue)
            {
                var to = request.To.Value;
                filtered = filtered.Where(e => e.OccurredAt <= to);
            }

            rows = filtered.Skip(skip).Take(take).ToList();
        }

        var actorIds = rows.Where(r => r.ActorId.HasValue).Select(r => r.ActorId!.Value).Distinct().ToList();
        if (actorIds.Count == 0)
        {
            return rows;
        }

        var names = await _db.Users.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, cancellationToken);

        return rows.Select(r => r with
        {
            ActorDisplayName = r.ActorId is Guid id && names.TryGetValue(id, out var n) ? n : null
        }).ToList();
    }
}
