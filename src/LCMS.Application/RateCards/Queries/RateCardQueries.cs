using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Common.Paging;
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
    string? CarrierName = null);

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

        return ToDto(card);
    }

    internal static RateCardDto ToDto(Domain.Entities.RateCard card) =>
        new(card.Id, card.Code, card.Name, card.PartyType, card.CurrencyCode, card.Description, card.IsActive, card.CreatedAt, card.TransportMode, card.RouteCode, card.CarrierName);
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
            var mode = request.TransportMode.Trim().ToLowerInvariant();
            query = query.Where(r => r.TransportMode != null && r.TransportMode.ToLower() == mode);
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
                r.Id, r.Code, r.Name, r.PartyType, r.CurrencyCode, r.Description, r.IsActive, r.CreatedAt, r.TransportMode, r.RouteCode, r.CarrierName))
            .ToListAsync(cancellationToken);

        return new PagedResult<RateCardDto>(
            items,
            applyPaging ? page : 1,
            applyPaging ? pageSize : totalCount,
            totalCount);
    }
}
