using LCMS.Application.Financials;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;

namespace LCMS.Api.Tests;

/// <summary>
/// LCMS-CR-RATING-FINANCIAL-DEDUP-01 — P0 logic coverage mapped to TC blockers
/// (TC-01/02/03/23/25/26/30) without depending on a specific staging Bill.
/// </summary>
public sealed class RatingFinancialDedupP0Tests
{
    [Fact]
    public void TC01_UnappliedRatingDetails_AreComparisonOnly()
    {
        // Calculate-only leaves no Cost; exclusion reason documents comparison-only rows.
        var line = new EffectiveFactLine(
            Guid.NewGuid(), "cost", "k", "expected", 0m, 100m, null, null,
            false, EffectiveExclusionReasons.ComparisonOnlyUnapplied, null, null, null);
        Assert.False(line.EffectiveIncluded);
        Assert.Equal(0m, EffectiveFactSelector.SumBestAvailable([line]));
    }

    [Fact]
    public void TC02_TC03_ReapplySameIdentity_KeepsSingleEffectiveExpected()
    {
        var key = EconomicIdentity.Build(Guid.NewGuid(), Guid.NewGuid(), "BUY", "FREIGHT", null, null, Guid.NewGuid());
        var older = new EconomicFactCandidate(
            Guid.NewGuid(), "cost", key, "expected", 1_740_000m, null, null,
            EconomicRecordStatuses.Superseded, CostSourceTypes.RatingDetail, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddHours(-1));
        var newer = new EconomicFactCandidate(
            Guid.NewGuid(), "cost", key, "expected", 1_850_000m, null, null,
            EconomicRecordStatuses.Active, CostSourceTypes.RatingDetail, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow);

        var selected = EffectiveFactSelector.Select([older, newer]);
        Assert.Equal(1_850_000m, EffectiveFactSelector.SumBestAvailable(selected));
        Assert.Single(selected, l => l.EffectiveIncluded);
        Assert.Contains(selected, l => l.ExclusionReason == EffectiveExclusionReasons.SupersededByNewerExpected);
    }

    [Fact]
    public void TC23_ProtectedActual_KeepsActualWhenExpectedUpdated()
    {
        var cost = new Cost
        {
            FinancialMaturity = CostMaturities.Actual,
            ExpectedAmount = 1_440_000m,
            ActualAmount = 1_440_000m,
            Amount = 1_440_000m,
            RecordStatus = EconomicRecordStatuses.Active
        };
        Assert.True(RatingFinancialApply.HasProtectedMaturity(cost));
        cost.ExpectedAmount = 1_500_000m;
        Assert.Equal(1_440_000m, cost.ActualAmount);
    }

    [Fact]
    public void TC25_TC26_BestAvailable_ActualOverExpected_SameIdentity()
    {
        var key = "id-freight";
        var selected = EffectiveFactSelector.Select(
        [
            new EconomicFactCandidate(
                Guid.NewGuid(), "cost", key, "actual", 1_440_000m, null, 1_440_000m,
                "active", null, null, null, DateTimeOffset.UtcNow.AddDays(-1)),
            new EconomicFactCandidate(
                Guid.NewGuid(), "cost", key, "expected", 1_440_000m, null, null,
                "active", null, null, null, DateTimeOffset.UtcNow),
        ]);
        Assert.Equal(1_440_000m, EffectiveFactSelector.SumBestAvailable(selected));
    }

    [Fact]
    public void TC30_BillSample_EffectiveCost_1740000_WhenTwoObligations()
    {
        var freight = "FREIGHT";
        var pack = "DONGGOI";
        var selected = EffectiveFactSelector.Select(
        [
            new EconomicFactCandidate(Guid.NewGuid(), "cost", freight, "actual", 1_440_000m, null, 1_440_000m, "active", null, null, null, DateTimeOffset.UtcNow.AddDays(-1)),
            new EconomicFactCandidate(Guid.NewGuid(), "cost", freight, "expected", 1_440_000m, null, null, "active", null, null, null, DateTimeOffset.UtcNow),
            new EconomicFactCandidate(Guid.NewGuid(), "cost", pack, "expected", 300_000m, null, null, "active", null, null, null, DateTimeOffset.UtcNow),
            new EconomicFactCandidate(Guid.NewGuid(), "cost", pack, "expected", 300_000m, null, null, "active", null, null, null, DateTimeOffset.UtcNow.AddMinutes(-5)),
        ]);
        Assert.Equal(1_740_000m, EffectiveFactSelector.SumBestAvailable(selected));

        var revenue = EffectiveFactSelector.Select(
        [
            new EconomicFactCandidate(Guid.NewGuid(), "revenue", "SELL-F", "actual", 2_040_000m, null, 2_040_000m, "active", null, null, null, DateTimeOffset.UtcNow),
            new EconomicFactCandidate(Guid.NewGuid(), "revenue", "SELL-D", "expected", 400_000m, null, null, "active", null, null, null, DateTimeOffset.UtcNow),
        ]);
        Assert.Equal(2_440_000m, EffectiveFactSelector.SumBestAvailable(revenue));
        Assert.Equal(700_000m, EffectiveFactSelector.SumBestAvailable(revenue) - EffectiveFactSelector.SumBestAvailable(selected));
    }

    [Fact]
    public void TC08_TwoLegs_SameCharge_BothIncluded()
    {
        var tenant = Guid.NewGuid();
        var bill = Guid.NewGuid();
        var vendor = Guid.NewGuid();
        var k1 = EconomicIdentity.Build(tenant, bill, "BUY", "FREIGHT", Guid.NewGuid(), null, vendor);
        var k2 = EconomicIdentity.Build(tenant, bill, "BUY", "FREIGHT", Guid.NewGuid(), null, vendor);
        var selected = EffectiveFactSelector.Select(
        [
            new EconomicFactCandidate(Guid.NewGuid(), "cost", k1, "expected", 100m, null, null, "active", null, null, null, DateTimeOffset.UtcNow),
            new EconomicFactCandidate(Guid.NewGuid(), "cost", k2, "expected", 200m, null, null, "active", null, null, null, DateTimeOffset.UtcNow),
        ]);
        Assert.Equal(300m, EffectiveFactSelector.SumBestAvailable(selected));
    }

    [Fact]
    public void TC18_EligibleNotActivated_CannotSeed()
    {
        var details = new List<RatingDetail>
        {
            new() { ComponentCode = "FSC", FinancialNature = "cost", ApplicationBucket = "eligible_not_activated", Amount = 50 },
            new() { ComponentCode = "FREIGHT", FinancialNature = "cost", ApplicationBucket = "applied", Amount = 100 },
        };
        var seedable = RatingFinancialApply.FilterSeedableDetails(details, "cost");
        Assert.Single(seedable);
        Assert.Equal("FREIGHT", seedable[0].ComponentCode);
    }
}
