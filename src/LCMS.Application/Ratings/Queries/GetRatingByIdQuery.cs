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
    string CurrencyCode);

public sealed record RatingDto(
    Guid Id,
    Guid BillId,
    Guid RateVersionId,
    DateTimeOffset RatedAt,
    string CurrencyCode,
    decimal TotalAmount,
    decimal Quantity,
    string Status,
    IReadOnlyList<RatingDetailDto> Details);

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
                d.CurrencyCode))
            .ToListAsync(cancellationToken);

        return new RatingDto(
            rating.Id,
            rating.BillId,
            rating.RateVersionId,
            rating.RatedAt,
            rating.CurrencyCode,
            rating.TotalAmount,
            rating.Quantity,
            rating.Status,
            details);
    }
}
