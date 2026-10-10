using LCMS.Application.Profitability;
using LCMS.Domain.Pricing;

namespace LCMS.Api.Tests;

public sealed class ChargeProfitVatTests
{
    [Fact]
    public void Declared_vat_10_percent_splits_net_without_using_gross_as_economic()
    {
        var buy = DeclaredVat.Split(150_000m, 10m);
        var sell = DeclaredVat.Split(100_000m, 10m);

        Assert.Equal(150_000m, buy.Net);
        Assert.Equal(15_000m, buy.Vat);
        Assert.Equal(165_000m, buy.Gross);
        Assert.Equal(10_000m, sell.Vat);
        Assert.Equal(110_000m, sell.Gross);
        Assert.Equal(-50_000m, sell.Net - buy.Net);
    }

    [Fact]
    public void Missing_vat_is_not_zero()
    {
        var undeclared = DeclaredVat.Split(150_000m, null);
        var zero = DeclaredVat.Split(150_000m, 0m);

        Assert.Null(undeclared.Vat);
        Assert.Null(undeclared.Gross);
        Assert.Equal(0m, zero.Vat);
        Assert.Equal(150_000m, zero.Gross);
        Assert.False(DeclaredVat.IsValid(100.01m));
        Assert.True(DeclaredVat.IsValid(null));
        Assert.True(DeclaredVat.IsValid(0m));
    }

    [Fact]
    public void Packaging_loss_warns_even_when_another_charge_is_profitable()
    {
        var rows = ChargeProfitability.Compose("expected", "VND",
        [
            Line(Packaging, "cost", 150_000m, "expected"),
            Line(Packaging, "revenue", 100_000m, "expected"),
            Line(Pickup, "cost", 80_000m, "expected"),
            Line(Pickup, "revenue", 120_000m, "expected")
        ]);

        var packaging = rows.Rows.Single(r => r.EconomicChargeTypeId == Packaging);
        var pickup = rows.Rows.Single(r => r.EconomicChargeTypeId == Pickup);
        Assert.Equal(-50_000m, packaging.ProfitReporting);
        Assert.True(packaging.NegativeFlag);
        Assert.Equal(40_000m, pickup.ProfitReporting);
        Assert.False(pickup.NegativeFlag);
    }

    [Fact]
    public void Mixed_maturity_is_not_called_an_actual_loss()
    {
        var rows = ChargeProfitability.Compose("best", "VND",
        [
            Line(Packaging, "revenue", expected: 100_000m, peak: "expected"),
            Line(Packaging, "cost", actual: 150_000m, peak: "actual")
        ]);

        var row = Assert.Single(rows.Rows);
        Assert.Equal(ChargeProfitability.Mixed, row.ComparisonKind);
        Assert.False(row.NegativeFlag);
        Assert.Contains("hỗn hợp", row.Note);
    }

    [Fact]
    public void Missing_side_does_not_conclude_a_loss()
    {
        var rows = ChargeProfitability.Compose("actual", "VND",
        [
            Line(Packaging, "cost", actual: 150_000m, peak: "actual")
        ]);

        var row = Assert.Single(rows.Rows);
        Assert.Equal(ChargeProfitability.Incomplete, row.DataCompleteness);
        Assert.False(row.NegativeFlag);
        Assert.Null(row.ProfitReporting);
    }

    [Fact]
    public void Unmapped_codes_are_not_paired()
    {
        var rows = ChargeProfitability.Compose("expected", "VND",
        [
            Line(null, "cost", 150_000m, "expected", "PACKAGING"),
            Line(null, "revenue", 100_000m, "expected", "PACKAGING")
        ]);

        Assert.Equal(2, rows.Rows.Count);
        Assert.All(rows.Rows, r => Assert.Equal(ChargeProfitability.Incomplete, r.DataCompleteness));
    }

    [Fact]
    public void Order_shares_are_summed_once_each()
    {
        var billA = Guid.NewGuid();
        var billB = Guid.NewGuid();
        var rows = ChargeProfitability.Compose("expected", "VND",
        [
            Line(Packaging, "cost", 40_000m, "expected", billId: billA, sourceKind: "allocation"),
            Line(Packaging, "cost", 60_000m, "expected", billId: billB, sourceKind: "allocation"),
            Line(Packaging, "revenue", 50_000m, "expected", billId: billA),
            Line(Packaging, "revenue", 40_000m, "expected", billId: billB)
        ]);

        var row = Assert.Single(rows.Rows);
        Assert.Equal(100_000m, row.CostReporting);
        Assert.Equal(90_000m, row.RevenueReporting);
        Assert.Equal(-10_000m, row.ProfitReporting);
        Assert.Equal(4, row.Sources.Count);
    }

    [Fact]
    public void Actual_only_allocation_does_not_fill_expected_or_hide_a_real_expected_line()
    {
        var layers = ChargeProfitability.LayersForAllocation("actual", 80_000m);
        Assert.Null(layers.Expected);
        Assert.Null(layers.Confirmed);
        Assert.Equal(80_000m, layers.Actual);

        var rows = ChargeProfitability.Compose("expected", "VND",
        [
            Line(Packaging, "cost", expected: 100_000m),
            new ChargeProfitSlice(Packaging, "PACKAGING", "PACKAGING", "cost", "actual", layers.Expected, layers.Confirmed, layers.Actual, Guid.NewGuid(), "allocation", null, null, layers.ExpectedFx, layers.ConfirmedFx, layers.ActualFx),
            Line(Packaging, "revenue", expected: 90_000m)
        ]);

        var row = Assert.Single(rows.Rows);
        Assert.Equal(100_000m, row.CostReporting);
        Assert.Equal(-10_000m, row.ProfitReporting);
        Assert.True(row.NegativeFlag);
    }

    [Fact]
    public void Two_orders_each_take_half_of_a_shared_bill()
    {
        Assert.Equal(50_000m, ChargeProfitability.OrderShare(100_000m, 2));
        Assert.Equal(100_000m, ChargeProfitability.OrderShare(100_000m, 1));
    }

    [Fact]
    public void Missing_fx_keeps_the_side_incomplete()
    {
        var rows = ChargeProfitability.Compose("expected", "USD",
        [
            new ChargeProfitSlice(Packaging, "PACKAGING", "PACKAGING", "cost", "expected", null, null, null, Guid.NewGuid(), "cost", null, null, true, false, false),
            Line(Packaging, "revenue", expected: 90_000m)
        ]);

        var row = Assert.Single(rows.Rows);
        Assert.Equal(ChargeProfitability.Incomplete, row.DataCompleteness);
        Assert.Null(row.CostReporting);
        Assert.False(row.NegativeFlag);
    }

    private static readonly Guid Packaging = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid Pickup = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");

    private static ChargeProfitSlice Line(
        Guid? chargeTypeId,
        string side,
        decimal? expected = null,
        string peak = "expected",
        string code = "PACKAGING",
        decimal? confirmed = null,
        decimal? actual = null,
        Guid? billId = null,
        string sourceKind = "cost") =>
        new(
            chargeTypeId,
            code,
            code,
            side,
            peak,
            expected,
            confirmed,
            actual,
            Guid.NewGuid(),
            sourceKind,
            null,
            billId);
}
