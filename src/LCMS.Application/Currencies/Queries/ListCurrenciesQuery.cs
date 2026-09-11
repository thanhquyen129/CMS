using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Currencies.Queries;

public sealed record CurrencyDto(
    Guid Id,
    string Code,
    string Name,
    int DecimalPlaces,
    bool IsActive);

public sealed record ListCurrenciesQuery(bool? ActiveOnly) : IRequest<IReadOnlyList<CurrencyDto>>;

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
        var query = _db.Currencies.AsNoTracking().AsQueryable();
        if (request.ActiveOnly == true)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Code)
            .Select(c => new CurrencyDto(c.Id, c.Code, c.Name, c.DecimalPlaces, c.IsActive))
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetCurrencyByCodeQuery(string Code) : IRequest<CurrencyDto>;

public sealed class GetCurrencyByCodeQueryHandler : IRequestHandler<GetCurrencyByCodeQuery, CurrencyDto>
{
    private readonly ILcmsDbContext _db;

    public GetCurrencyByCodeQueryHandler(ILcmsDbContext db)
    {
        _db = db;
    }

    public async Task<CurrencyDto> Handle(GetCurrencyByCodeQuery request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var currency = await _db.Currencies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == code, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy tiền tệ.");

        return new CurrencyDto(currency.Id, currency.Code, currency.Name, currency.DecimalPlaces, currency.IsActive);
    }
}
