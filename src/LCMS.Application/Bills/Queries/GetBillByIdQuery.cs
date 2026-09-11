using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

public sealed record BillDto(
    Guid Id,
    Guid TenantId,
    string BillNo,
    string BillType,
    string? SourceSystem,
    string? ExternalId,
    string OperationalStatus,
    bool IsActive,
    Guid? OrganizationId,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record BillListItemDto(
    Guid Id,
    string BillNo,
    string BillType,
    string OperationalStatus,
    bool IsActive,
    Guid? OrganizationId,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt);

public sealed record GetBillByIdQuery(Guid Id) : IRequest<BillDto>;

public sealed class GetBillByIdQueryHandler : IRequestHandler<GetBillByIdQuery, BillDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public GetBillByIdQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissions,
        IOrganizationHierarchyService orgHierarchy)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissions = permissions;
        _orgHierarchy = orgHierarchy;
    }

    public async Task<BillDto> Handle(GetBillByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var scope = await _permissions.EnsureAndResolveDataScopeAsync(
            PermissionCodes.BillRead,
            "Bạn không có quyền xem Bill.",
            cancellationToken);

        var bill = await _db.Bills
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (bill is null)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        Guid? actorOrgId = null;
        IReadOnlySet<Guid> orgSubtree = new HashSet<Guid>();
        if (scope == DataScopes.Organization && _userContext.HasUser)
        {
            var actor = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _userContext.UserId, cancellationToken);
            actorOrgId = actor?.OrganizationId;
            orgSubtree = await _orgHierarchy.GetSubtreeIdsAsync(actorOrgId, cancellationToken);
        }

        if (!DataScopeAccess.Allows(
                scope,
                _userContext.UserId,
                actorOrgId,
                orgSubtree,
                bill.CreatedBy,
                bill.OrganizationId))
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        return Map(bill);
    }

    internal static BillDto Map(Bill bill) => new(
        bill.Id,
        bill.TenantId,
        bill.BillNo,
        bill.BillType,
        bill.SourceSystem,
        bill.ExternalId,
        bill.OperationalStatus,
        bill.IsActive,
        bill.OrganizationId,
        bill.CreatedBy,
        bill.CreatedAt);
}

public sealed record ListBillsQuery : IRequest<IReadOnlyList<BillListItemDto>>;

public sealed class ListBillsQueryHandler : IRequestHandler<ListBillsQuery, IReadOnlyList<BillListItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public ListBillsQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissions,
        IOrganizationHierarchyService orgHierarchy)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissions = permissions;
        _orgHierarchy = orgHierarchy;
    }

    public async Task<IReadOnlyList<BillListItemDto>> Handle(
        ListBillsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var scope = await _permissions.EnsureAndResolveDataScopeAsync(
            PermissionCodes.BillRead,
            "Bạn không có quyền xem Bill.",
            cancellationToken);

        var query = _db.Bills.AsNoTracking().AsQueryable();

        if (scope == DataScopes.Own)
        {
            if (!_userContext.HasUser)
            {
                return [];
            }

            query = query.Where(b => b.CreatedBy == _userContext.UserId);
        }
        else if (scope == DataScopes.Organization)
        {
            if (!_userContext.HasUser)
            {
                return [];
            }

            var actor = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _userContext.UserId, cancellationToken);
            var orgSubtree = await _orgHierarchy.GetSubtreeIdsAsync(actor?.OrganizationId, cancellationToken);
            if (orgSubtree.Count == 0)
            {
                return [];
            }

            query = query.Where(b => b.OrganizationId != null && orgSubtree.Contains(b.OrganizationId.Value));
        }

        return await query
            .OrderByDescending(b => b.BillNo)
            .ThenBy(b => b.Id)
            .Select(b => new BillListItemDto(
                b.Id,
                b.BillNo,
                b.BillType,
                b.OperationalStatus,
                b.IsActive,
                b.OrganizationId,
                b.CreatedBy,
                b.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
