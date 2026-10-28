using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Common.Paging;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateCards.Queries;

public sealed record RateCardDto(
    Guid Id,
    string Code,
    string Name,
    string PartyType,
    string CurrencyCode,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? TransportMode = null,
    string? RouteCode = null,
    string? CarrierName = null,
    Guid? SupplierPartyId = null,
    Guid? CustomerPartyId = null,
    string? CustomerGroupCode = null,
    bool HasPublishedVersion = false,
    Guid? RouteId = null,
    string? DeclarationMode = null,
    bool? RouteIsActive = null,
    string? RouteOriginCode = null,
    string? RouteDestinationCode = null,
    string? RowVersion = null);

public sealed record GetRateCardByIdQuery(Guid Id) : IRequest<RateCardDto>;

public sealed class GetRateCardByIdQueryHandler : IRequestHandler<GetRateCardByIdQuery, RateCardDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetRateCardByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<RateCardDto> Handle(GetRateCardByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var card = await _db.RateCards
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (card is null)
        {
            throw new NotFoundAppException("Không tìm thấy bảng giá.");
        }

        var hasPublished = await _db.RateVersions.AnyAsync(
            v => v.RateCardId == card.Id && v.Status == RateVersionStatuses.Published,
            cancellationToken);
        bool? routeActive = null;
        string? origin = null;
        string? destination = null;
        if (card.RouteId is Guid routeId)
        {
            var route = await _db.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == routeId, cancellationToken);
            if (route is null)
            {
                routeActive = false;
            }
            else
            {
                routeActive = route.IsActive;
                var codes = await _db.Locations.AsNoTracking()
                    .Where(l => l.Id == route.OriginLocationId || l.Id == route.DestinationLocationId)
                    .Select(l => new { l.Id, l.Code })
                    .ToListAsync(cancellationToken);
                origin = codes.FirstOrDefault(l => l.Id == route.OriginLocationId)?.Code;
                destination = codes.FirstOrDefault(l => l.Id == route.DestinationLocationId)?.Code;
            }
        }

        return ToDto(card) with
        {
            HasPublishedVersion = hasPublished,
            RouteIsActive = routeActive,
            RouteOriginCode = origin,
            RouteDestinationCode = destination
        };
    }

    internal static RateCardDto ToDto(Domain.Entities.RateCard card) =>
        new(
            card.Id,
            card.Code,
            card.Name,
            card.PartyType,
            card.CurrencyCode,
            card.Description,
            card.IsActive,
            card.CreatedAt,
            card.TransportMode,
            card.RouteCode,
            card.CarrierName,
            card.SupplierPartyId,
            card.CustomerPartyId,
            card.CustomerGroupCode,
            false,
            card.RouteId,
            card.DeclarationMode,
            null,
            null,
            null,
            Convert.ToBase64String(card.RowVersion));
}

public sealed record ListRateCardsQuery(
    string? Q = null,
    string? PartyType = null,
    bool? IsActive = null,
    int? Page = null,
    int? PageSize = null,
    string? TransportMode = null) : IRequest<PagedResult<RateCardDto>>;

public sealed class ListRateCardsQueryHandler : IRequestHandler<ListRateCardsQuery, PagedResult<RateCardDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ListRateCardsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext, IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<PagedResult<RateCardDto>> Handle(ListRateCardsQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (page, pageSize, applyPaging) = PagingNormalize.Normalize(request.Page, request.PageSize);

        var query = _db.RateCards.AsNoTracking().AsQueryable();
        var canBuy = await _permissions.HasPermissionAsync(PermissionCodes.RateBuyRead, cancellationToken);
        var canSell = await _permissions.HasPermissionAsync(PermissionCodes.RateSellRead, cancellationToken);
        if (!canBuy && !canSell)
        {
            return PagingNormalize.Empty<RateCardDto>(page, pageSize, applyPaging);
        }

        if (canBuy && !canSell)
        {
            query = query.Where(r => r.PartyType == "vendor");
        }
        else if (canSell && !canBuy)
        {
            query = query.Where(r => r.PartyType == "customer");
        }

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var q = request.Q.Trim().ToLowerInvariant();
            query = query.Where(r =>
                r.Code.ToLower().Contains(q)
                || r.Name.ToLower().Contains(q)
                || (r.CarrierName != null && r.CarrierName.ToLower().Contains(q))
                || (r.RouteCode != null && r.RouteCode.ToLower().Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(request.TransportMode))
        {
            var mode = LCMS.Domain.Pricing.RateFieldPolicy.ToStoredMode(request.TransportMode);
            query = query.Where(r => r.TransportMode != null && r.TransportMode.ToUpper() == mode);
        }

        if (!string.IsNullOrWhiteSpace(request.PartyType))
        {
            var partyType = request.PartyType.Trim().ToLowerInvariant();
            query = query.Where(r => r.PartyType.ToLower() == partyType);
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(r => r.IsActive == request.IsActive.Value);
        }

        var ordered = query.OrderBy(r => r.Code);
        var totalCount = await ordered.CountAsync(cancellationToken);
        if (totalCount == 0)
        {
            return PagingNormalize.Empty<RateCardDto>(page, pageSize, applyPaging);
        }

        var pageQuery = applyPaging
            ? ordered.Skip((page - 1) * pageSize).Take(pageSize)
            : ordered;

        var items = await pageQuery
            .Select(r => new RateCardDto(
                r.Id, r.Code, r.Name, r.PartyType, r.CurrencyCode, r.Description, r.IsActive, r.CreatedAt, r.TransportMode, r.RouteCode, r.CarrierName, r.SupplierPartyId, r.CustomerPartyId, r.CustomerGroupCode, false, r.RouteId, r.DeclarationMode, null, null, null, null))
            .ToListAsync(cancellationToken);

        var ids = items.Select(i => i.Id).ToList();
        var publishedIds = await _db.RateVersions.AsNoTracking()
            .Where(v => ids.Contains(v.RateCardId) && v.Status == RateVersionStatuses.Published)
            .Select(v => v.RateCardId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var published = publishedIds.ToHashSet();
        items = items.Select(i => i with { HasPublishedVersion = published.Contains(i.Id) }).ToList();

        return new PagedResult<RateCardDto>(
            items,
            applyPaging ? page : 1,
            applyPaging ? pageSize : totalCount,
            totalCount);
    }
}

public sealed record MixedDeclarationRateCardDto(
    Guid Id,
    string Code,
    string Name,
    string PartyType,
    int VersionNumber,
    int CountRules,
    int CountMatrixBands,
    string Status);

public sealed record ListMixedDeclarationRateCardsQuery : IRequest<IReadOnlyList<MixedDeclarationRateCardDto>>;

/// <summary>Legacy cards that already contain both shapes. Read-only census. Does not delete or migrate.</summary>
public sealed class ListMixedDeclarationRateCardsQueryHandler
    : IRequestHandler<ListMixedDeclarationRateCardsQuery, IReadOnlyList<MixedDeclarationRateCardDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public ListMixedDeclarationRateCardsQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<MixedDeclarationRateCardDto>> Handle(
        ListMixedDeclarationRateCardsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var canBuy = await _permissions.HasPermissionAsync(PermissionCodes.RateBuyRead, cancellationToken);
        var canSell = await _permissions.HasPermissionAsync(PermissionCodes.RateSellRead, cancellationToken);
        if (!canBuy && !canSell)
        {
            return [];
        }

        var rows = await (
            from rule in _db.PricingRules.AsNoTracking()
            join version in _db.RateVersions.AsNoTracking() on rule.RateVersionId equals version.Id
            join card in _db.RateCards.AsNoTracking() on version.RateCardId equals card.Id
            select new
            {
                card.Id,
                card.Code,
                card.Name,
                card.PartyType,
                VersionId = version.Id,
                version.VersionNo,
                version.Status,
                RuleId = rule.Id,
                rule.CalcMethod
            }).ToListAsync(cancellationToken);

        var matrixRuleIds = rows
            .Where(r => RateDeclarationPolicy.IsMatrixMethod(r.CalcMethod))
            .Select(r => r.RuleId)
            .Distinct()
            .ToList();
        var bandCounts = matrixRuleIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _db.RateBreaks.AsNoTracking()
                .Where(b => matrixRuleIds.Contains(b.PricingRuleId))
                .GroupBy(b => b.PricingRuleId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return rows
            .GroupBy(r => r.VersionId)
            .Select(g =>
            {
                var basic = g.Where(x => !RateDeclarationPolicy.IsMatrixMethod(x.CalcMethod)).ToList();
                var matrix = g.Where(x => RateDeclarationPolicy.IsMatrixMethod(x.CalcMethod)).ToList();
                if (basic.Count == 0 || matrix.Count == 0)
                {
                    return null;
                }

                var first = g.First();
                var bands = matrix.Sum(x => bandCounts.GetValueOrDefault(x.RuleId));
                return new MixedDeclarationRateCardDto(
                    first.Id,
                    first.Code,
                    first.Name,
                    first.PartyType,
                    first.VersionNo,
                    basic.Count,
                    bands,
                    first.Status);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .Where(x => canBuy && canSell
                || (canBuy && x.PartyType == "vendor")
                || (canSell && x.PartyType == "customer"))
            .OrderBy(x => x.Code)
            .ThenBy(x => x.VersionNumber)
            .ToList();
    }
}
