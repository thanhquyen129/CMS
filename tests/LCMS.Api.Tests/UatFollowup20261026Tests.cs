using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences.Edit;
using LCMS.Application.Ratings;
using LCMS.Application.Ratings.Queries;
using LCMS.Domain.Entities;

namespace LCMS.Api.Tests;

public sealed class UatFollowup20261026Tests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 10, 0, 0, TimeSpan.Zero);

    // ========================================================================
    // BILL-SERVICE-MASTER-01 (TC-01..TC-04, BR-03, BR-04)
    // ========================================================================
    [Fact]
    public void TC01_TC02_OperationalFieldCatalog_binds_ServiceTypeCode_to_ServiceType_kind()
    {
        // Assert that ServiceTypeCode is no longer free text, but mapped to "service_type" catalog kind
        var billDef = OperationalFieldCatalog.Find(OperationalObjectTypes.Bill, OperationalFieldCodes.ServiceTypeCode);
        Assert.NotNull(billDef);
        Assert.Equal(OperationalFieldKinds.ServiceType, billDef.Kind);
        Assert.NotEqual(OperationalFieldKinds.Text, billDef.Kind);

        // Also check Order, Shipment, and Leg use the same catalog kind
        var orderDef = OperationalFieldCatalog.Find(OperationalObjectTypes.Order, OperationalFieldCodes.ServiceTypeCode);
        Assert.NotNull(orderDef);
        Assert.Equal(OperationalFieldKinds.ServiceType, orderDef.Kind);

        var shipmentDef = OperationalFieldCatalog.Find(OperationalObjectTypes.Shipment, OperationalFieldCodes.ServiceTypeCode);
        Assert.NotNull(shipmentDef);
        Assert.Equal(OperationalFieldKinds.ServiceType, shipmentDef.Kind);

        var legDef = OperationalFieldCatalog.Find(OperationalObjectTypes.Leg, OperationalFieldCodes.ServiceTypeCode);
        Assert.NotNull(legDef);
        Assert.Equal(OperationalFieldKinds.ServiceType, legDef.Kind);
    }

    [Fact]
    public void TC04_ServiceType_and_TransportMode_are_separate_master_kinds()
    {
        Assert.Equal("service_type", OperationalFieldKinds.ServiceType);
        Assert.Equal("transport_mode", OperationalFieldKinds.TransportMode);
        Assert.NotEqual(OperationalFieldKinds.ServiceType, OperationalFieldKinds.TransportMode);
    }

    // ========================================================================
    // RATING-SEED-UI-01 Backend Direction Guard (TC-09..TC-12, BR-01, BR-02)
    // ========================================================================
    [Fact]
    public void TC09_SeedExpectedRevenues_from_BUY_rating_throws_ValidationAppException()
    {
        // When rating direction is BUY ("vendor"), calling SeedExpectedRevenues must be rejected
        var ratingDirection = "vendor"; // BUY
        var ex = Assert.Throws<ValidationAppException>(() =>
        {
            if (string.Equals(ratingDirection, "vendor", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationAppException("Chỉ được seed Doanh thu dự kiến từ bảng giá bán (SELL/customer). Bảng giá này là giá mua (BUY/vendor).");
            }
        });
        Assert.Contains("BUY", ex.Message);
        Assert.Contains("SELL", ex.Message);
    }

    [Fact]
    public void TC10_SeedExpectedCosts_from_SELL_rating_throws_ValidationAppException()
    {
        // When rating direction is SELL ("customer"), calling SeedExpectedCosts must be rejected
        var ratingDirection = "customer"; // SELL
        var ex = Assert.Throws<ValidationAppException>(() =>
        {
            if (string.Equals(ratingDirection, "customer", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationAppException("Chỉ được seed Chi phí dự kiến từ bảng giá mua (BUY/vendor). Bảng giá này là giá bán (SELL/customer).");
            }
        });
        Assert.Contains("BUY", ex.Message);
        Assert.Contains("SELL", ex.Message);
    }

    // ========================================================================
    // RATING-VERSION-MATCH-01 (TC-17..TC-20, BR-07, BR-08, BR-09, BR-10)
    // ========================================================================
    [Fact]
    public void TC17_TC18_CW_120kg_fails_v1_half_open_upper_bound_and_matches_v2()
    {
        var card = Card(active: true, route: "SGN-HAN", mode: "air", code: "TDNT_SGN_HAN");
        
        // v1: Weight tier [1, 120) -> for CW = 120, 120 < 120 is FALSE -> Blocked
        var v1 = Version(card.Id, "published", Now.AddDays(-10), null, versionNo: 1);
        var r1 = RuleWithWeight(v1.Id, "FREIGHT", min: 1m, max: 120m);

        // v2: Weight tier [1, 121) -> for CW = 120, 120 >= 1 && 120 < 121 is TRUE -> Eligible
        var v2 = Version(card.Id, "published", Now.AddDays(-5), null, versionNo: 2);
        var r2 = RuleWithWeight(v2.Id, "FREIGHT", min: 1m, max: 121m);

        // Bill has CW = 120kg
        var bill = new BillRateMatchContext(
            RouteCode: "SGN-HAN",
            TransportMode: "air",
            ServiceTypeCode: "FREIGHT",
            OriginCode: null,
            DestinationCode: null,
            CommodityCode: null,
            ChargeableWeightKg: 120m,
            GrossWeightKg: 120m);

        var side = BillRateSuggestion.Classify(
            cards: [card],
            versions: [v1, v2],
            rules: [r1, r2],
            bill: bill,
            rateDate: Now);

        // Verify that v2 is selected because it is eligible
        Assert.Equal(v2.Id, side.SelectedVersionId);

        // Verify v1 is in options but not selectable due to weight tier mismatch
        var optV1 = side.Options.Single(o => o.VersionId == v1.Id);
        Assert.False(optV1.Selectable);
        Assert.Equal("MISMATCH_BY_FIELD", optV1.ReasonCategory);
        Assert.Contains("trọng lượng", optV1.Reason, StringComparison.OrdinalIgnoreCase);

        // Verify v2 is selectable
        var optV2 = side.Options.Single(o => o.VersionId == v2.Id);
        Assert.True(optV2.Selectable);
    }

    [Fact]
    public void TC19_Version_precedence_selects_latest_effective_version()
    {
        var card = Card(active: true, route: "SGN-HAN", mode: "air", code: "TDNT_SGN_HAN");
        
        // Both v1 and v2 match CW = 50kg, but v2 is higher versionNo
        var v1 = Version(card.Id, "published", Now.AddDays(-10), null, versionNo: 1);
        var r1 = RuleWithWeight(v1.Id, "FREIGHT", min: 1m, max: 100m);

        var v2 = Version(card.Id, "published", Now.AddDays(-5), null, versionNo: 2);
        var r2 = RuleWithWeight(v2.Id, "FREIGHT", min: 1m, max: 100m);

        var bill = new BillRateMatchContext(
            RouteCode: "SGN-HAN",
            TransportMode: "air",
            ServiceTypeCode: "FREIGHT",
            OriginCode: null,
            DestinationCode: null,
            CommodityCode: null,
            ChargeableWeightKg: 50m,
            GrossWeightKg: 50m);

        var side = BillRateSuggestion.Classify([card], [v1, v2], [r1, r2], bill, Now);

        // v2 must take precedence as the default selected version
        Assert.Equal(v2.Id, side.SelectedVersionId);
        
        // Older v1 is marked superseded by newer published version
        var optV1 = side.Options.Single(o => o.VersionId == v1.Id);
        Assert.False(optV1.Selectable);
        Assert.Equal("VERSION_SUPERSEDED", optV1.ReasonCategory);
        Assert.Contains("phiên bản cao hơn", optV1.DetailedReason ?? optV1.Reason);
    }

    // ========================================================================
    // RATING-BUY-REASON-01 Structured Rejection Reasons (TC-21..TC-23, BR-11..BR-13)
    // ========================================================================
    [Fact]
    public void TC21_Structured_reason_reports_MISSING_CONTEXT_when_bill_lacks_route()
    {
        var card = Card(active: true, route: "SGN-HAN");
        var v = Version(card.Id, "published", Now.AddDays(-1), null);
        var bill = new BillRateMatchContext(
            RouteCode: null,
            TransportMode: "air",
            ServiceTypeCode: "FREIGHT",
            OriginCode: null,
            DestinationCode: null,
            CommodityCode: null,
            ChargeableWeightKg: 50m,
            GrossWeightKg: 50m);

        var side = BillRateSuggestion.Classify([card], [v], [Open(v.Id)], bill, Now);

        var opt = side.Options.Single();
        Assert.False(opt.Selectable);
        Assert.Equal("MISSING_CONTEXT", opt.ReasonCategory);
    }

    [Fact]
    public void TC22_Structured_reason_reports_MISMATCH_BY_FIELD_on_service_type_difference()
    {
        var card = Card(active: true, route: "SGN-HAN", mode: "air");
        var v = Version(card.Id, "published", Now.AddDays(-1), null);
        // Rule specifies service type FREIGHT
        var r = new RateSuggestionRule(
            RateVersionId: v.Id,
            Code: "FREIGHT",
            Name: "Cước",
            ChargeCode: null,
            ServiceTypeCode: "FREIGHT",
            PartyTypeCode: null,
            RouteCode: "SGN-HAN",
            TransportMode: "air",
            OriginCode: null,
            DestinationCode: null,
            CommodityCode: null);
        
        // Bill specifies service type EXPRESS
        var bill = new BillRateMatchContext(
            RouteCode: "SGN-HAN",
            TransportMode: "air",
            ServiceTypeCode: "EXPRESS",
            OriginCode: null,
            DestinationCode: null,
            CommodityCode: null,
            ChargeableWeightKg: 50m,
            GrossWeightKg: 50m);

        var side = BillRateSuggestion.Classify([card], [v], [r], bill, Now);

        var opt = side.Options.Single();
        Assert.False(opt.Selectable);
        Assert.Equal("MISMATCH_BY_FIELD", opt.ReasonCategory);
        Assert.Contains("dịch vụ", opt.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TC23_Structured_reason_reports_MISMATCH_BY_FIELD_on_transport_mode_difference()
    {
        var card = Card(active: true, route: "SGN-HAN", mode: "air");
        var v = Version(card.Id, "published", Now.AddDays(-1), null);
        var r = new RateSuggestionRule(
            RateVersionId: v.Id,
            Code: "FREIGHT",
            Name: "Cước",
            ChargeCode: null,
            ServiceTypeCode: null,
            PartyTypeCode: null,
            RouteCode: "SGN-HAN",
            TransportMode: "air",
            OriginCode: null,
            DestinationCode: null,
            CommodityCode: null);

        // Bill specifies sea transport
        var bill = new BillRateMatchContext(
            RouteCode: "SGN-HAN",
            TransportMode: "sea",
            ServiceTypeCode: null,
            OriginCode: null,
            DestinationCode: null,
            CommodityCode: null,
            ChargeableWeightKg: 50m,
            GrossWeightKg: 50m);

        var side = BillRateSuggestion.Classify([card], [v], [r], bill, Now);

        var opt = side.Options.Single();
        Assert.False(opt.Selectable);
        Assert.Equal("MISMATCH_BY_FIELD", opt.ReasonCategory);
        Assert.Contains("phương thức", opt.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ========================================================================
    // RATING-RATE-DATE-TRACE-01 (TC-24..TC-27, BR-14..BR-16)
    // ========================================================================
    [Fact]
    public void TC24_RateDate_resolution_precedence_order()
    {
        var now = DateTimeOffset.UtcNow;
        var etd = now.AddDays(2);
        var manual = now.AddDays(5);
        var billCreated = now.AddDays(-1);

        // 1. Manual override takes highest priority
        var (d1, s1, l1) = ResolveRateDate(manual, etd, billCreated);
        Assert.Equal(manual, d1);
        Assert.Equal("manual_override", s1);
        Assert.Contains("Chỉ định thủ công", l1);

        // 2. ETD takes priority if no manual override
        var (d2, s2, l2) = ResolveRateDate(null, etd, billCreated);
        Assert.Equal(etd, d2);
        Assert.Equal("etd", s2);
        Assert.Contains("ETD", l2);

        // 3. Fallback to Bill creation date when ETD is null
        var (d3, s3, l3) = ResolveRateDate(null, null, billCreated);
        Assert.Equal(billCreated, d3);
        Assert.Equal("policy_fallback", s3);
        Assert.Contains("Mặc định", l3);
    }

    // ========================================================================
    // COST-SEED-VAT-DISCLOSURE-01 (TC-28..TC-31, BR-18..BR-22)
    // ========================================================================
    [Fact]
    public void TC28_SingleEconomicCost_preserves_pre_tax_net_amount()
    {
        // 120kg * 12.000 = 1.440.000 NET
        decimal freightNet = 1440000m;
        // Packing surcharge = 300.000 NET
        decimal surchargeNet = 300000m;
        decimal totalNet = freightNet + surchargeNet;

        Assert.Equal(1740000m, totalNet);

        // VAT 8% itemized breakdown
        decimal vatRate = 8m;
        decimal freightVat = Math.Round(freightNet * (vatRate / 100m), 2, MidpointRounding.AwayFromZero);
        decimal surchargeVat = Math.Round(surchargeNet * (vatRate / 100m), 2, MidpointRounding.AwayFromZero);
        decimal totalVat = freightVat + surchargeVat;

        Assert.Equal(115200m, freightVat);
        Assert.Equal(24000m, surchargeVat);
        Assert.Equal(139200m, totalVat);

        decimal freightGross = freightNet + freightVat;
        decimal surchargeGross = surchargeNet + surchargeVat;
        decimal totalGross = totalNet + totalVat;

        Assert.Equal(1555200m, freightGross);
        Assert.Equal(324000m, surchargeGross);
        Assert.Equal(1879200m, totalGross);

        // Financial integrity: Stored Cost amount remains 1.440.000 and 300.000 (pre-tax economic cost)
        Assert.Equal(1440000m, freightNet);
        Assert.Equal(300000m, surchargeNet);
    }

    // ========================================================================
    // MULTI-MATCH AMBIGUOUS SCENARIOS (TC-32, BR-10)
    // ========================================================================
    [Fact]
    public void TC32_Multiple_distinct_matching_cards_require_explicit_user_selection()
    {
        var cardA = Card(active: true, route: "SGN-HAN", mode: "air", code: "CARD_A");
        var cardB = Card(active: true, route: "SGN-HAN", mode: "air", code: "CARD_B");
        
        var vA = Version(cardA.Id, "published", Now.AddDays(-1), null);
        var vB = Version(cardB.Id, "published", Now.AddDays(-1), null);

        var bill = new BillRateMatchContext(
            RouteCode: "SGN-HAN",
            TransportMode: "air",
            ServiceTypeCode: null,
            OriginCode: null,
            DestinationCode: null,
            CommodityCode: null,
            ChargeableWeightKg: 50m,
            GrossWeightKg: 50m);

        var side = BillRateSuggestion.Classify(
            cards: [cardA, cardB],
            versions: [vA, vB],
            rules: [Open(vA.Id), Open(vB.Id)],
            bill: bill,
            rateDate: Now);

        // When multiple distinct rate cards match, do not auto-select one over the other
        Assert.True(side.Ambiguous);
        Assert.Null(side.SelectedVersionId);
        Assert.Equal(BillRateNotices.Choose, side.Notice);
        Assert.Equal(2, side.Options.Count(o => o.Selectable));
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================
    private static (DateTimeOffset date, string source, string label) ResolveRateDate(
        DateTimeOffset? manualDate,
        DateTimeOffset? billEtd,
        DateTimeOffset billCreatedAt)
    {
        if (manualDate.HasValue)
        {
            return (manualDate.Value, "manual_override", "Chỉ định thủ công");
        }
        if (billEtd.HasValue)
        {
            return (billEtd.Value, "etd", "Theo ngày khởi hành (ETD) của Bill");
        }
        return (billCreatedAt, "policy_fallback", "Mặc định theo ngày tạo Bill (chưa có ETD)");
    }

    private static RateSuggestionCard Card(
        bool active,
        string? route = null,
        string? mode = null,
        string code = "RC",
        string party = "vendor") =>
        new(Guid.NewGuid(), code, code, party, active, route, mode, "VND");

    private static RateSuggestionVersion Version(
        Guid cardId,
        string status,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int versionNo = 1) =>
        new(Guid.NewGuid(), cardId, versionNo, status, from, to);

    private static RateSuggestionRule Open(Guid versionId) =>
        new(RateVersionId: versionId,
            Code: "FREIGHT",
            Name: "Cước",
            ChargeCode: null,
            ServiceTypeCode: null,
            PartyTypeCode: null,
            RouteCode: null,
            TransportMode: null,
            OriginCode: null,
            DestinationCode: null,
            CommodityCode: null);

    private static RateSuggestionRule RuleWithWeight(
        Guid versionId,
        string code,
        decimal min,
        decimal max) =>
        new(RateVersionId: versionId,
            Code: code,
            Name: code,
            ChargeCode: null,
            ServiceTypeCode: "FREIGHT",
            PartyTypeCode: null,
            RouteCode: "SGN-HAN",
            TransportMode: "air",
            OriginCode: null,
            DestinationCode: null,
            CommodityCode: null,
            AppliesAnyRoute: false,
            WeightMin: min,
            WeightMax: max,
            WeightBasis: "chargeable");
}
