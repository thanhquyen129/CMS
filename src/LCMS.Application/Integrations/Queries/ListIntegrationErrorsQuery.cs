using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Integrations.Queries;

public sealed record IntegrationErrorDto(
    Guid Id,
    Guid IntegrationRecordId,
    string ErrorCode,
    string Message,
    string? Detail,
    int AttemptNo,
    DateTimeOffset OccurredAt,
    DateTimeOffset? NextRetryAt,
    string RecoveryStatus,
    DateTimeOffset? RecoveredAt,
    string? RecoveryNote);

public sealed record ListIntegrationErrorsQuery(
    Guid? IntegrationRecordId = null,
    string? RecoveryStatus = null,
    int Take = 100) : IRequest<IReadOnlyList<IntegrationErrorDto>>;

public sealed class ListIntegrationErrorsQueryHandler
    : IRequestHandler<ListIntegrationErrorsQuery, IReadOnlyList<IntegrationErrorDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListIntegrationErrorsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<IntegrationErrorDto>> Handle(
        ListIntegrationErrorsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var take = Math.Clamp(request.Take, 1, 500);
        var query = _db.IntegrationErrors.AsNoTracking().AsQueryable();

        if (request.IntegrationRecordId.HasValue)
        {
            query = query.Where(e => e.IntegrationRecordId == request.IntegrationRecordId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.RecoveryStatus))
        {
            var status = request.RecoveryStatus.Trim().ToLowerInvariant();
            query = query.Where(e => e.RecoveryStatus == status);
        }

        return await query
            .OrderByDescending(e => e.Id)
            .Take(take)
            .Select(e => new IntegrationErrorDto(
                e.Id,
                e.IntegrationRecordId,
                e.ErrorCode,
                e.Message,
                e.Detail,
                e.AttemptNo,
                e.OccurredAt,
                e.NextRetryAt,
                e.RecoveryStatus,
                e.RecoveredAt,
                e.RecoveryNote))
            .ToListAsync(cancellationToken);
    }
}
