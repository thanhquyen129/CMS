using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialCloses.Queries;

public sealed record FinancialCloseSnapshotDetailDto(
    Guid Id,
    Guid SnapshotId,
    int LineNo,
    string MetricKey,
    decimal MetricValue,
    string? CurrencyCode,
    string? SourceType,
    Guid? SourceId,
    string? Notes);

public sealed record FinancialCloseSnapshotDto(
    Guid Id,
    Guid FinancialCloseId,
    string ScopeType,
    Guid? ScopeId,
    int SnapshotVersion,
    DateTimeOffset ClosedAt,
    Guid? ClosedBy,
    string PolicyVersion,
    string BaseCurrency,
    string ImmutableHash,
    IReadOnlyList<FinancialCloseSnapshotDetailDto> Details);

public sealed record FinancialCloseDto(
    Guid Id,
    string ScopeType,
    Guid? ScopeId,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    int VersionNo,
    string Status,
    string PolicyVersion,
    string BaseCurrency,
    string? Notes,
    DateTimeOffset? StartedAt,
    Guid? StartedBy,
    DateTimeOffset? LockedAt,
    Guid? LockedBy,
    DateTimeOffset? ReopenedAt,
    Guid? ReopenedBy,
    string? ReopenReason,
    Guid? SupersedesCloseId,
    IReadOnlyList<FinancialCloseSnapshotDto> Snapshots,
    byte[]? RowVersion = null);

public sealed record ListFinancialClosesQuery(string? Status, string? ScopeType)
    : IRequest<IReadOnlyList<FinancialCloseDto>>;

public sealed record GetFinancialCloseByIdQuery(Guid Id) : IRequest<FinancialCloseDto>;

public sealed record ListFinancialCloseSnapshotsQuery(Guid FinancialCloseId)
    : IRequest<IReadOnlyList<FinancialCloseSnapshotDto>>;

public sealed record GetFinancialCloseSnapshotByIdQuery(Guid Id) : IRequest<FinancialCloseSnapshotDto>;

public sealed class ListFinancialClosesQueryHandler
    : IRequestHandler<ListFinancialClosesQuery, IReadOnlyList<FinancialCloseDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListFinancialClosesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<FinancialCloseDto>> Handle(
        ListFinancialClosesQuery request,
        CancellationToken cancellationToken)
    {
        EnsureTenant(_tenantContext);
        var query = _db.FinancialCloses.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.ScopeType))
        {
            var scope = request.ScopeType.Trim().ToLowerInvariant();
            query = query.Where(c => c.ScopeType == scope);
        }

        var closes = await query.OrderByDescending(c => c.Id).ToListAsync(cancellationToken);
        return await MapManyAsync(_db, closes, includeDetails: false, cancellationToken);
    }

    internal static void EnsureTenant(ITenantContext tenantContext)
    {
        if (!tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }
    }

    internal static async Task<IReadOnlyList<FinancialCloseDto>> MapManyAsync(
        ILcmsDbContext db,
        IReadOnlyList<FinancialClose> closes,
        bool includeDetails,
        CancellationToken cancellationToken)
    {
        var closeIds = closes.Select(c => c.Id).ToList();
        var snapshots = await db.FinancialCloseSnapshots.AsNoTracking()
            .Where(s => closeIds.Contains(s.FinancialCloseId))
            .OrderBy(s => s.SnapshotVersion)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, List<FinancialCloseSnapshotDetail>> detailsBySnapshot = new();
        if (includeDetails && snapshots.Count > 0)
        {
            var snapshotIds = snapshots.Select(s => s.Id).ToList();
            var details = await db.FinancialCloseSnapshotDetails.AsNoTracking()
                .Where(d => snapshotIds.Contains(d.SnapshotId))
                .OrderBy(d => d.LineNo)
                .ToListAsync(cancellationToken);
            detailsBySnapshot = details.GroupBy(d => d.SnapshotId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        return closes.Select(c => MapClose(
            c,
            snapshots.Where(s => s.FinancialCloseId == c.Id).ToList(),
            detailsBySnapshot)).ToList();
    }

    internal static FinancialCloseDto MapClose(
        FinancialClose c,
        IReadOnlyList<FinancialCloseSnapshot> snapshots,
        IReadOnlyDictionary<Guid, List<FinancialCloseSnapshotDetail>> detailsBySnapshot) =>
        new(
            c.Id,
            c.ScopeType,
            c.ScopeId,
            c.PeriodFrom,
            c.PeriodTo,
            c.VersionNo,
            c.Status,
            c.PolicyVersion,
            c.BaseCurrency,
            c.Notes,
            c.StartedAt,
            c.StartedBy,
            c.LockedAt,
            c.LockedBy,
            c.ReopenedAt,
            c.ReopenedBy,
            c.ReopenReason,
            c.SupersedesCloseId,
            snapshots.Select(s => MapSnapshot(
                s,
                detailsBySnapshot.TryGetValue(s.Id, out var d) ? d : [])).ToList(),
            c.RowVersion);

    internal static FinancialCloseSnapshotDto MapSnapshot(
        FinancialCloseSnapshot s,
        IReadOnlyList<FinancialCloseSnapshotDetail> details) =>
        new(
            s.Id,
            s.FinancialCloseId,
            s.ScopeType,
            s.ScopeId,
            s.SnapshotVersion,
            s.ClosedAt,
            s.ClosedBy,
            s.PolicyVersion,
            s.BaseCurrency,
            s.ImmutableHash,
            details.Select(d => new FinancialCloseSnapshotDetailDto(
                d.Id,
                d.SnapshotId,
                d.LineNo,
                d.MetricKey,
                d.MetricValue,
                d.CurrencyCode,
                d.SourceType,
                d.SourceId,
                d.Notes)).ToList());
}

public sealed class GetFinancialCloseByIdQueryHandler : IRequestHandler<GetFinancialCloseByIdQuery, FinancialCloseDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetFinancialCloseByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<FinancialCloseDto> Handle(GetFinancialCloseByIdQuery request, CancellationToken cancellationToken)
    {
        ListFinancialClosesQueryHandler.EnsureTenant(_tenantContext);
        var close = await _db.FinancialCloses.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lần chốt tài chính.");
        var mapped = await ListFinancialClosesQueryHandler.MapManyAsync(
            _db, [close], includeDetails: true, cancellationToken);
        return mapped[0];
    }
}

public sealed class ListFinancialCloseSnapshotsQueryHandler
    : IRequestHandler<ListFinancialCloseSnapshotsQuery, IReadOnlyList<FinancialCloseSnapshotDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListFinancialCloseSnapshotsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<FinancialCloseSnapshotDto>> Handle(
        ListFinancialCloseSnapshotsQuery request,
        CancellationToken cancellationToken)
    {
        ListFinancialClosesQueryHandler.EnsureTenant(_tenantContext);
        var exists = await _db.FinancialCloses.AsNoTracking()
            .AnyAsync(c => c.Id == request.FinancialCloseId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundAppException("Không tìm thấy lần chốt tài chính.");
        }

        var snapshots = await _db.FinancialCloseSnapshots.AsNoTracking()
            .Where(s => s.FinancialCloseId == request.FinancialCloseId)
            .OrderBy(s => s.SnapshotVersion)
            .ToListAsync(cancellationToken);

        var snapshotIds = snapshots.Select(s => s.Id).ToList();
        var details = await _db.FinancialCloseSnapshotDetails.AsNoTracking()
            .Where(d => snapshotIds.Contains(d.SnapshotId))
            .OrderBy(d => d.LineNo)
            .ToListAsync(cancellationToken);

        var bySnap = details.GroupBy(d => d.SnapshotId).ToDictionary(g => g.Key, g => g.ToList());
        return snapshots
            .Select(s => ListFinancialClosesQueryHandler.MapSnapshot(
                s, bySnap.TryGetValue(s.Id, out var d) ? d : []))
            .ToList();
    }
}

public sealed class GetFinancialCloseSnapshotByIdQueryHandler
    : IRequestHandler<GetFinancialCloseSnapshotByIdQuery, FinancialCloseSnapshotDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetFinancialCloseSnapshotByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<FinancialCloseSnapshotDto> Handle(
        GetFinancialCloseSnapshotByIdQuery request,
        CancellationToken cancellationToken)
    {
        ListFinancialClosesQueryHandler.EnsureTenant(_tenantContext);
        var snapshot = await _db.FinancialCloseSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy bản chốt tài chính.");
        var details = await _db.FinancialCloseSnapshotDetails.AsNoTracking()
            .Where(d => d.SnapshotId == snapshot.Id)
            .OrderBy(d => d.LineNo)
            .ToListAsync(cancellationToken);
        return ListFinancialClosesQueryHandler.MapSnapshot(snapshot, details);
    }
}
