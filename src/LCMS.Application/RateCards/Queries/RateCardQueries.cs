using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
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
    DateTimeOffset CreatedAt);

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
        new(card.Id, card.Code, card.Name, card.PartyType, card.CurrencyCode, card.Description, card.IsActive, card.CreatedAt);
}

public sealed record ListRateCardsQuery : IRequest<IReadOnlyList<RateCardDto>>;

public sealed class ListRateCardsQueryHandler : IRequestHandler<ListRateCardsQuery, IReadOnlyList<RateCardDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListRateCardsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RateCardDto>> Handle(ListRateCardsQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        return await _db.RateCards
            .AsNoTracking()
            .OrderBy(r => r.Code)
            .Select(r => new RateCardDto(
                r.Id, r.Code, r.Name, r.PartyType, r.CurrencyCode, r.Description, r.IsActive, r.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
