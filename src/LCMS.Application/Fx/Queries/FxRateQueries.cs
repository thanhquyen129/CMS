using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Fx.Queries;

public sealed record FxRateDto(
    Guid Id,
    string FromCurrencyCode,
    string ToCurrencyCode,
    DateOnly RateDate,
    decimal Rate,
    string Source,
    int Version,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record ListFxRatesQuery(
    string? FromCurrencyCode,
    string? ToCurrencyCode,
    DateOnly? FromDate,
    DateOnly? ToDate) : IRequest<IReadOnlyList<FxRateDto>>;

public sealed class ListFxRatesQueryHandler : IRequestHandler<ListFxRatesQuery, IReadOnlyList<FxRateDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListFxRatesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<FxRateDto>> Handle(
        ListFxRatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var q = _db.FxRates.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.FromCurrencyCode))
        {
            var from = request.FromCurrencyCode.Trim().ToUpperInvariant();
            q = q.Where(r => r.FromCurrencyCode == from);
        }

        if (!string.IsNullOrWhiteSpace(request.ToCurrencyCode))
        {
            var to = request.ToCurrencyCode.Trim().ToUpperInvariant();
            q = q.Where(r => r.ToCurrencyCode == to);
        }

        if (request.FromDate.HasValue)
        {
            q = q.Where(r => r.RateDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            q = q.Where(r => r.RateDate <= request.ToDate.Value);
        }

        return await q
            .OrderByDescending(r => r.RateDate)
            .ThenBy(r => r.FromCurrencyCode)
            .ThenByDescending(r => r.Version)
            .Select(r => new FxRateDto(
                r.Id,
                r.FromCurrencyCode,
                r.ToCurrencyCode,
                r.RateDate,
                r.Rate,
                r.Source,
                r.Version,
                r.Note,
                r.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed record ResolveFxRateQuery(
    string FromCurrencyCode,
    string ToCurrencyCode,
    DateOnly AsOf) : IRequest<FxRateDto?>;

public sealed class ResolveFxRateQueryHandler : IRequestHandler<ResolveFxRateQuery, FxRateDto?>
{
    private readonly IFxRateLookup _lookup;
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ResolveFxRateQueryHandler(
        IFxRateLookup lookup,
        ILcmsDbContext db,
        ITenantContext tenantContext)
    {
        _lookup = lookup;
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<FxRateDto?> Handle(ResolveFxRateQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var resolved = await _lookup.ResolveAsync(
            request.FromCurrencyCode,
            request.ToCurrencyCode,
            request.AsOf,
            cancellationToken);

        if (resolved is null)
        {
            return null;
        }

        var row = await _db.FxRates.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == resolved.FxRateId, cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new FxRateDto(
            row.Id,
            row.FromCurrencyCode,
            row.ToCurrencyCode,
            row.RateDate,
            row.Rate,
            row.Source,
            row.Version,
            row.Note,
            row.CreatedAt);
    }
}
