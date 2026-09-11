using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Variances.Queries;

public sealed record VarianceDto(
    Guid Id,
    Guid? ReconciliationId,
    Guid? ReconciliationDetailId,
    string VarianceType,
    decimal Amount,
    string CurrencyCode,
    string SourceType,
    Guid SourceId,
    string? TargetType,
    Guid? TargetId,
    string Status,
    string? Explanation,
    Guid? ExceptionId);

public sealed record ListVariancesQuery(string? Status) : IRequest<IReadOnlyList<VarianceDto>>;
public sealed record GetVarianceByIdQuery(Guid Id) : IRequest<VarianceDto>;

public sealed class ListVariancesQueryHandler : IRequestHandler<ListVariancesQuery, IReadOnlyList<VarianceDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListVariancesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<VarianceDto>> Handle(ListVariancesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.Variances.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(v => v.Status == status);
        }

        var list = await query.OrderByDescending(v => v.Id).ToListAsync(cancellationToken);
        return list.Select(Map).ToList();
    }

    internal static VarianceDto Map(Domain.Entities.Variance v) =>
        new(
            v.Id,
            v.ReconciliationId,
            v.ReconciliationDetailId,
            v.VarianceType,
            v.Amount,
            v.CurrencyCode,
            v.SourceType,
            v.SourceId,
            v.TargetType,
            v.TargetId,
            v.Status,
            v.Explanation,
            v.ExceptionId);
}

public sealed class GetVarianceByIdQueryHandler : IRequestHandler<GetVarianceByIdQuery, VarianceDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetVarianceByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<VarianceDto> Handle(GetVarianceByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var variance = await _db.Variances.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chênh lệch.");
        return ListVariancesQueryHandler.Map(variance);
    }
}
