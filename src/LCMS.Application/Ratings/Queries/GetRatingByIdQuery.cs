using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings.Queries;

public sealed record RatingDetailDto(
    Guid Id,
    Guid? PricingRuleId,
    Guid? PricingRuleComponentId,
    string RuleCode,
    string ComponentCode,
    string ComponentName,
    string FinancialNature,
    string FinancialMaturity,
    decimal Amount,
    string CurrencyCode,
    string? FormulaText = null);

public sealed record RatingDto(
    Guid Id,
    Guid BillId,
    Guid RateVersionId,
    DateTimeOffset RatedAt,
    string CurrencyCode,
    decimal TotalAmount,
    decimal Quantity,
    decimal? Weight,
    string? ServiceTypeCode,
    string? PartyTypeCode,
    string? RouteCode,
    decimal? BaseAmount,
    string Status,
    Guid? SupersedesRatingId,
    IReadOnlyList<RatingDetailDto> Details,
    string? ContextJson = null,
    decimal? ChargeableWeightKg = null,
    string? ChargeableBasis = null,
    decimal? OriginalAmount = null,
    decimal? FxRate = null,
    string? FxSource = null,
    decimal? RoundedAmount = null);

public sealed record RatingHistoryItemDto(
    Guid Id,
    Guid BillId,
    Guid RateVersionId,
    DateTimeOffset RatedAt,
    string CurrencyCode,
    decimal TotalAmount,
    decimal Quantity,
    decimal? Weight,
    string Status,
    Guid? SupersedesRatingId);

public sealed record GetRatingByIdQuery(Guid Id) : IRequest<RatingDto>;

public sealed class GetRatingByIdQueryHandler : IRequestHandler<GetRatingByIdQuery, RatingDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetRatingByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<RatingDto> Handle(GetRatingByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var rating = await _db.Ratings
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (rating is null)
        {
            throw new NotFoundAppException("Không tìm thấy lần tính giá.");
        }

        var details = await _db.RatingDetails
            .AsNoTracking()
            .Where(d => d.RatingId == rating.Id)
            .OrderBy(d => d.RuleCode)
            .ThenBy(d => d.ComponentCode)
            .Select(d => new RatingDetailDto(
                d.Id,
                d.PricingRuleId,
                d.PricingRuleComponentId,
                d.RuleCode,
                d.ComponentCode,
                d.ComponentName,
                d.FinancialNature,
                d.FinancialMaturity,
                d.Amount,
                d.CurrencyCode,
                d.FormulaText))
            .ToListAsync(cancellationToken);

        return new RatingDto(
            rating.Id,
            rating.BillId,
            rating.RateVersionId,
            rating.RatedAt,
            rating.CurrencyCode,
            rating.TotalAmount,
            rating.Quantity,
            rating.Weight,
            rating.ServiceTypeCode,
            rating.PartyTypeCode,
            rating.RouteCode,
            rating.BaseAmount,
            rating.Status,
            rating.SupersedesRatingId,
            details,
            rating.ContextJson,
            rating.ChargeableWeightKg,
            rating.ChargeableBasis,
            rating.OriginalAmount,
            rating.FxRate,
            rating.FxSource,
            rating.RoundedAmount);
    }
}

public sealed record ListRatingsByBillQuery(Guid BillId) : IRequest<IReadOnlyList<RatingHistoryItemDto>>;

public sealed class ListRatingsByBillQueryHandler
    : IRequestHandler<ListRatingsByBillQuery, IReadOnlyList<RatingHistoryItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListRatingsByBillQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RatingHistoryItemDto>> Handle(
        ListRatingsByBillQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var billExists = await _db.Bills.AnyAsync(b => b.Id == request.BillId, cancellationToken);
        if (!billExists)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        var items = await _db.Ratings
            .AsNoTracking()
            .Where(r => r.BillId == request.BillId)
            .Select(r => new RatingHistoryItemDto(
                r.Id,
                r.BillId,
                r.RateVersionId,
                r.RatedAt,
                r.CurrencyCode,
                r.TotalAmount,
                r.Quantity,
                r.Weight,
                r.Status,
                r.SupersedesRatingId))
            .ToListAsync(cancellationToken);

        // Client-side order: SQLite cannot ORDER BY DateTimeOffset (tests); Postgres OK either way.
        return items
            .OrderByDescending(r => r.RatedAt)
            .ThenByDescending(r => r.Id)
            .ToList();
    }
}
