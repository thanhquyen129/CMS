using LCMS.Application.RateCards;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;

namespace LCMS.Api.Tests;

public class RateWeightBandTests
{
    [Theory]
    [InlineData(4.999, 0, 5, true)]
    [InlineData(5, 0, 5, false)]
    [InlineData(5, 5, 11, true)]
    [InlineData(10.999, 5, 11, true)]
    [InlineData(11, 5, 11, false)]
    [InlineData(11, 11, 21, true)]
    [InlineData(20.999, 11, 21, true)]
    [InlineData(21, 11, 21, false)]
    [InlineData(21, 21, null, true)]
    public void HalfOpenBand_MatchesBoundaries(decimal weight, decimal min, int? max, bool expected)
    {
        decimal? upper = max;
        Assert.Equal(expected, WeightBand.Contains(weight, min, upper));
    }

    [Fact]
    public void Label_DoesNotRoundDecimals()
    {
        Assert.Equal("< 5", WeightBand.Label(0, 5));
        Assert.Equal("5 – < 11", WeightBand.Label(5, 11));
        Assert.Equal("10.999 – < 11", WeightBand.Label(10.999m, 11));
        Assert.Equal("≥ 21", WeightBand.Label(21, null));
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, 5)]
    [InlineData(11, 10)]
    public void InvalidBand_IsRejected(decimal min, decimal max)
    {
        Assert.NotNull(WeightBand.InvalidReason(min, max));
    }

    [Fact]
    public void OutsideEveryBand_IsNotTheNearest()
    {
        var rules = Bands();
        Assert.Equal("A", Assert.Single(rules.Where(r => RatingEngine.WeightMatches(r, 4.999m, 4.999m))).Code);
        Assert.Equal("D", Assert.Single(rules.Where(r => RatingEngine.WeightMatches(r, 30m, 30m))).Code);
        Assert.Empty(rules.Where(r => RatingEngine.WeightMatches(r, -1m, -1m)));
    }

    [Fact]
    public void FixedAndUnitRate_UseTheMatchedBandOnly()
    {
        var low = Band("A", 0, 5, PricingCalcMethods.Fixed, 100);
        var high = Band("B", 5, 11, PricingCalcMethods.UnitRate, 8);
        Assert.True(RatingEngine.WeightMatches(low, 4.999m, 4.999m));
        Assert.False(RatingEngine.WeightMatches(high, 4.999m, 4.999m));
        Assert.Equal(100m, low.UnitAmount);
        Assert.Equal(40m, decimal.Round(high.UnitAmount * 5m, 4, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void UnitRate_IsNotWeightStep()
    {
        var unit = decimal.Round(10m * 5.5m, 4, MidpointRounding.AwayFromZero);
        var step = RatingEngine.WeightStep(5.5m, 1m, 10, [], null);
        Assert.Equal(55m, unit);
        Assert.Equal(60m, step.Amount);
    }

    [Fact]
    public void GrossAndChargeable_AreDifferentSources()
    {
        var gross = Band("G", 0, 5, PricingCalcMethods.Fixed, 1);
        gross.WeightBasis = WeightBand.Gross;
        var chargeable = Band("C", 0, 5, PricingCalcMethods.Fixed, 1);
        chargeable.WeightBasis = WeightBand.Chargeable;
        Assert.True(RatingEngine.WeightMatches(gross, 4, 9));
        Assert.False(RatingEngine.WeightMatches(chargeable, 4, 9));
        Assert.True(RatingEngine.WeightMatches(chargeable, 9, 4));
        Assert.False(RatingEngine.WeightMatches(gross, 9, 4));
    }

    [Fact]
    public void DirectedRoute_DoesNotMatchTheReverse()
    {
        var sgnHan = new PricingRule { RouteCode = "SGN-HAN", AppliesAnyRoute = false };
        var any = new PricingRule { AppliesAnyRoute = true };
        Assert.True(RatingEngine.RouteMatches(sgnHan, null, "SGN-HAN"));
        Assert.False(RatingEngine.RouteMatches(sgnHan, null, "HAN-SGN"));
        Assert.True(RatingEngine.RouteMatches(any, null, "HAN-SGN"));
        Assert.False(RatingEngine.RouteMatches(new PricingRule { AppliesAnyRoute = false }, null, "SGN-HAN"));
    }

    [Fact]
    public void SameChargeOverlap_Conflicts_DifferentCharges_DoNot()
    {
        var left = Band("A", 0, 10, PricingCalcMethods.Fixed, 1);
        var right = Band("B", 5, 15, PricingCalcMethods.Fixed, 2);
        left.ChargeCode = "FREIGHT";
        right.ChargeCode = "FREIGHT";
        Assert.Equal(RateRuleConflicts.Message, RateRuleConflicts.Find([left, right]));

        right.ChargeCode = "DOC";
        Assert.Null(RateRuleConflicts.Find([left, right]));
    }

    [Fact]
    public void SameChargeSameWeight_SelectDoesNotSumOrGuess()
    {
        var left = Band("A", 0, 10, PricingCalcMethods.Fixed, 1);
        var right = Band("B", 0, 10, PricingCalcMethods.Fixed, 2);
        left.ChargeCode = "FREIGHT";
        right.ChargeCode = "FREIGHT";
        var ex = Assert.Throws<LCMS.Application.Common.Exceptions.ConflictAppException>(
            () => RatingEngine.Select([left, right]));
        Assert.Contains("AMBIGUOUS_RATE_RULE", ex.Message);
    }

    private static List<PricingRule> Bands() =>
    [
        Band("A", 0, 5, PricingCalcMethods.Fixed, 1),
        Band("B", 5, 11, PricingCalcMethods.UnitRate, 2),
        Band("C", 11, 21, PricingCalcMethods.Fixed, 3),
        Band("D", 21, null, PricingCalcMethods.Fixed, 4)
    ];

    private static PricingRule Band(string code, decimal min, decimal? max, string method, decimal amount) =>
        new()
        {
            Code = code,
            Name = code,
            CalcMethod = method,
            UnitAmount = amount,
            WeightMin = min,
            WeightMax = max,
            ChargeCode = "FREIGHT",
            AppliesAnyRoute = true
        };
}
