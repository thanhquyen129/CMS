using LCMS.Application.Financials;
using LCMS.Domain.Pricing;

namespace LCMS.Api.Tests;

public sealed class EconomicIdentityEffectiveFactSelectorTests
{
    [Fact]
    public void EconomicIdentity_DifferentLegs_AreDistinct()
    {
        var tenant = Guid.NewGuid();
        var bill = Guid.NewGuid();
        var leg1 = Guid.NewGuid();
        var leg2 = Guid.NewGuid();
        var vendor = Guid.NewGuid();

        var a = EconomicIdentity.Build(tenant, bill, "BUY", "FREIGHT", leg1, null, vendor);
        var b = EconomicIdentity.Build(tenant, bill, "BUY", "FREIGHT", leg2, null, vendor);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void EffectiveFactSelector_DoesNotSumActualPlusDuplicateExpected()
    {
        var key = "t|bill|BUY|FREIGHT|leg1|vendor";
        var lines = EffectiveFactSelector.Select(
        [
            new EconomicFactCandidate(
                Guid.NewGuid(), "cost", key, "actual", 1_440_000m, null, 1_440_000m,
                "active", "rating_detail", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2)),
            new EconomicFactCandidate(
                Guid.NewGuid(), "cost", key, "expected", 1_440_000m, null, null,
                "active", "rating_detail", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow),
            new EconomicFactCandidate(
                Guid.NewGuid(), "cost", key + "|donggoi", "expected", 300_000m, null, null,
                "active", "rating_detail", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow),
            new EconomicFactCandidate(
                Guid.NewGuid(), "cost", key + "|donggoi", "expected", 300_000m, null, null,
                "active", "rating_detail", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)),
        ]);

        var best = EffectiveFactSelector.SumBestAvailable(lines);
        Assert.Equal(1_740_000m, best);
        Assert.Equal(2, lines.Count(l => l.EffectiveIncluded));
        Assert.Contains(lines, l => l.ExclusionReason == EffectiveExclusionReasons.ShadowedByActual);
        Assert.Contains(lines, l => l.ExclusionReason == EffectiveExclusionReasons.SupersededByNewerExpected);
    }

    [Fact]
    public void EffectiveFactSelector_PartialActual_DoesNotDoubleCount()
    {
        var key = "t|bill|BUY|FREIGHT|occ|v";
        var lines = EffectiveFactSelector.Select(
        [
            new EconomicFactCandidate(
                Guid.NewGuid(), "cost", key, "actual", 10_000_000m, null, 6_000_000m,
                "active", "manual", null, null, DateTimeOffset.UtcNow),
        ]);

        var best = EffectiveFactSelector.SumBestAvailable(lines);
        Assert.Equal(10_000_000m, best);
        Assert.True(lines[0].PartialActualRemaining);
        Assert.Equal(4_000_000m, lines[0].RemainingExpectedAmount);
    }

    [Fact]
    public void RatingApplicationBuckets_OnlyAppliedMaySeed()
    {
        Assert.True(RatingApplicationBuckets.MaySeedToFinancials(null, "base_rate"));
        Assert.True(RatingApplicationBuckets.MaySeedToFinancials("applied", "base_rate"));
        Assert.False(RatingApplicationBuckets.MaySeedToFinancials(null, "surcharge"));
        Assert.True(RatingApplicationBuckets.MaySeedToFinancials("applied", "surcharge"));
        Assert.False(RatingApplicationBuckets.MaySeedToFinancials("eligible_not_activated", "surcharge"));
        Assert.False(RatingApplicationBuckets.MaySeedToFinancials("excluded", "base_rate"));
    }
}
