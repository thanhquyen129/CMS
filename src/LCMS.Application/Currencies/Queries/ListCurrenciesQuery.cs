using LCMS.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Currencies.Queries;

public sealed record CurrencyDto(
    Guid Id,
    string Code,
    string Name,
    int DecimalPlaces,
    bool IsActive);

public sealed record ListCurrenciesQuery : IRequest<IReadOnlyList<CurrencyDto>>;

public sealed class ListCurrenciesQueryHandler : IRequestHandler<ListCurrenciesQuery, IReadOnlyList<CurrencyDto>>
{
    private readonly ILcmsDbContext _db;

    public ListCurrenciesQueryHandler(ILcmsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CurrencyDto>> Handle(
        ListCurrenciesQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.Currencies
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CurrencyDto(c.Id, c.Code, c.Name, c.DecimalPlaces, c.IsActive))
            .ToListAsync(cancellationToken);
    }
}
