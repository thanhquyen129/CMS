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
    public void Air_casing_is_the_same_mode()
    {
        Assert.True(LCMS.Domain.Pricing.RateFieldPolicy.SameMode("AIR", "air"));
    }

    private static RateSuggestionCard Card(string route) =>
        new(Guid.NewGuid(), route, route, "customer", true, route, "AIR", "VND");
}
