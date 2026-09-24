using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Common.Paging;
using LCMS.Application.Identity;
using LCMS.Application.OperationalReferences;
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
    DateTimeOffset CreatedAt,
    Guid? CustomerPartyId = null,
    string? CustomerName = null,
    string? RouteCode = null,
    DateTimeOffset? EtdAt = null,
    DateTimeOffset? EtaAt = null,
    Guid? AssignedUserId = null,
    string? AssignedUserName = null,
    string? Description = null,
    string? InternalNote = null,
    string? TransportMode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? CustomerReference = null,
    OperationalContextDocument? Context = null,
    Guid? PayerPartyId = null,
    Guid? ShipperPartyId = null,
    Guid? ConsigneePartyId = null,
    Guid? BillToPartyId = null,
    Guid? OriginLocationId = null,
    Guid? DestinationLocationId = null,
    Guid? RouteId = null);

public sealed record BillListItemDto(
    Guid Id,
    string BillNo,
    string BillType,
    string OperationalStatus,
    bool IsActive,
    Guid? OrganizationId,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    /// <summary>Primary currency for list rollup (alphabetical among currencies with activity).</summary>
    string? SummaryCurrencyCode = null,
    decimal? RevenueBestAvailable = null,
    decimal? CostBestAvailable = null,
    decimal? ProfitBestAvailable = null,
    decimal? RevenueExpectedTotal = null,
    decimal? RevenueConfirmedTotal = null,
    decimal? RevenueActualTotal = null,
    decimal? CostExpectedTotal = null,
    decimal? CostConfirmedTotal = null,
    decimal? CostActualTotal = null,
    string? CustomerName = null,
    string? RouteCode = null,
    int? CostLineCount = null,
    int? RevenueLineCount = null,
    int? DocumentCount = null,
    string? TransportMode = null,
    DateTimeOffset? EtdAt = null,
    DateTimeOffset? EtaAt = null,
    string? ExternalId = null,
    string? MasterBillNo = null,
    string? CustomerReference = null);

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

        string? customerName = null;
        if (bill.CustomerPartyId is Guid partyId)
        {
            customerName = await _db.BusinessParties.AsNoTracking()
                .Where(p => p.Id == partyId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        string? assignedName = null;
        if (bill.AssignedUserId is Guid uid)
        {
            assignedName = await _db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => u.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Map(bill, customerName, assignedName);
    }

    internal static BillDto Map(
        Bill bill,
        string? customerName = null,
        string? assignedUserName = null) => new(
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
        bill.CreatedAt,
        bill.CustomerPartyId,
        customerName,
        bill.RouteCode,
        bill.EtdAt,
        bill.EtaAt,
        bill.AssignedUserId,
        assignedUserName,
        bill.Description,
        bill.InternalNote,
        bill.TransportMode,
        bill.OriginCode,
        bill.DestinationCode,
        bill.CustomerReference,
        OperationalContextJson.Deserialize(bill.ContextJson),
        bill.PayerPartyId,
        bill.ShipperPartyId,
        bill.ConsigneePartyId,
        bill.BillToPartyId,
        bill.OriginLocationId,
        bill.DestinationLocationId,
        bill.RouteId);
}

public sealed record ListBillsQuery(
    string? Q = null,
    int? Page = null,
    int? PageSize = null) : IRequest<PagedResult<BillListItemDto>>;

public sealed class ListBillsQueryHandler : IRequestHandler<ListBillsQuery, PagedResult<BillListItemDto>>
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

    public async Task<PagedResult<BillListItemDto>> Handle(
        ListBillsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (page, pageSize, applyPaging) = PagingNormalize.Normalize(request.Page, request.PageSize);

        var scope = await _permissions.EnsureAndResolveDataScopeAsync(
            PermissionCodes.BillRead,
            "Bạn không có quyền xem Bill.",
            cancellationToken);

        var query = _db.Bills.AsNoTracking().AsQueryable();

        if (scope == DataScopes.Own)
        {
            if (!_userContext.HasUser)
            {
                return PagingNormalize.Empty<BillListItemDto>(page, pageSize, applyPaging);
            }

            query = query.Where(b => b.CreatedBy == _userContext.UserId);
        }
        else if (scope == DataScopes.Organization)
        {
            if (!_userContext.HasUser)
            {
                return PagingNormalize.Empty<BillListItemDto>(page, pageSize, applyPaging);
            }

            var actor = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _userContext.UserId, cancellationToken);
            var orgSubtree = await _orgHierarchy.GetSubtreeIdsAsync(actor?.OrganizationId, cancellationToken);
            if (orgSubtree.Count == 0)
            {
                return PagingNormalize.Empty<BillListItemDto>(page, pageSize, applyPaging);
            }

            query = query.Where(b => b.OrganizationId != null && orgSubtree.Contains(b.OrganizationId.Value));
        }

        var q = request.Q?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            var pattern = q.ToLowerInvariant();
            var orderBillIds = await _db.OrderBillLinks.AsNoTracking()
                .Join(
                    _db.Orders.AsNoTracking(),
                    l => l.OrderId,
                    o => o.Id,
                    (l, o) => new { l.BillId, o.ExternalId, o.OrderNo })
                .Where(x =>
                    x.ExternalId.ToLower().Contains(pattern)
                    || x.OrderNo.ToLower().Contains(pattern))
                .Select(x => x.BillId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var waybillBillIds = await _db.BillWaybills.AsNoTracking()
                .Where(w =>
                    (w.SenderName != null && w.SenderName.ToLower().Contains(pattern))
                    || (w.ConsigneeName != null && w.ConsigneeName.ToLower().Contains(pattern))
                    || (w.SenderCustomerCode != null && w.SenderCustomerCode.ToLower().Contains(pattern)))
                .Select(w => w.BillId)
                .Distinct()
                .ToListAsync(cancellationToken);

            query = query.Where(b =>
                b.BillNo.ToLower().Contains(pattern)
                || (b.ExternalId != null && b.ExternalId.ToLower().Contains(pattern))
                || (b.MasterBillNo != null && b.MasterBillNo.ToLower().Contains(pattern))
                || (b.CustomerReference != null && b.CustomerReference.ToLower().Contains(pattern))
                || orderBillIds.Contains(b.Id)
                || waybillBillIds.Contains(b.Id));
        }

        var ordered = query.OrderByDescending(b => b.BillNo).ThenBy(b => b.Id);
        var totalCount = await ordered.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return PagingNormalize.Empty<BillListItemDto>(page, pageSize, applyPaging);
        }

        var pageQuery = applyPaging
            ? ordered.Skip((page - 1) * pageSize).Take(pageSize)
            : ordered;

        var bills = await pageQuery
            .Select(b => new BillListItemDto(
                b.Id,
                b.BillNo,
                b.BillType,
                b.OperationalStatus,
                b.IsActive,
                b.OrganizationId,
                b.CreatedBy,
                b.CreatedAt,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                b.RouteCode,
                null,
                null,
                null,
                b.TransportMode,
                b.EtdAt,
                b.EtaAt,
                b.ExternalId,
                b.MasterBillNo,
                b.CustomerReference))
            .ToListAsync(cancellationToken);

        var enriched = await AttachFinancialSummariesAsync(bills, cancellationToken);
        return new PagedResult<BillListItemDto>(
            enriched,
            applyPaging ? page : 1,
            applyPaging ? pageSize : totalCount,
            totalCount);
    }

    /// <summary>
    /// Batch financial rollup for list (current maturity; no asOf).
    /// Best Available = Actual → Confirmed → Expected. Cost includes finalized allocations.
    /// </summary>
    private async Task<IReadOnlyList<BillListItemDto>> AttachFinancialSummariesAsync(
        IReadOnlyList<BillListItemDto> bills,
        CancellationToken cancellationToken)
    {
        var billIds = bills.Select(b => b.Id).ToList();

        var contexts = await _db.Bills.AsNoTracking()
            .Where(b => billIds.Contains(b.Id))
            .Select(b => new { b.Id, b.CustomerPartyId, b.RouteCode })
            .ToListAsync(cancellationToken);
        var contextById = contexts.ToDictionary(c => c.Id);

        var revenues = await _db.Revenues.AsNoTracking()
            .Where(r => billIds.Contains(r.BillId) && r.RecordStatus == "active")
            .Select(r => new LineAmount(
                r.BillId,
                r.CurrencyCode,
                r.ExpectedAmount,
                r.ConfirmedAmount,
                r.ActualAmount,
                r.CustomerPartyId))
            .ToListAsync(cancellationToken);

        var directCosts = await _db.Costs.AsNoTracking()
            .Where(c =>
                c.BillId != null
                && billIds.Contains(c.BillId.Value)
                && c.AttributionType == CostAttributionTypes.Direct
                && c.RecordStatus == "active")
            .Select(c => new LineAmount(
                c.BillId!.Value,
                c.CurrencyCode,
                c.ExpectedAmount,
                c.ConfirmedAmount,
                c.ActualAmount,
                null))
            .ToListAsync(cancellationToken);

        var allocated = await (
            from d in _db.CostAllocationDetails.AsNoTracking()
            join a in _db.CostAllocations.AsNoTracking() on d.AllocationId equals a.Id
            join c in _db.Costs.AsNoTracking() on a.CostId equals c.Id
            where billIds.Contains(d.BillId)
                  && a.AllocationStatus == CostAllocationStatuses.Finalized
                  && c.RecordStatus == "active"
            select new { d.BillId, c.CurrencyCode, d.AllocatedAmount }
        ).ToListAsync(cancellationToken);

        var docCounts = await _db.FinancialDocuments.AsNoTracking()
            .Where(d => d.BillId != null && billIds.Contains(d.BillId.Value))
            .GroupBy(d => d.BillId!.Value)
            .Select(g => new { BillId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var docByBill = docCounts.ToDictionary(x => x.BillId, x => x.Count);

        // Order CreatedAt in-memory — SQLite rejects DateTimeOffset in SQL ORDER BY.
        var ratingRoutes = await _db.Ratings.AsNoTracking()
            .Where(r => billIds.Contains(r.BillId) && r.RouteCode != null && r.RouteCode != "")
            .Select(r => new { r.BillId, r.RouteCode, r.CreatedAt })
            .ToListAsync(cancellationToken);
        var routeFromRating = ratingRoutes
            .OrderByDescending(r => r.CreatedAt)
            .GroupBy(r => r.BillId)
            .ToDictionary(g => g.Key, g => g.First().RouteCode);

        var partyIds = contexts
            .Where(c => c.CustomerPartyId != null)
            .Select(c => c.CustomerPartyId!.Value)
            .Concat(revenues.Where(r => r.CustomerPartyId != null).Select(r => r.CustomerPartyId!.Value))
            .Distinct()
            .ToList();
        var partyNames = partyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.BusinessParties.AsNoTracking()
                .Where(p => partyIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var senderByBill = await _db.BillWaybills.AsNoTracking()
            .Where(w => billIds.Contains(w.BillId) && w.SenderName != null && w.SenderName != "")
            .Select(w => new { w.BillId, w.SenderName })
            .ToListAsync(cancellationToken);
        var senderNameByBill = senderByBill
            .GroupBy(x => x.BillId)
            .ToDictionary(g => g.Key, g => g.First().SenderName);

        var revByBill = revenues.GroupBy(r => r.BillId).ToDictionary(g => g.Key, g => g.ToList());
        var costByBill = directCosts.GroupBy(c => c.BillId).ToDictionary(g => g.Key, g => g.ToList());
        var allocByBill = allocated.GroupBy(a => a.BillId).ToDictionary(
            g => g.Key,
            g => g.ToList());

        var enriched = new List<BillListItemDto>(bills.Count);
        foreach (var bill in bills)
        {
            revByBill.TryGetValue(bill.Id, out var revLines);
            costByBill.TryGetValue(bill.Id, out var costLines);
            allocByBill.TryGetValue(bill.Id, out var allocLines);
            contextById.TryGetValue(bill.Id, out var ctx);

            revLines ??= [];
            costLines ??= [];

            string? customerName = null;
            if (ctx?.CustomerPartyId is Guid cpid && partyNames.TryGetValue(cpid, out var storedName))
            {
                customerName = storedName;
            }
            else
            {
                var derivedParty = revLines
                    .Select(r => r.CustomerPartyId)
                    .FirstOrDefault(id => id != null);
                if (derivedParty is Guid dpid && partyNames.TryGetValue(dpid, out var derivedName))
                {
                    customerName = derivedName;
                }
                else if (senderNameByBill.TryGetValue(bill.Id, out var senderName))
                {
                    customerName = senderName;
                }
            }

            var routeCode = !string.IsNullOrWhiteSpace(ctx?.RouteCode)
                ? ctx!.RouteCode
                : !string.IsNullOrWhiteSpace(bill.RouteCode)
                    ? bill.RouteCode
                    : routeFromRating.GetValueOrDefault(bill.Id);

            var currencyCodes = revLines.Select(r => r.CurrencyCode)
                .Concat(costLines.Select(c => c.CurrencyCode))
                .Concat((allocLines ?? []).Select(a => a.CurrencyCode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            docByBill.TryGetValue(bill.Id, out var docCount);

            if (currencyCodes.Count == 0)
            {
                enriched.Add(bill with
                {
                    CustomerName = customerName,
                    RouteCode = routeCode,
                    CostLineCount = costLines.Count,
                    RevenueLineCount = revLines.Count,
                    DocumentCount = docCount
                });
                continue;
            }

            // Primary currency for list columns = first alphabetical (same as profile bucket order).
            var code = currencyCodes[0];
            var rev = revLines
                .Where(r => string.Equals(r.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var costs = costLines
                .Where(c => string.Equals(c.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var allocTotal = (allocLines ?? [])
                .Where(a => string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .Sum(a => a.AllocatedAmount);

            var revExpected = rev.Sum(r => r.Expected);
            var revConfirmed = rev.Sum(r => r.Confirmed ?? 0m);
            var revActual = rev.Sum(r => r.Actual ?? 0m);
            var revBest = rev.Sum(r => BestAvailable(r.Expected, r.Confirmed, r.Actual));
            var costExpected = costs.Sum(c => c.Expected) + allocTotal;
            var costConfirmed = costs.Sum(c => c.Confirmed ?? 0m) + allocTotal;
            var costActual = costs.Sum(c => c.Actual ?? 0m) + allocTotal;
            var directBest = costs.Sum(c => BestAvailable(c.Expected, c.Confirmed, c.Actual));
            var costBest = decimal.Round(directBest + allocTotal, 4, MidpointRounding.AwayFromZero);
            var profit = decimal.Round(revBest - costBest, 4, MidpointRounding.AwayFromZero);

            enriched.Add(bill with
            {
                SummaryCurrencyCode = code.ToUpperInvariant(),
                RevenueBestAvailable = decimal.Round(revBest, 4, MidpointRounding.AwayFromZero),
                CostBestAvailable = costBest,
                ProfitBestAvailable = profit,
                RevenueExpectedTotal = decimal.Round(revExpected, 4, MidpointRounding.AwayFromZero),
                RevenueConfirmedTotal = decimal.Round(revConfirmed, 4, MidpointRounding.AwayFromZero),
                RevenueActualTotal = decimal.Round(revActual, 4, MidpointRounding.AwayFromZero),
                CostExpectedTotal = decimal.Round(costExpected, 4, MidpointRounding.AwayFromZero),
                CostConfirmedTotal = decimal.Round(costConfirmed, 4, MidpointRounding.AwayFromZero),
                CostActualTotal = decimal.Round(costActual, 4, MidpointRounding.AwayFromZero),
                CustomerName = customerName,
                RouteCode = routeCode,
                CostLineCount = costLines.Count,
                RevenueLineCount = revLines.Count,
                DocumentCount = docCount
            });
        }

        return enriched;
    }

    private static decimal BestAvailable(decimal expected, decimal? confirmed, decimal? actual)
    {
        if (actual.HasValue)
        {
            return actual.Value;
        }

        if (confirmed.HasValue)
        {
            return confirmed.Value;
        }

        return expected;
    }

    private sealed record LineAmount(
        Guid BillId,
        string CurrencyCode,
        decimal Expected,
        decimal? Confirmed,
        decimal? Actual,
        Guid? CustomerPartyId);
}

