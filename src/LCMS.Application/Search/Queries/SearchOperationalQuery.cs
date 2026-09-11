using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Bills.Queries;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Search.Queries;

public sealed record OperationalSearchHitDto(
    Guid BillId,
    string BillNo,
    string BillType,
    string OperationalStatus,
    string MatchKind,
    string? MatchedValue);

public sealed record SearchOperationalQuery(string Q) : IRequest<IReadOnlyList<OperationalSearchHitDto>>;

public sealed class SearchOperationalQueryValidator : AbstractValidator<SearchOperationalQuery>
{
    public SearchOperationalQueryValidator()
    {
        RuleFor(x => x.Q)
            .NotEmpty().WithMessage("Từ khóa tìm kiếm không được để trống.")
            .MaximumLength(128).WithMessage("Từ khóa tìm kiếm không được vượt quá 128 ký tự.");
    }
}

/// <summary>
/// Tenant-scoped operational search: bill_no / bill external_id / order external_id (and order_no).
/// Respects JWT Data Scope from Pass 2 S1.
/// </summary>
public sealed class SearchOperationalQueryHandler
    : IRequestHandler<SearchOperationalQuery, IReadOnlyList<OperationalSearchHitDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public SearchOperationalQueryHandler(
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

    public async Task<IReadOnlyList<OperationalSearchHitDto>> Handle(
        SearchOperationalQuery request,
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

        var pattern = request.Q.Trim().ToLowerInvariant();
        var bills = _db.Bills.AsNoTracking().AsQueryable();

        if (scope == DataScopes.Own)
        {
            if (!_userContext.HasUser)
            {
                return [];
            }

            bills = bills.Where(b => b.CreatedBy == _userContext.UserId);
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

            bills = bills.Where(b => b.OrganizationId != null && orgSubtree.Contains(b.OrganizationId.Value));
        }

        var billHits = await bills
            .Where(b =>
                b.BillNo.ToLower().Contains(pattern)
                || (b.ExternalId != null && b.ExternalId.ToLower().Contains(pattern)))
            .Select(b => new
            {
                b.Id,
                b.BillNo,
                b.BillType,
                b.OperationalStatus,
                MatchKind = b.BillNo.ToLower().Contains(pattern) ? "bill_no" : "bill_external_id",
                MatchedValue = b.BillNo.ToLower().Contains(pattern) ? b.BillNo : b.ExternalId
            })
            .ToListAsync(cancellationToken);

        var scopedBillIds = await bills.Select(b => b.Id).ToListAsync(cancellationToken);

        var orderHits = await (
            from l in _db.OrderBillLinks.AsNoTracking()
            join o in _db.Orders.AsNoTracking() on l.OrderId equals o.Id
            join b in _db.Bills.AsNoTracking() on l.BillId equals b.Id
            where scopedBillIds.Contains(b.Id)
                  && (o.ExternalId.ToLower().Contains(pattern) || o.OrderNo.ToLower().Contains(pattern))
            select new
            {
                BillId = b.Id,
                b.BillNo,
                b.BillType,
                b.OperationalStatus,
                MatchKind = o.ExternalId.ToLower().Contains(pattern) ? "order_external_id" : "order_no",
                MatchedValue = o.ExternalId.ToLower().Contains(pattern) ? o.ExternalId : o.OrderNo
            }).ToListAsync(cancellationToken);

        return billHits
            .Select(h => new OperationalSearchHitDto(
                h.Id,
                h.BillNo,
                h.BillType,
                h.OperationalStatus,
                h.MatchKind,
                h.MatchedValue))
            .Concat(orderHits.Select(h => new OperationalSearchHitDto(
                h.BillId,
                h.BillNo,
                h.BillType,
                h.OperationalStatus,
                h.MatchKind,
                h.MatchedValue)))
            .GroupBy(h => new { h.BillId, h.MatchKind, h.MatchedValue })
            .Select(g => g.First())
            .OrderBy(h => h.BillNo)
            .ThenBy(h => h.MatchKind)
            .ToList();
    }
}
