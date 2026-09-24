using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Common.Paging;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Queries;

public sealed record RevenueAdjustmentDto(
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

public sealed record RevenueMappingSummaryDto(
    Guid Id,
    string MappingStatus,
    int VersionNo,
    byte[] RowVersion);

public sealed record RevenueDto(
    Guid Id,
    Guid BillId,
    string FinancialMaturity,
    decimal ExpectedAmount,
    decimal? ConfirmedAmount,
    decimal? ActualAmount,
    decimal Amount,
    string CurrencyCode,
    decimal? BaseAmount,
    Guid? FxRateId,
    string? RevenueTypeCode,
    Guid? CustomerPartyId,
    string? SourceType,
    Guid? SourceId,
    string? RecognitionPolicyVersion,
    string RecordStatus,
    string ApprovalStatus,
    DateOnly EffectiveDate,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ActualizedAt,
    IReadOnlyList<RevenueAdjustmentDto> Adjustments,
    byte[] RowVersion,
    IReadOnlyList<RevenueMappingSummaryDto>? Mappings = null);

public sealed record RevenueListItemDto(
    Guid Id,
    Guid BillId,
    string FinancialMaturity,
    decimal Amount,
    string CurrencyCode,
    string? RevenueTypeCode,
    string RecordStatus,
    DateOnly EffectiveDate,
    byte[]? RowVersion = null);

public sealed record GetRevenueByIdQuery(Guid Id) : IRequest<RevenueDto>;

public sealed class GetRevenueByIdQueryHandler : IRequestHandler<GetRevenueByIdQuery, RevenueDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public GetRevenueByIdQueryHandler(
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

    public async Task<RevenueDto> Handle(GetRevenueByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (scope, orgSubtree) = await DataScopeFilter.ResolveAsync(
            _permissions, _userContext, _db, _orgHierarchy,
            PermissionCodes.RevenueRead, "Bạn không có quyền xem doanh thu.", cancellationToken);

        var revenue = await _db.Revenues.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy doanh thu.");

        var billOrgId = await DataScopeFilter.BillOrganizationIdAsync(_db, revenue.BillId, cancellationToken);
        if (!DataScopeFilter.AllowsViaBillOrg(
                scope, _userContext.UserId, orgSubtree, revenue.CreatedBy, billOrgId))
        {
            throw new NotFoundAppException("Không tìm thấy doanh thu.");
        }

        var adjustments = await _db.RevenueAdjustments.AsNoTracking()
            .Where(a => a.RevenueId == revenue.Id)
            .OrderBy(a => a.Id)
            .Select(a => new RevenueAdjustmentDto(
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

        var mappings = await _db.RevenueMappings.AsNoTracking()
            .Where(m => m.RevenueId == revenue.Id)
            .OrderByDescending(m => m.VersionNo)
            .Select(m => new RevenueMappingSummaryDto(m.Id, m.MappingStatus, m.VersionNo, m.RowVersion))
            .ToListAsync(cancellationToken);

        return new RevenueDto(
            revenue.Id,
            revenue.BillId,
            revenue.FinancialMaturity,
            revenue.ExpectedAmount,
            revenue.ConfirmedAmount,
            revenue.ActualAmount,
            revenue.Amount,
            revenue.CurrencyCode,
            revenue.BaseAmount,
            revenue.FxRateId,
            revenue.RevenueTypeCode,
            revenue.CustomerPartyId,
            revenue.SourceType,
            revenue.SourceId,
            revenue.RecognitionPolicyVersion,
            revenue.RecordStatus,
            revenue.ApprovalStatus,
            revenue.EffectiveDate,
            revenue.ConfirmedAt,
            revenue.ActualizedAt,
            adjustments,
            revenue.RowVersion,
            mappings);
    }
}

public sealed record ListRevenuesQuery(
    Guid? BillId,
    string? FinancialMaturity,
    int? Page = null,
    int? PageSize = null) : IRequest<PagedResult<RevenueListItemDto>>;

public sealed class ListRevenuesQueryHandler : IRequestHandler<ListRevenuesQuery, PagedResult<RevenueListItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public ListRevenuesQueryHandler(
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

    public async Task<PagedResult<RevenueListItemDto>> Handle(
        ListRevenuesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (page, pageSize, applyPaging) = PagingNormalize.Normalize(request.Page, request.PageSize);

        var (scope, orgSubtree) = await DataScopeFilter.ResolveAsync(
            _permissions, _userContext, _db, _orgHierarchy,
            PermissionCodes.RevenueRead, "Bạn không có quyền xem doanh thu.", cancellationToken);

        var query = _db.Revenues.AsNoTracking().AsQueryable();
        if (request.BillId.HasValue)
        {
            query = query.Where(r => r.BillId == request.BillId);
        }

        if (!string.IsNullOrWhiteSpace(request.FinancialMaturity))
        {
            var maturity = request.FinancialMaturity.Trim().ToLowerInvariant();
            query = query.Where(r => r.FinancialMaturity == maturity);
        }

        if (scope == DataScopes.Own)
        {
            if (!_userContext.HasUser)
            {
                return PagingNormalize.Empty<RevenueListItemDto>(page, pageSize, applyPaging);
            }

            query = query.Where(r => r.CreatedBy == _userContext.UserId);
        }
        else if (scope == DataScopes.Organization)
        {
            var billIds = await DataScopeFilter.BillIdsInOrgSubtreeAsync(_db, orgSubtree, cancellationToken);
            if (billIds.Count == 0)
            {
                return PagingNormalize.Empty<RevenueListItemDto>(page, pageSize, applyPaging);
            }

            query = query.Where(r => billIds.Contains(r.BillId));
        }

        var ordered = query.OrderByDescending(r => r.EffectiveDate).ThenBy(r => r.Id);
        var totalCount = await ordered.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return PagingNormalize.Empty<RevenueListItemDto>(page, pageSize, applyPaging);
        }

        var pageQuery = applyPaging
            ? ordered.Skip((page - 1) * pageSize).Take(pageSize)
            : ordered;

        var items = await pageQuery
            .Select(r => new RevenueListItemDto(
                r.Id,
                r.BillId,
                r.FinancialMaturity,
                r.Amount,
                r.CurrencyCode,
                r.RevenueTypeCode,
                r.RecordStatus,
                r.EffectiveDate,
                r.RowVersion))
            .ToListAsync(cancellationToken);

        return new PagedResult<RevenueListItemDto>(
            items,
            applyPaging ? page : 1,
            applyPaging ? pageSize : totalCount,
            totalCount);
    }
}
