using LCMS.Application.Ratings;
using LCMS.Application.Ratings.Queries;

namespace LCMS.Api.Tests;

public sealed class BillRateSuggestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 2, 54, 0, TimeSpan.Zero);

    [Fact]
    public void Active_card_with_only_draft_is_not_ready()
    {
        var card = Card(active: true);
        var draft = Version(card.Id, "draft", null, null);
        var side = Classify([card], [draft], [], Ctx("SGN-HAN"));

        Assert.Equal(BillRateNotices.NoPublished, side.PublicationLabel);
        Assert.Equal(BillRateNotices.NoPublishedNotice, side.Notice);
        Assert.Null(side.ReadyLabel);
        Assert.Empty(side.Options);
    }

    [Fact]
    public void Published_in_date_and_route_is_selectable()
    {
        var card = Card(active: true, route: "SGN-HAN", mode: "air");
        var version = Version(card.Id, "published", Now.AddDays(-1), Now.AddDays(10));
        var side = Classify([card], [version], [Open(version.Id)], Ctx("SGN-HAN"));

        Assert.Equal(BillRateNotices.ReadyForBill, side.ReadyLabel);
        Assert.Equal(version.Id, side.SelectedVersionId);
        Assert.True(side.Options.Single().Selectable);
    }

    [Fact]
    public void Published_wrong_route_is_not_selectable()
    {
        var card = Card(active: true, route: "SGN-DAD", mode: "air");
        var version = Version(card.Id, "published", Now.AddDays(-1), null);
        var side = Classify([card], [version], [Open(version.Id)], Ctx("SGN-HAN"));

        Assert.False(side.Options.Single().Selectable);
        Assert.Equal(BillRateNotices.RouteMismatch, side.Options.Single().Reason);
        Assert.Contains(BillRateNotices.NoMatchNotice, side.Notice);
        Assert.Contains(BillRateNotices.RouteMismatch, side.Notice);
    }

    [Fact]
    public void Rule_route_mismatch_blocks_even_when_card_has_no_route()
    {
        var card = Card(active: true);
        var version = Version(card.Id, "published", null, null);
        var side = Classify([card], [version], [OnRoute(version.Id, "SGN-DAD")], Ctx("SGN-HAN"));

        Assert.Equal(BillRateNotices.RouteMismatch, side.Options.Single().Reason);
        Assert.False(side.Options.Single().Selectable);
    }

    [Fact]
    public void Rule_route_without_bill_route_is_missing_data()
    {
        var card = Card(active: true);
        var version = Version(card.Id, "published", null, null);
        var side = Classify([card], [version], [OnRoute(version.Id, "SGN-HAN")], Ctx(null, "air"));

        Assert.Equal(BillRateNotices.MissingBillNotice, side.Notice);
        Assert.Null(side.ReadyLabel);
    }

    [Fact]
    public void Published_outside_date_is_not_selectable()
    {
        var card = Card(active: true, route: "SGN-HAN");
        var version = Version(card.Id, "published", Now.AddDays(2), null);
        var side = Classify([card], [version], [Open(version.Id)], Ctx("SGN-HAN"));

        Assert.Equal(BillRateNotices.OutsideDate, side.Options.Single().Reason);
        Assert.False(side.Options.Single().Selectable);
    }

    [Fact]
    public void Retired_card_cannot_be_selected()
    {
        var card = Card(active: false, route: "SGN-HAN");
        var version = Version(card.Id, "published", Now.AddDays(-1), null);
        var side = Classify([card], [version], [Open(version.Id)], Ctx("SGN-HAN"));

        Assert.Equal(BillRateNotices.RetiredShort, side.Options.Single().Reason);
        Assert.Null(side.SelectedVersionId);
    }

    [Fact]
    public void Several_equal_matches_are_not_auto_selected()
    {
        var a = Card(active: true, code: "A");
        var b = Card(active: true, code: "B");
        var va = Version(a.Id, "published", null, null);
        var vb = Version(b.Id, "published", null, null);
        var side = Classify([a, b], [va, vb], [Open(va.Id), Open(vb.Id)], Ctx(null, null));

        Assert.True(side.Ambiguous);
        Assert.Null(side.SelectedVersionId);
        Assert.Equal(BillRateNotices.Choose, side.Notice);
        Assert.Equal(2, side.Options.Count(o => o.Selectable));
    }

    [Fact]
    public void Missing_bill_route_is_not_called_a_mismatch()
    {
        var card = Card(active: true, route: "SGN-HAN");
        var version = Version(card.Id, "published", null, null);
        var side = Classify([card], [version], [Open(version.Id)], Ctx(null));

        Assert.Equal(BillRateNotices.MissingBillNotice, side.Notice);
        Assert.Null(side.ReadyLabel);
    }

    [Fact]
    public void Buy_and_sell_are_classified_separately()
    {
        var buy = Card(active: true, code: "BUY", party: "vendor");
        var sell = Card(active: true, code: "SELL", party: "customer");
        var versions = new[] { Version(buy.Id, "draft", null, null) };
        var buySide = Classify([buy], versions, [], Ctx(null, null));
        var sellSide = Classify([sell], versions, [], Ctx(null, null));

        Assert.Equal(BillRateNotices.NoPublishedNotice, buySide.Notice);
        Assert.Equal(BillRateNotices.NoPublishedNotice, sellSide.Notice);
    }

    [Fact]
    public void Same_currency_margin_uses_only_the_two_ratings()
    {
        var margin = RatingPairMargin.Compute("VND", ("VND", 1000m), ("VND", 1500m), _ => null);

        Assert.False(margin.FxMissing);
        Assert.Equal(500m, margin.ReportingProfit);
    }

    [Fact]
    public void Missing_fx_does_not_invent_a_profit()
    {
        var margin = RatingPairMargin.Compute("VND", ("USD", 10m), ("VND", 1000m), _ => null);

        Assert.True(margin.FxMissing);
        Assert.Null(margin.ReportingProfit);
    }

    [Fact]
    public void One_side_does_not_show_profit()
    {
        var margin = RatingPairMargin.Compute("VND", ("VND", 1000m), null, _ => 1m);

        Assert.Null(margin.ReportingProfit);
        Assert.False(margin.FxMissing);
        Assert.Contains("một chiều", margin.Notice, StringComparison.OrdinalIgnoreCase);
    }

    private static BillRateSideDto Classify(
        IEnumerable<RateSuggestionCard> cards,
        IEnumerable<RateSuggestionVersion> versions,
        IEnumerable<RateSuggestionRule> rules,
        BillRateMatchContext bill) =>
        BillRateSuggestion.Classify(cards, versions, rules, bill, Now);

    private static BillRateMatchContext Ctx(string? route, string? mode = "air") =>
        new(route, mode, null, null, null, null);

    private static RateSuggestionRule Open(Guid versionId) =>
        new(versionId, "FREIGHT", "Cước", null, null, null, null, null, null, null, null);

    private static RateSuggestionRule OnRoute(Guid versionId, string route) =>
        new(versionId, "FREIGHT", "Cước", null, null, null, route, null, null, null, null);

    private static RateSuggestionCard Card(
        bool active,
        string? route = null,
        string? mode = null,
        string code = "RC",
        string party = "vendor") =>
        new(Guid.NewGuid(), code, code, party, active, route, mode, "VND");

    private static RateSuggestionVersion Version(Guid cardId, string status, DateTimeOffset? from, DateTimeOffset? to) =>
        new(Guid.NewGuid(), cardId, 1, status, from, to);
}
