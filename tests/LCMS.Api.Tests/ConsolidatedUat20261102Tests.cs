using LCMS.Application.Costs.Queries;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;

namespace LCMS.Api.Tests;

public sealed class ConsolidatedUat20261102Tests
{
    [Fact]
    public void Directed_route_treats_arrow_and_dash_as_the_same_direction()
    {
        Assert.True(RatingEngine.SameDirectedRoute("SGN-HAN", "SGN → HAN"));
        Assert.True(RatingEngine.SameDirectedRoute("sgn/han", "SGN-HAN"));
        Assert.False(RatingEngine.SameDirectedRoute("SGN-HAN", "HAN-SGN"));
    }

    [Fact]
    public void Sell_compare_does_not_price_the_reverse_route()
    {
        var cards = new[]
        {
            Card("SGN-HAN"),
            Card("HAN-SGN")
        };
        var versions = cards.Select(c => new RateSuggestionVersion(Guid.NewGuid(), c.Id, 1, "published", null, null)).ToList();
        var rules = versions.Select(v => new RateSuggestionRule(
            v.Id, "QT", "Cước", "FREIGHT", "FREIGHT", "customer",
            cards.First(c => c.Id == v.RateCardId).RouteCode,
            "AIR", "SGN", "HAN", null, false, 1m, 121m, "chargeable")).ToList();
        rules[1] = rules[1] with { OriginCode = "HAN", DestinationCode = "SGN", RouteCode = "HAN-SGN" };

        var forward = BillRateSuggestion.Classify(
            cards, versions, rules,
            new BillRateMatchContext("SGN-HAN", "AIR", "FREIGHT", "SGN", "HAN", null, 120m, 120m),
            new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
        var back = BillRateSuggestion.Classify(
            cards, versions, rules,
            new BillRateMatchContext("HAN-SGN", "AIR", "FREIGHT", "HAN", "SGN", null, 120m, 120m),
            new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("SGN-HAN", Assert.Single(forward.Options.Where(o => o.Selectable)).Code);
        Assert.Equal("HAN-SGN", Assert.Single(back.Options.Where(o => o.Selectable)).Code);
    }

    [Fact]
    public void Bidirectional_matches_only_the_reverse_pair_and_any_route_matches_other_pairs()
    {
        var directed = Rule("SGN-HAN", bidirectional: false, anyRoute: false);
        var both = Rule("SGN-HAN", bidirectional: true, anyRoute: false);
        var any = Rule("SGN-HAN", bidirectional: false, anyRoute: true);
        Assert.True(RatingEngine.DirectionMatches(directed, null, "SGN-HAN", "SGN", "HAN"));
        Assert.False(RatingEngine.DirectionMatches(directed, null, "HAN-SGN", "HAN", "SGN"));
        Assert.False(RatingEngine.DirectionMatches(directed, null, "DAD-SGN", "DAD", "SGN"));
        Assert.True(RatingEngine.DirectionMatches(both, null, "HAN-SGN", "HAN", "SGN"));
        Assert.False(RatingEngine.DirectionMatches(both, null, "DAD-SGN", "DAD", "SGN"));
        Assert.True(RatingEngine.DirectionMatches(any, null, "DAD-SGN", "DAD", "SGN"));
    }

    private static PricingRule Rule(string route, bool bidirectional, bool anyRoute) => new()
    {
        RouteCode = route,
        OriginCode = "SGN",
        DestinationCode = "HAN",
        IsBidirectional = bidirectional,
        AppliesAnyRoute = anyRoute
    };

    [Fact]
    public void Freight_and_donggoi_trace_come_from_the_stored_snapshot()
    {
        const string json = """
            {"rateCardCode":"TDNT_SGN_HAN","rateCardName":"TDNT SGN HAN","versionNo":2,"chargeable":120,"rules":[{"code":"QT0001","unitAmount":12000,"calcMethod":"unit_rate","applied_weight":120}],"surcharges":["DONGGOI v2"]}
            """;
        var freight = RatingTraceSnapshot.Read(json, "QT0001");
        Assert.Equal("TDNT_SGN_HAN", freight.RateCardCode);
        Assert.Equal(2, freight.VersionNo);
        Assert.Equal("QT0001", freight.RuleCode);
        Assert.Equal(120m, freight.ChargeableKg);
        Assert.Equal(12000m, freight.UnitRate);

        var pack = RatingTraceSnapshot.Read(json, "DONGGOI");
        Assert.Equal("DONGGOI", pack.SurchargeCode);
        Assert.Equal(2, pack.SurchargeVersionNo);
        Assert.Equal("rating", freight.RateCardCode is null ? "missing_link" : "rating");
        Assert.True(RatingTraceSnapshot.Read(null).RateCardCode is null);
    }

    [Fact]
    public void Air_casing_is_the_same_mode()
    {
        Assert.True(LCMS.Domain.Pricing.RateFieldPolicy.SameMode("AIR", "air"));
    }

    private static RateSuggestionCard Card(string route) =>
        new(Guid.NewGuid(), route, route, "customer", true, route, "AIR", "VND");
}
