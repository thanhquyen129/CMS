using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Reconciliations.Queries;

public sealed record ReconciliationDetailDto(
    Guid Id,
    Guid ReconciliationId,
    string SourceType,
    Guid SourceId,
    string? TargetType,
    Guid? TargetId,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal MatchedAmount,
    decimal VarianceAmount,
    string CurrencyCode,
    string LineStatus,
    Guid? VarianceId,
    string? Notes);

public sealed record ReconciliationDto(
    Guid Id,
    string ReconciliationType,
    string? RuleCode,
    int VersionNo,
    string Status,
    Guid? BillId,
    string? Notes,
    DateTimeOffset? StartedAt,
    Guid? StartedBy,
    DateTimeOffset? CompletedAt,
    Guid? CompletedBy,
    IReadOnlyList<ReconciliationDetailDto> Details);

public sealed record ListReconciliationsQuery(string? Status) : IRequest<IReadOnlyList<ReconciliationDto>>;
public sealed record GetReconciliationByIdQuery(Guid Id) : IRequest<ReconciliationDto>;

public sealed class ListReconciliationsQueryHandler : IRequestHandler<ListReconciliationsQuery, IReadOnlyList<ReconciliationDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListReconciliationsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ReconciliationDto>> Handle(ListReconciliationsQuery request, CancellationToken cancellationToken)
    {
        EnsureTenant(_tenantContext);
        var query = _db.Reconciliations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(r => r.Status == status);
        }

        var sessions = await query.OrderByDescending(r => r.Id).ToListAsync(cancellationToken);
        var ids = sessions.Select(s => s.Id).ToList();
        var details = await _db.ReconciliationDetails.AsNoTracking()
            .Where(d => ids.Contains(d.ReconciliationId))
            .ToListAsync(cancellationToken);

        return sessions.Select(s => Map(s, details.Where(d => d.ReconciliationId == s.Id).ToList())).ToList();
    }

    internal static void EnsureTenant(ITenantContext tenantContext)
    {
        if (!tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }
    }

    internal static ReconciliationDto Map(Reconciliation r, IReadOnlyList<ReconciliationDetail> details) =>
        new(
            r.Id,
            r.ReconciliationType,
            r.RuleCode,
            r.VersionNo,
            r.Status,
            r.BillId,
            r.Notes,
            r.StartedAt,
            r.StartedBy,
            r.CompletedAt,
            r.CompletedBy,
            details.Select(d => new ReconciliationDetailDto(
                d.Id,
                d.ReconciliationId,
                d.SourceType,
                d.SourceId,
                d.TargetType,
                d.TargetId,
                d.SourceAmount,
                d.TargetAmount,
                d.MatchedAmount,
                d.VarianceAmount,
                d.CurrencyCode,
                d.LineStatus,
                d.VarianceId,
                d.Notes)).ToList());
}

public sealed class GetReconciliationByIdQueryHandler : IRequestHandler<GetReconciliationByIdQuery, ReconciliationDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetReconciliationByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ReconciliationDto> Handle(GetReconciliationByIdQuery request, CancellationToken cancellationToken)
    {
        ListReconciliationsQueryHandler.EnsureTenant(_tenantContext);
        var session = await _db.Reconciliations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên đối soát.");
        var details = await _db.ReconciliationDetails.AsNoTracking()
            .Where(d => d.ReconciliationId == session.Id)
            .ToListAsync(cancellationToken);
        return ListReconciliationsQueryHandler.Map(session, details);
    }
}
