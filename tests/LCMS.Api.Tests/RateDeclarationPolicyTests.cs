using LCMS.Domain.Entities;

namespace LCMS.Api.Tests;

public class RateDeclarationPolicyTests
{
    [Fact]
    public void Dual_payload_is_rejected()
    {
        Assert.True(RateDeclarationPolicy.IsDual(true, true));
        Assert.False(RateDeclarationPolicy.IsDual(true, false));
        Assert.False(RateDeclarationPolicy.IsDual(false, true));
        Assert.Equal(
            "Một bảng giá chỉ dùng một hình thức khai báo. Không gửi đồng thời quy tắc giá và ma trận khung giá.",
            RateDeclarationPolicy.DualPayloadMessage);
    }

    [Theory]
    [InlineData(false, true, "basic")]
    [InlineData(true, false, "advanced")]
    [InlineData(false, false, null)]
    [InlineData(true, true, null)]
    public void Legacy_inference_does_not_guess_mixed_rows(bool matrix, bool basic, string? expected)
    {
        Assert.Equal(expected, RateDeclarationPolicy.Infer(matrix, basic));
    }

    [Fact]
    public void Mode_change_keeps_data_until_confirmed_and_locks_published()
    {
        var needsConfirm = RateDeclarationPolicy.DecideMode("basic", "advanced", confirm: false, hasPublished: false, draftHasOtherShape: true);
        Assert.Equal(RateDeclarationPolicy.ModeChangeConfirmMessage, needsConfirm.BlockMessage);
        Assert.False(needsConfirm.RetireOtherShape);

        var accepted = RateDeclarationPolicy.DecideMode("basic", "advanced", confirm: true, hasPublished: false, draftHasOtherShape: true);
        Assert.Null(accepted.BlockMessage);
        Assert.Equal(RateDeclarationModes.Advanced, accepted.Mode);
        Assert.True(accepted.RetireOtherShape);

        var locked = RateDeclarationPolicy.DecideMode("advanced", "basic", confirm: true, hasPublished: true, draftHasOtherShape: true);
        Assert.Equal(RateDeclarationPolicy.PublishedModeLockedMessage, locked.BlockMessage);
    }

    [Fact]
    public void Unset_mode_with_the_other_shape_still_needs_confirm()
    {
        var blocked = RateDeclarationPolicy.DecideMode(null, "advanced", confirm: false, hasPublished: false, draftHasOtherShape: true);
        Assert.Equal(RateDeclarationPolicy.ModeChangeConfirmMessage, blocked.BlockMessage);

        var accepted = RateDeclarationPolicy.DecideMode(null, "advanced", confirm: true, hasPublished: false, draftHasOtherShape: true);
        Assert.Null(accepted.BlockMessage);
        Assert.True(accepted.RetireOtherShape);
    }

    [Fact]
    public void Empty_switch_does_not_require_confirm()
    {
        var decision = RateDeclarationPolicy.DecideMode("basic", "advanced", confirm: false, hasPublished: false, draftHasOtherShape: false);
        Assert.Null(decision.BlockMessage);
        Assert.True(decision.RetireOtherShape);
    }

    [Fact]
    public void Currency_keeps_stored_code_and_rejects_unknown()
    {
        Assert.Null(RateDeclarationPolicy.CurrencyBlockReason(existsAndActive: false, unchanged: true));
        Assert.Null(RateDeclarationPolicy.CurrencyBlockReason(existsAndActive: true, unchanged: false));
        Assert.Equal(
            RateDeclarationPolicy.UnknownCurrencyMessage,
            RateDeclarationPolicy.CurrencyBlockReason(existsAndActive: false, unchanged: false));
    }

    [Fact]
    public void Route_rejects_missing_or_inactive_change_and_keeps_stored_inactive()
    {
        Assert.Equal(
            RateDeclarationPolicy.MissingRouteMessage,
            RateDeclarationPolicy.RouteBlockReason(foundInTenant: false, active: false, sameAsStored: false));
        Assert.Equal(
            RateDeclarationPolicy.InactiveRouteMessage,
            RateDeclarationPolicy.RouteBlockReason(foundInTenant: true, active: false, sameAsStored: false));
        Assert.Null(RateDeclarationPolicy.RouteBlockReason(foundInTenant: true, active: false, sameAsStored: true));
        Assert.Null(RateDeclarationPolicy.RouteBlockReason(foundInTenant: true, active: true, sameAsStored: false));
    }

    [Fact]
    public void Matrix_method_is_only_the_existing_weight_break()
    {
        Assert.True(RateDeclarationPolicy.IsMatrixMethod(PricingCalcMethods.WeightBreakPivot));
        Assert.False(RateDeclarationPolicy.IsMatrixMethod(PricingCalcMethods.Fixed));
        Assert.False(RateDeclarationPolicy.IsMatrixMethod(PricingCalcMethods.UnitRate));
    }
}
