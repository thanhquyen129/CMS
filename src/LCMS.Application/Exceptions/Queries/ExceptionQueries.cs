using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Exceptions.Queries;

public sealed record ExceptionDto(
    Guid Id,
    string RuleCode,
    string Severity,
    Guid? OwnerId,
    string Status,
    DateTimeOffset? DueAt,
    string Title,
    string? Description,
    Guid? BillId,
    Guid? ReconciliationId,
    Guid? VarianceId,
    DateTimeOffset? ResolvedAt,
    Guid? ResolvedBy,
    string? ResolutionNotes,
    DateTimeOffset? ClosedAt,
    Guid? ClosedBy);

public sealed record ListExceptionsQuery(string? Status, string? Severity) : IRequest<IReadOnlyList<ExceptionDto>>;
public sealed record GetExceptionByIdQuery(Guid Id) : IRequest<ExceptionDto>;

public sealed class ListExceptionsQueryHandler : IRequestHandler<ListExceptionsQuery, IReadOnlyList<ExceptionDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListExceptionsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ExceptionDto>> Handle(ListExceptionsQuery request, CancellationToken cancellationToken)
    {
        EnsureTenant(_tenantContext);
        var query = _db.Exceptions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(e => e.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Severity))
        {
            var severity = request.Severity.Trim().ToLowerInvariant();
            query = query.Where(e => e.Severity == severity);
        }

        var list = await query.OrderByDescending(e => e.Id).ToListAsync(cancellationToken);
        return list.Select(Map).ToList();
    }

    internal static void EnsureTenant(ITenantContext tenantContext)
    {
        if (!tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }
    }

    internal static ExceptionDto Map(FinancialException e) =>
        new(
            e.Id,
            e.RuleCode,
            e.Severity,
            e.OwnerId,
            e.Status,
            e.DueAt,
            e.Title,
            e.Description,
            e.BillId,
            e.ReconciliationId,
            e.VarianceId,
            e.ResolvedAt,
            e.ResolvedBy,
            e.ResolutionNotes,
            e.ClosedAt,
            e.ClosedBy);
}

public sealed class GetExceptionByIdQueryHandler : IRequestHandler<GetExceptionByIdQuery, ExceptionDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetExceptionByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ExceptionDto> Handle(GetExceptionByIdQuery request, CancellationToken cancellationToken)
    {
        ListExceptionsQueryHandler.EnsureTenant(_tenantContext);
        var entity = await _db.Exceptions.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy ngoại lệ.");
        return ListExceptionsQueryHandler.Map(entity);
    }
}
