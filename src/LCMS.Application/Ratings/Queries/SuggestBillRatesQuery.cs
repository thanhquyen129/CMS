using LCMS.Application.Abstractions;
using LCMS.Application.Ratings;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings.Queries;

public sealed record LegRateOptionDto(
    Guid Id,
    string LegNo,
    Guid ShipmentId,
    string ShipmentNo,
    string? OriginCode,
    string? DestinationCode,
    string? TransportMode,
    string? RouteCode,
    string? ServiceTypeCode,
    int LinkedBillCount,
    string? CommodityCode = null);

public sealed record VendorRateOptionDto(Guid Id, string Code, string Name);

public sealed record BillRateSuggestionDto(
    Guid BillId,
    string? RouteCode,
    string? TransportMode,
    BillRateSideDto Buy,
    BillRateSideDto Sell,
    string ContextType = RatingContextTypes.Bill,
    Guid? LegId = null,
    Guid? VendorPartyId = null,
    IReadOnlyList<LegRateOptionDto>? Legs = null,
    IReadOnlyList<VendorRateOptionDto>? Vendors = null,
    Guid? ActiveRatingId = null,
    decimal? ActiveRatingQuantity = null,
    decimal? ActiveRatingWeight = null);

public sealed record SuggestBillRatesQuery(
    Guid BillId,
    DateTimeOffset? RateDate = null,
    string? ContextType = null,
    Guid? LegId = null,
    Guid? VendorPartyId = null,
    Guid? RateVersionId = null) : IRequest<BillRateSuggestionDto>;

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
            c.Id, c.Code, c.Name, c.PartyType, c.IsActive, c.RouteCode, c.TransportMode, c.CurrencyCode, c.SupplierPartyId)).ToList();
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
        var legs = await LoadLegsAsync(bill.Id, cancellationToken);
        var contextType = RatingContextTypes.Normalize(request.ContextType);
        var buy = BillRateSuggestion.Classify(buyCards, versions, rules, context, rateDate);
        Guid? selectedLeg = null;
        Guid? selectedVendor = null;
        IReadOnlyList<VendorRateOptionDto> vendors = [];
        if (contextType == RatingContextTypes.Leg)
        {
            if (request.LegId is not Guid legId)
            {
                buy = EmptyBuy("Chọn chặng liên kết với Bill.");
            }
            else
            {
                var leg = legs.FirstOrDefault(l => l.Id == legId)
                    ?? throw new ConflictAppException("Chặng không liên kết với Bill này.");
                selectedLeg = leg.Id;
                var legContext = new BillRateMatchContext(
                    leg.RouteCode,
                    leg.TransportMode,
                    leg.ServiceTypeCode,
                    leg.OriginCode,
                    leg.DestinationCode,
                    leg.CommodityCode);
                var withVendor = buyCards.Where(c => c.SupplierPartyId is not null).ToList();
                var preview = BillRateSuggestion.Classify(withVendor, versions, rules, legContext, rateDate);
                var selectableCards = preview.Options.Where(o => o.Selectable).Select(o => o.RateCardId).ToHashSet();
                var vendorIds = withVendor
                    .Where(c => selectableCards.Contains(c.Id))
                    .Select(c => c.SupplierPartyId!.Value)
                    .Distinct()
                    .ToList();
                vendors = await _db.BusinessParties.AsNoTracking()
                    .Where(p => vendorIds.Contains(p.Id))
                    .OrderBy(p => p.Name)
                    .Select(p => new VendorRateOptionDto(p.Id, p.Code, p.Name))
                    .ToListAsync(cancellationToken);
                if (request.VendorPartyId is Guid vendorId)
                {
                    if (!vendorIds.Contains(vendorId))
                    {
                        buy = EmptyBuy("Vendor không có bảng giá mua phù hợp với chặng, tuyến hoặc ngày hiệu lực.");
                    }
                    else
                    {
                        selectedVendor = vendorId;
                        buy = BillRateSuggestion.Classify(
                            withVendor.Where(c => c.SupplierPartyId == vendorId),
                            versions,
                            rules,
                            legContext,
                            rateDate);
                    }
                }
                else if (vendorIds.Count == 1)
                {
                    selectedVendor = vendorIds[0];
                    buy = BillRateSuggestion.Classify(
                        withVendor.Where(c => c.SupplierPartyId == selectedVendor),
                        versions,
                        rules,
                        legContext,
                        rateDate);
                }
                else if (vendorIds.Count == 0)
                {
                    buy = preview.Options.Count == 0
                        ? EmptyBuy("Không có bảng giá mua đã phát hành, còn hiệu lực và gắn vendor cho chặng này.")
                        : preview with { Notice = "Không có bảng giá mua phù hợp với chặng này.", SelectedVersionId = null, Ambiguous = true };
                }
                else
                {
                    buy = EmptyBuy("Có nhiều vendor. Chọn vendor trước khi chọn bảng giá.");
                }
            }
        }

        Guid? activeRatingId = null;
        decimal? activeQuantity = null;
        decimal? activeWeight = null;
        if (contextType == RatingContextTypes.Leg
            && selectedLeg is Guid activeLeg
            && selectedVendor is Guid activeVendor
            && request.RateVersionId is Guid activeVersion)
        {
            var activeRows = await _db.Ratings.AsNoTracking()
                .Where(r => r.ContextType == RatingContextTypes.Leg
                            && r.TransportLegId == activeLeg
                            && r.VendorPartyId == activeVendor
                            && r.RateVersionId == activeVersion
                            && r.Status == RatingStatuses.Completed)
                .ToListAsync(cancellationToken);
            var active = activeRows.OrderByDescending(r => r.RatedAt).FirstOrDefault();
            if (active is not null)
            {
                activeRatingId = active.Id;
                activeQuantity = active.Quantity;
                activeWeight = active.Weight;
            }
        }

        return new BillRateSuggestionDto(
            bill.Id,
            bill.RouteCode,
            bill.TransportMode,
            buy,
            BillRateSuggestion.Classify(sellCards, versions, rules, context, rateDate),
            contextType,
            selectedLeg,
            selectedVendor,
            legs,
            vendors,
            activeRatingId,
            activeQuantity,
            activeWeight);
    }

    private async Task<IReadOnlyList<LegRateOptionDto>> LoadLegsAsync(Guid billId, CancellationToken cancellationToken)
    {
        var legIds = await _db.BillLegLinks.AsNoTracking()
            .Where(l => l.BillId == billId)
            .Select(l => l.TransportLegId)
            .ToListAsync(cancellationToken);
        if (legIds.Count == 0)
        {
            return [];
        }

        var legs = await _db.TransportLegs.AsNoTracking()
            .Where(l => legIds.Contains(l.Id) && l.IsActive)
            .OrderBy(l => l.SequenceNo)
            .ThenBy(l => l.LegNo)
            .ToListAsync(cancellationToken);
        var shipmentIds = legs.Select(l => l.ShipmentId).Distinct().ToList();
        var shipments = await _db.Shipments.AsNoTracking()
            .Where(s => shipmentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);
        var counts = await _db.BillLegLinks.AsNoTracking()
            .Where(l => legIds.Contains(l.TransportLegId))
            .GroupBy(l => l.TransportLegId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var countByLeg = counts.ToDictionary(x => x.Key, x => x.Count);
        var commodityIds = legs
            .Select(l => l.CommodityTypeId ?? (shipments.TryGetValue(l.ShipmentId, out var shipment) ? shipment.CommodityTypeId : null))
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var commodityCodes = commodityIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.CommodityTypes.AsNoTracking()
                .Where(c => commodityIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
        return legs.Select(l =>
        {
            shipments.TryGetValue(l.ShipmentId, out var shipment);
            countByLeg.TryGetValue(l.Id, out var linked);
            var commodityId = l.CommodityTypeId ?? shipment?.CommodityTypeId;
            return new LegRateOptionDto(
                l.Id,
                l.LegNo,
                l.ShipmentId,
                shipment?.ShipmentNo ?? "",
                l.OriginCode ?? shipment?.OriginCode,
                l.DestinationCode ?? shipment?.DestinationCode,
                ShipmentModePolicy.LegRatingMode(l.TransportMode, shipment?.TransportMode, null),
                l.RouteCode ?? shipment?.RouteCode,
                l.ServiceTypeCode ?? shipment?.ServiceTypeCode,
                linked,
                commodityId is Guid id && commodityCodes.TryGetValue(id, out var code) ? code : null);
        }).ToList();
    }

    private static BillRateSideDto EmptyBuy(string notice) =>
        new(BillRateNotices.NoMatchNotice, null, notice, true, null, []);
}
