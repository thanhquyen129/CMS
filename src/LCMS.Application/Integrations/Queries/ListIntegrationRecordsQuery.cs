using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Integrations.Queries;

public sealed record IntegrationRecordDto(
    Guid Id,
    string SourceSystem,
    string ExternalObjectType,
    string ExternalId,
    string? ExternalVersion,
    string Status,
    string? LocalObjectType,
    Guid? LocalObjectId,
    string? PayloadHash,
    string? Notes,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProcessedAt,
    int ErrorCount);

public sealed record ListIntegrationRecordsQuery(
    string? SourceSystem = null,
    string? ExternalObjectType = null,
    string? Status = null,
    int Take = 100) : IRequest<IReadOnlyList<IntegrationRecordDto>>;

public sealed class ListIntegrationRecordsQueryHandler
    : IRequestHandler<ListIntegrationRecordsQuery, IReadOnlyList<IntegrationRecordDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListIntegrationRecordsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<IntegrationRecordDto>> Handle(
        ListIntegrationRecordsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var take = Math.Clamp(request.Take, 1, 500);
        var query = _db.IntegrationRecords.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SourceSystem))
        {
            var source = request.SourceSystem.Trim();
            query = query.Where(r => r.SourceSystem == source);
        }

        if (!string.IsNullOrWhiteSpace(request.ExternalObjectType))
        {
            var type = request.ExternalObjectType.Trim().ToLowerInvariant();
            query = query.Where(r => r.ExternalObjectType == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(r => r.Status == status);
        }

        var rows = await query
            // Order by Id (UUIDv7 time-sortable) — SQLite rejects DateTimeOffset in ORDER BY.
            .OrderByDescending(r => r.Id)
            .Take(take)
            .Select(r => new
            {
                r.Id,
                r.SourceSystem,
                r.ExternalObjectType,
                r.ExternalId,
                r.ExternalVersion,
                r.Status,
                r.LocalObjectType,
                r.LocalObjectId,
                r.PayloadHash,
                r.Notes,
                r.ReceivedAt,
                r.ProcessedAt
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();
        var errorRows = await _db.IntegrationErrors.AsNoTracking()
            .Where(e => ids.Contains(e.IntegrationRecordId))
            .Select(e => e.IntegrationRecordId)
            .ToListAsync(cancellationToken);
        var errorCounts = errorRows
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        return rows.Select(r => new IntegrationRecordDto(
            r.Id,
            r.SourceSystem,
            r.ExternalObjectType,
            r.ExternalId,
            r.ExternalVersion,
            r.Status,
            r.LocalObjectType,
            r.LocalObjectId,
            r.PayloadHash,
            r.Notes,
            r.ReceivedAt,
            r.ProcessedAt,
            errorCounts.GetValueOrDefault(r.Id))).ToList();
    }
}
