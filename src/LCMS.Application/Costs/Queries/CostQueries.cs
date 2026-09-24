using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Common.Paging;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Queries;

public sealed record CostAdjustmentDto(
    Guid Id,
    string AdjustmentType,
    decimal DeltaAmount,
    string CurrencyCode,
    string Reason,
    DateOnly EffectiveDate,
    string AppliedToMaturity,
    decimal AmountBefore,
    decimal AmountAfter,
    DateTimeOffset CreatedAt);

public sealed record CostAllocationDetailDto(
    Guid Id,
    Guid BillId,
    decimal BasisValue,
    decimal BasisRatio,
    decimal AllocatedAmount,
    decimal RoundingAdjustment,
    decimal? ManualOverrideAmount,
    string? OverrideReason,
    decimal? OverrideBeforeAmount = null);

public sealed record CostAllocationDto(
    Guid Id,
    int VersionNo,
    string AllocationBasis,
    string AllocationStatus,
    decimal AllocatableAmount,
    decimal AllocatedAmount,
    DateTimeOffset? FinalizedAt,
    Guid? SupersedesAllocationId,
    Guid? CreatedBy,
    IReadOnlyList<CostAllocationDetailDto> Details);

public sealed record CostDto(
    Guid Id,
    Guid? BillId,
    string AttributionType,
    string FinancialMaturity,
    decimal ExpectedAmount,
    decimal? ConfirmedAmount,
    decimal? ActualAmount,
    decimal Amount,
    string CurrencyCode,
    decimal? BaseAmount,
    Guid? FxRateId,
    string? CostTypeCode,
    Guid? VendorPartyId,
    string? SourceType,
    Guid? SourceId,
    string RecordStatus,
    string ApprovalStatus,
    DateOnly EffectiveDate,
    Guid? OrganizationId,
    Guid? CreatedBy,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ActualizedAt,
    IReadOnlyList<CostAdjustmentDto> Adjustments,
    IReadOnlyList<CostAllocationDto> Allocations,
    byte[] RowVersion);

public sealed record CostListItemDto(
    Guid Id,
    Guid? BillId,
    string AttributionType,
    string FinancialMaturity,
    decimal Amount,
    string CurrencyCode,
    string? CostTypeCode,
    string RecordStatus,
    Guid? OrganizationId,
    Guid? CreatedBy,
    DateOnly EffectiveDate,
    Guid? VendorPartyId = null,
    decimal ExpectedAmount = 0,
    decimal? ConfirmedAmount = null,
    decimal? ActualAmount = null,
    byte[]? RowVersion = null);

public sealed record GetCostByIdQuery(Guid Id) : IRequest<CostDto>;

public sealed class GetCostByIdQueryHandler : IRequestHandler<GetCostByIdQuery, CostDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public GetCostByIdQueryHandler(
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

    public async Task<CostDto> Handle(GetCostByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var scope = await _permissions.EnsureAndResolveDataScopeAsync(
            PermissionCodes.CostRead,
            "Bạn không có quyền xem chi phí.",
            cancellationToken);

        var cost = await _db.Costs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi phí.");

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
                cost.CreatedBy,
                cost.OrganizationId))
        {
            throw new NotFoundAppException("Không tìm thấy chi phí.");
        }

        var adjustments = await _db.CostAdjustments.AsNoTracking()
            .Where(a => a.CostId == cost.Id)
            .OrderBy(a => a.Id)
            .Select(a => new CostAdjustmentDto(
                a.Id,
                a.AdjustmentType,
                a.DeltaAmount,
                a.CurrencyCode,
                a.Reason,
                a.EffectiveDate,
                a.AppliedToMaturity,
                a.AmountBefore,
                a.AmountAfter,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        var allocations = await _db.CostAllocations.AsNoTracking()
            .Where(a => a.CostId == cost.Id)
            .OrderBy(a => a.VersionNo)
            .ToListAsync(cancellationToken);

        var allocationIds = allocations.Select(a => a.Id).ToList();
        var details = await _db.CostAllocationDetails.AsNoTracking()
            .Where(d => allocationIds.Contains(d.AllocationId))
            .ToListAsync(cancellationToken);
        var detailsByAlloc = details.GroupBy(d => d.AllocationId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.BillId).ToList());

        var allocationDtos = allocations.Select(a =>
        {
            detailsByAlloc.TryGetValue(a.Id, out var lines);
            lines ??= [];
            return new CostAllocationDto(
                a.Id,
                a.VersionNo,
                a.AllocationBasis,
                a.AllocationStatus,
                a.AllocatableAmount,
                a.AllocatedAmount,
                a.FinalizedAt,
                a.SupersedesAllocationId,
                a.CreatedBy,
                lines.Select(d => new CostAllocationDetailDto(
                    d.Id,
                    d.BillId,
                    d.BasisValue,
                    d.BasisRatio,
                    d.AllocatedAmount,
                    d.RoundingAdjustment,
                    d.ManualOverrideAmount,
                    d.OverrideReason,
                    d.OverrideBeforeAmount)).ToList());
        }).ToList();

        return new CostDto(
            cost.Id,
            cost.BillId,
            cost.AttributionType,
            cost.FinancialMaturity,
            cost.ExpectedAmount,
            cost.ConfirmedAmount,
            cost.ActualAmount,
            cost.Amount,
            cost.CurrencyCode,
            cost.BaseAmount,
            cost.FxRateId,
            cost.CostTypeCode,
            cost.VendorPartyId,
            cost.SourceType,
            cost.SourceId,
            cost.RecordStatus,
            cost.ApprovalStatus,
            cost.EffectiveDate,
            cost.OrganizationId,
            cost.CreatedBy,
            cost.ConfirmedAt,
            cost.ActualizedAt,
            adjustments,
            allocationDtos,
            cost.RowVersion);
    }
}

public sealed record ListCostsQuery(
    Guid? BillId,
    string? FinancialMaturity,
    string? AttributionType = null,
    Guid? VendorPartyId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int? Page = null,
    int? PageSize = null) : IRequest<PagedResult<CostListItemDto>>;

public sealed class ListCostsQueryHandler : IRequestHandler<ListCostsQuery, PagedResult<CostListItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public ListCostsQueryHandler(
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

    public async Task<PagedResult<CostListItemDto>> Handle(ListCostsQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (page, pageSize, applyPaging) = PagingNormalize.Normalize(request.Page, request.PageSize);

        var scope = await _permissions.EnsureAndResolveDataScopeAsync(
            PermissionCodes.CostRead,
            "Bạn không có quyền xem chi phí.",
            cancellationToken);

        var query = _db.Costs.AsNoTracking().AsQueryable();
        if (request.BillId.HasValue)
        {
            query = query.Where(c => c.BillId == request.BillId);
        }

        if (!string.IsNullOrWhiteSpace(request.FinancialMaturity))
        {
            var maturity = request.FinancialMaturity.Trim().ToLowerInvariant();
            query = query.Where(c => c.FinancialMaturity == maturity);
        }

        if (!string.IsNullOrWhiteSpace(request.AttributionType))
        {
            var attribution = request.AttributionType.Trim().ToLowerInvariant();
            query = query.Where(c => c.AttributionType == attribution);
        }

        if (request.VendorPartyId.HasValue)
        {
            query = query.Where(c => c.VendorPartyId == request.VendorPartyId);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(c => c.EffectiveDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(c => c.EffectiveDate <= request.ToDate.Value);
        }

        if (scope == DataScopes.Own)
        {
            if (!_userContext.HasUser)
            {
                return PagingNormalize.Empty<CostListItemDto>(page, pageSize, applyPaging);
            }

            query = query.Where(c => c.CreatedBy == _userContext.UserId);
        }
        else if (scope == DataScopes.Organization)
        {
            if (!_userContext.HasUser)
            {
                return PagingNormalize.Empty<CostListItemDto>(page, pageSize, applyPaging);
            }

            var actor = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _userContext.UserId, cancellationToken);
            var orgSubtree = await _orgHierarchy.GetSubtreeIdsAsync(actor?.OrganizationId, cancellationToken);
            if (orgSubtree.Count == 0)
            {
                return PagingNormalize.Empty<CostListItemDto>(page, pageSize, applyPaging);
            }

            query = query.Where(c => c.OrganizationId != null && orgSubtree.Contains(c.OrganizationId.Value));
        }

        var ordered = query.OrderByDescending(c => c.EffectiveDate).ThenBy(c => c.Id);
        var totalCount = await ordered.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return PagingNormalize.Empty<CostListItemDto>(page, pageSize, applyPaging);
        }

        var pageQuery = applyPaging
            ? ordered.Skip((page - 1) * pageSize).Take(pageSize)
            : ordered;

        var items = await pageQuery
            .Select(c => new CostListItemDto(
                c.Id,
                c.BillId,
                c.AttributionType,
                c.FinancialMaturity,
                c.Amount,
                c.CurrencyCode,
                c.CostTypeCode,
                c.RecordStatus,
                c.OrganizationId,
                c.CreatedBy,
                c.EffectiveDate,
                c.VendorPartyId,
                c.ExpectedAmount,
                c.ConfirmedAmount,
                c.ActualAmount,
                c.RowVersion))
            .ToListAsync(cancellationToken);

        return new PagedResult<CostListItemDto>(
            items,
            applyPaging ? page : 1,
            applyPaging ? pageSize : totalCount,
            totalCount);
    }
}
