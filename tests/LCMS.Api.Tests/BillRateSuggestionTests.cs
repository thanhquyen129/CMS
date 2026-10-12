using LCMS.Application.Ratings;

namespace LCMS.Api.Tests;

public sealed class BillRateSuggestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 2, 54, 0, TimeSpan.Zero);

    [Fact]
    public void Active_card_with_only_draft_is_not_ready()
    {
        var card = Card(active: true);
        var draft = Version(card.Id, "draft", null, null);
        var side = BillRateSuggestion.Classify([card], [draft], "SGN-HAN", "air", Now);

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
        var side = BillRateSuggestion.Classify([card], [version], "SGN-HAN", "air", Now);

        Assert.Equal(BillRateNotices.ReadyForBill, side.ReadyLabel);
        Assert.Equal(version.Id, side.SelectedVersionId);
        Assert.True(side.Options.Single().Selectable);
    }

    [Fact]
    public void Published_wrong_route_is_not_selectable()
    {
        var card = Card(active: true, route: "SGN-DAD", mode: "air");
        var version = Version(card.Id, "published", Now.AddDays(-1), null);
        var side = BillRateSuggestion.Classify([card], [version], "SGN-HAN", "air", Now);

        Assert.False(side.Options.Single().Selectable);
        Assert.Equal(BillRateNotices.RouteMismatch, side.Options.Single().Reason);
        Assert.Contains(BillRateNotices.NoMatchNotice, side.Notice);
        Assert.Contains(BillRateNotices.RouteMismatch, side.Notice);
    }

    [Fact]
    public void Published_outside_date_is_not_selectable()
    {
        var card = Card(active: true, route: "SGN-HAN");
        var version = Version(card.Id, "published", Now.AddDays(2), null);
        var side = BillRateSuggestion.Classify([card], [version], "SGN-HAN", "air", Now);

        Assert.Equal(BillRateNotices.OutsideDate, side.Options.Single().Reason);
        Assert.False(side.Options.Single().Selectable);
    }

    [Fact]
    public void Retired_card_cannot_be_selected()
    {
        var card = Card(active: false, route: "SGN-HAN");
        var version = Version(card.Id, "published", Now.AddDays(-1), null);
        var side = BillRateSuggestion.Classify([card], [version], "SGN-HAN", "air", Now);

        Assert.Equal(BillRateNotices.RetiredShort, side.Options.Single().Reason);
        Assert.Null(side.SelectedVersionId);
    }

    [Fact]
    public void Several_equal_matches_are_not_auto_selected()
    {
        var a = Card(active: true, code: "A");
        var b = Card(active: true, code: "B");
        var side = BillRateSuggestion.Classify(
            [a, b],
            [Version(a.Id, "published", null, null), Version(b.Id, "published", null, null)],
            null,
            null,
            Now);

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
        var side = BillRateSuggestion.Classify([card], [version], null, "air", Now);

        Assert.Equal(BillRateNotices.MissingBillNotice, side.Notice);
        Assert.Null(side.ReadyLabel);
    }

    [Fact]
    public void Buy_and_sell_are_classified_separately()
    {
        var buy = Card(active: true, code: "BUY", party: "vendor");
        var sell = Card(active: true, code: "SELL", party: "customer");
        var versions = new[] { Version(buy.Id, "draft", null, null) };
        var buySide = BillRateSuggestion.Classify([buy], versions, null, null, Now);
        var sellSide = BillRateSuggestion.Classify([sell], versions, null, null, Now);

        Assert.Equal(BillRateNotices.NoPublishedNotice, buySide.Notice);
        Assert.Equal(BillRateNotices.NoPublishedNotice, sellSide.Notice);
    }

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
