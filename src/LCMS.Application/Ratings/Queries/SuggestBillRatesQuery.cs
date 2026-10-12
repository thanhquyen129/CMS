using LCMS.Application.Abstractions;
using LCMS.Application.Ratings;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings.Queries;

public sealed record BillRateSuggestionDto(
    Guid BillId,
    string? RouteCode,
    string? TransportMode,
    BillRateSideDto Buy,
    BillRateSideDto Sell);

public sealed record SuggestBillRatesQuery(Guid BillId, DateTimeOffset? RateDate = null) : IRequest<BillRateSuggestionDto>;

public sealed class SuggestBillRatesQueryHandler : IRequestHandler<SuggestBillRatesQuery, BillRateSuggestionDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public SuggestBillRatesQueryHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<BillRateSuggestionDto> Handle(SuggestBillRatesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy Bill.");

        var canBuy = await _permissions.HasPermissionAsync(PermissionCodes.RateBuyRead, cancellationToken);
        var canSell = await _permissions.HasPermissionAsync(PermissionCodes.RateSellRead, cancellationToken);
        var cards = await _db.RateCards.AsNoTracking().ToListAsync(cancellationToken);
        if (!canBuy)
        {
            cards = cards.Where(c => !string.Equals(c.PartyType, "vendor", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!canSell)
        {
            cards = cards.Where(c => !string.Equals(c.PartyType, "customer", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var ids = cards.Select(c => c.Id).ToList();
        var versions = await _db.RateVersions.AsNoTracking()
            .Where(v => ids.Contains(v.RateCardId))
            .Select(v => new RateSuggestionVersion(v.Id, v.RateCardId, v.VersionNo, v.Status, v.EffectiveFrom, v.EffectiveTo))
            .ToListAsync(cancellationToken);
        var versionIds = versions.Select(v => v.Id).ToList();
        var rules = await _db.PricingRules.AsNoTracking()
            .Where(r => versionIds.Contains(r.RateVersionId) && r.IsActive)
            .Select(r => new RateSuggestionRule(
                r.RateVersionId, r.Code, r.Name, r.ChargeCode, r.ServiceTypeCode, r.PartyTypeCode, r.RouteCode, r.TransportMode, r.OriginCode, r.DestinationCode, r.CommodityCode))
            .ToListAsync(cancellationToken);
        rules = rules.Where(r => !LCMS.Application.Surcharges.LegacyComponentClassifier.IsAdditionalCharge(r.Code, r.Name, r.ChargeCode)).ToList();
        string? commodity = null;
        if (bill.CommodityTypeId is Guid commodityId)
        {
            commodity = await _db.CommodityTypes.AsNoTracking()
                .Where(c => c.Id == commodityId)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var mapped = cards.Select(c => new RateSuggestionCard(
            c.Id, c.Code, c.Name, c.PartyType, c.IsActive, c.RouteCode, c.TransportMode, c.CurrencyCode)).ToList();
        var rateDate = request.RateDate ?? DateTimeOffset.UtcNow;
        var context = new BillRateMatchContext(
            bill.RouteCode,
            bill.TransportMode,
            string.IsNullOrWhiteSpace(bill.ServiceTypeCode) ? bill.BillType : bill.ServiceTypeCode,
            bill.OriginCode,
            bill.DestinationCode,
            commodity);
        var buyCards = mapped.Where(c => string.Equals(c.PartyType, "vendor", StringComparison.OrdinalIgnoreCase)).ToList();
        var sellCards = mapped.Where(c => string.Equals(c.PartyType, "customer", StringComparison.OrdinalIgnoreCase)).ToList();

        return new BillRateSuggestionDto(
            bill.Id,
            bill.RouteCode,
            bill.TransportMode,
            BillRateSuggestion.Classify(buyCards, versions, rules, context, rateDate),
            BillRateSuggestion.Classify(sellCards, versions, rules, context, rateDate));
    }
}
