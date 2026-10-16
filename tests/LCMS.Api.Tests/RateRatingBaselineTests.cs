using LCMS.Application.Pricing;
using LCMS.Application.Ratings;
using LCMS.Application.Surcharges;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;

namespace LCMS.Api.Tests;

public sealed class RateRatingBaselineTests
{
    [Fact]
    public void Uat01_service_air_is_not_a_mode_and_stays_readable()
    {
        Assert.Equal(RateFieldPolicy.ServiceAirBlocked, RateFieldPolicy.ServiceBlock("AIR", alreadyStored: false));
        Assert.Null(RateFieldPolicy.ServiceBlock("AIR", alreadyStored: true));
        Assert.Null(RateFieldPolicy.ServiceBlock("FREIGHT", alreadyStored: false));
        Assert.NotEqual(RateFieldPolicy.ToStoredMode("AIR"), RateFieldPolicy.ToStoredMode("FREIGHT"));
    }

    [Fact]
    public void Uat02_mode_adapter_is_bidirectional_and_does_not_collapse_water_or_express()
    {
        Assert.Equal("air", RateFieldPolicy.ToStoredMode("AIR"));
        Assert.Equal("AIR", RateFieldPolicy.ToCanonicalMode("air"));
        Assert.Equal("sea", RateFieldPolicy.ToStoredMode("SEA"));
        Assert.Equal("road", RateFieldPolicy.ToStoredMode("ROAD"));
        Assert.Equal("rail", RateFieldPolicy.ToStoredMode("RAIL"));
        Assert.Equal("water", RateFieldPolicy.ToStoredMode("WATER"));
        Assert.Equal("express", RateFieldPolicy.ToStoredMode("EXPRESS"));
        Assert.NotEqual("sea", RateFieldPolicy.ToStoredMode("WATER"));
        Assert.NotEqual("air", RateFieldPolicy.ToStoredMode("EXPRESS"));
        Assert.Equal(RateFieldPolicy.WaterBlocked, RateFieldPolicy.NewModeBlock("WATER"));
        Assert.Equal(RateFieldPolicy.ExpressBlocked, RateFieldPolicy.NewModeBlock("express"));
        Assert.False(RateFieldPolicy.ExpressContextSupported);
    }

    [Fact]
    public void Uat04_route_mode_conflict_and_any_route()
    {
        Assert.Equal(RateFieldPolicy.ModeRouteConflict, RateFieldPolicy.RouteModeConflict("air", "ROAD", false));
        Assert.Null(RateFieldPolicy.RouteModeConflict("air", "AIR", false));
        Assert.Null(RateFieldPolicy.RouteModeConflict("air", "ROAD", true));
        Assert.Null(RateFieldPolicy.RouteModeConflict(null, "ROAD", false));
    }

    [Fact]
    public void Uat06_basic_code_stays_stable_when_the_name_changes()
    {
        var first = RateFieldPolicy.NextBasicCode([]);
        var again = RateFieldPolicy.NextBasicCode([first, "QT0002"]);
        Assert.Equal("QT0001", first);
        Assert.Equal("QT0003", again);
        Assert.Equal("QT0001", first);
        var afterMiddleDelete = RateFieldPolicy.NextBasicCode(["QT0001", "QT0002~retired", "QT0003"]);
        Assert.Equal("QT0002", afterMiddleDelete);
        Assert.DoesNotContain(afterMiddleDelete, new[] { "QT0001", "QT0003", "QT0002~retired" });
    }

    [Fact]
    public void Uat07_same_charge_group_picks_one_and_a_tie_is_ambiguous()
    {
        var specific = Rule("A", "FREIGHT", "SGN-FRA");
        var broad = Rule("B", "FREIGHT", null);
        var selected = RatingEngine.Select([specific, broad]);
        Assert.Equal("A", Assert.Single(selected).Code);

        var tie = Assert.Throws<LCMS.Application.Common.Exceptions.ConflictAppException>(() =>
            RatingEngine.Select([Rule("A", "FREIGHT", "SGN-FRA"), Rule("B", "FREIGHT", "SGN-FRA")]));
        Assert.Contains("AMBIGUOUS_RATE_RULE", tie.Message);
    }

    [Fact]
    public void Uat08_different_groups_both_apply()
    {
        var selected = RatingEngine.Select([Rule("A", "FREIGHT", null), Rule("B", "HANDLING", null)]);
        Assert.Equal(2, selected.Count);
    }

    [Fact]
    public void Uat09_explicit_base_rate_ignores_fuel_in_the_name()
    {
        Assert.True(LegacyComponentClassifier.IsAdditionalCharge("FUEL", "Phụ phí FUEL", null));
        Assert.False(RateChargeSelection.ExcludeFromBase("FUEL", "Phụ phí FUEL", "FREIGHT", RateLineRoles.BaseRate, explicitGroups: true));
        Assert.True(RateChargeSelection.ExcludeFromBase("FUEL", "Phụ phí FUEL", null, null, explicitGroups: false));
    }

    [Fact]
    public void Uat10_nse_remote_delivery_is_listed_and_not_doubled()
    {
        var rows = new[]
        {
            Line("NSE-REMOTE-1", 10m),
            Line("NSE-REMOTE-2", 20m),
            Line("NSE-DELIVERY-1", 30m),
            Line("NSE-DELIVERY-2", 40m),
            Line("NSE-DELIVERY-3", 50m),
        };
        var omitted = NseRemoteDeliveryPlan.Omitted(rows);
        var dry = NseRemoteDeliveryPlan.Run(omitted, baseAmount: 100m);
        Assert.Equal(5, dry.Omitted.Count);
        Assert.Equal(150m, dry.SurchargeAmount);
        Assert.Equal(250m, dry.Total);
        Assert.True(dry.PublishedUnchanged);
        Assert.False(dry.Doubled);
        Assert.Equal(10m, rows[0].Amount);
    }

    [Fact]
    public void Uat11_and_12_economic_category_uses_only_a_real_mapping()
    {
        var typeId = Guid.NewGuid();
        var mapping = new Dictionary<string, Guid>
        {
            [ChargeLineSnapshot.MapKey(ChargeTypeMappingKinds.CostType, "FREIGHT")] = typeId
        };
        Assert.Equal(typeId, ChargeLineSnapshot.MappedEconomicCharge("FREIGHT", null, "LINE", mapping));
        Assert.Null(ChargeLineSnapshot.MappedEconomicCharge(null, null, "LINE", new Dictionary<string, Guid>()));
    }

    [Fact]
    public void Uat13_basic_band_is_half_open_and_advanced_break_includes_the_upper_bound()
    {
        Assert.True(WeightBand.Contains(0m, 0m, 10m));
        Assert.False(WeightBand.Contains(10m, 0m, 10m));
        Assert.True(WeightBand.Contains(10m, 10m, 20m));
        var upper = new[]
        {
            new RateBreak { MinQuantity = 0m, MaxQuantity = 10m, UnitAmount = 2m }
        };
        var atUpper = RatingEngine.WeightBreakPivot(10m, upper, null);
        Assert.Equal(20m, atUpper.Amount);
        Assert.Contains("10", atUpper.Band);
        Assert.Throws<LCMS.Application.Common.Exceptions.ConflictAppException>(() =>
            RatingEngine.WeightBreakPivot(10.01m, upper, null));
    }

    [Fact]
    public void Uat15_publish_of_an_explicit_buy_card_needs_supplier_and_cost_type()
    {
        var blocked = RateFieldPolicy.PublishBlock(buy: true, hasSupplier: false, [new ExplicitRuleFinance("FREIGHT", "base_rate", "FREIGHT", null)]);
        Assert.Equal(RateFieldPolicy.PublishSupplierRequired, blocked);
        var missingCost = RateFieldPolicy.PublishBlock(buy: true, hasSupplier: true, [new ExplicitRuleFinance("FREIGHT", "base_rate", null, null)]);
        Assert.Equal(RateFieldPolicy.PublishCostRequired, missingCost);
        Assert.Null(RateFieldPolicy.PublishBlock(buy: false, hasSupplier: false, [new ExplicitRuleFinance("FREIGHT", "base_rate", null, "REV")]));
    }

    [Fact]
    public void Uat16_rule_mode_is_not_hidden_by_a_legacy_header_mode()
    {
        var card = new RateSuggestionCard(Guid.NewGuid(), "RC", "RC", "vendor", true, "SGN-HAN", "road", "VND");
        var version = new RateSuggestionVersion(Guid.NewGuid(), card.Id, 1, "published", null, null);
        var rule = new RateSuggestionRule(version.Id, "FRT", "Cước", "FREIGHT", null, null, "SGN-FRA", "air", null, null, null);
        var reason = BillRateSuggestion.BlockReason(
            card,
            version,
            [rule],
            new BillRateMatchContext("SGN-FRA", "air", null, null, null, null),
            DateTimeOffset.Parse("2026-10-16T00:00:00Z"));
        Assert.Null(reason);
    }

    [Fact]
    public void Uat18_historical_bill_amounts_are_not_rewritten()
    {
        var kept = RateFieldPolicy.HistoricalRating("BILL26107473", 350m, 500m, "USD");
        Assert.Equal(350m, kept.Buy);
        Assert.Equal(500m, kept.Sell);
        Assert.Equal("USD", kept.Currency);
    }

    [Fact]
    public void Null_charge_codes_still_accumulate_and_matrix_codes_do_not_truncate()
    {
        var selected = RatingEngine.Select([Rule("A", null, null), Rule("B", null, null)]);
        Assert.Equal(2, selected.Count);
        Assert.Equal(RateFieldPolicy.MatrixCodeTooLong, RateFieldPolicy.MatrixCodeError([new string('X', 65)]));
        Assert.Equal(RateFieldPolicy.MatrixCodeCollision, RateFieldPolicy.MatrixCodeError(["G1-GENERAL", "G1-GENERAL"]));
        Assert.Equal(
            RateFieldPolicy.MatrixGroupOverlap,
            RateFieldPolicy.MatrixGroupOverlapError([
                new MatrixGroupScope("SGN-FRA", false, "air", "VN", ["GENERAL"]),
                new MatrixGroupScope("SGN-FRA", false, "AIR", "VN", ["GENERAL"])
            ]));
    }

    private static PricingRule Rule(string code, string? charge, string? route) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            ChargeCode = charge,
            RouteCode = route,
            IsActive = true
        };

    private static NseRemoteDeliveryPlan.Row Line(string code, decimal amount) =>
        new("published", code, "SGN-FRA", amount, code, DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null);
}
