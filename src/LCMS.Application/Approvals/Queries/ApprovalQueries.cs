using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Approvals.Queries;

public sealed record ApprovalDto(
    Guid Id,
    string ObjectType,
    Guid ObjectId,
    string Status,
    Guid? RequestedBy,
    DateTimeOffset RequestedAt,
    string? RequestReason,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionReason,
    string? Notes);

public sealed record ListApprovalsQuery(string? Status, string? ObjectType) : IRequest<IReadOnlyList<ApprovalDto>>;
public sealed record GetApprovalByIdQuery(Guid Id) : IRequest<ApprovalDto>;

public sealed class ListApprovalsQueryHandler : IRequestHandler<ListApprovalsQuery, IReadOnlyList<ApprovalDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListApprovalsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<ApprovalDto>> Handle(ListApprovalsQuery request, CancellationToken cancellationToken)
    {
        EnsureTenant(_tenantContext);
        var query = _db.Approvals.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim().ToLowerInvariant();
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.ObjectType))
        {
            var objectType = request.ObjectType.Trim().ToLowerInvariant();
            query = query.Where(a => a.ObjectType == objectType);
        }

        var list = await query.OrderByDescending(a => a.Id).ToListAsync(cancellationToken);
        return list.Select(Map).ToList();
    }

    internal static void EnsureTenant(ITenantContext tenantContext)
    {
        if (!tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }
    }

    internal static ApprovalDto Map(Approval a) =>
        new(
            a.Id,
            a.ObjectType,
            a.ObjectId,
            a.Status,
            a.RequestedBy,
            a.RequestedAt,
            a.RequestReason,
            a.DecidedBy,
            a.DecidedAt,
            a.DecisionReason,
            a.Notes);
}

public sealed class GetApprovalByIdQueryHandler : IRequestHandler<GetApprovalByIdQuery, ApprovalDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetApprovalByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ApprovalDto> Handle(GetApprovalByIdQuery request, CancellationToken cancellationToken)
    {
        ListApprovalsQueryHandler.EnsureTenant(_tenantContext);
        var approval = await _db.Approvals.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy yêu cầu phê duyệt.");
        return ListApprovalsQueryHandler.Map(approval);
    }
}
