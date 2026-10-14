namespace LCMS.Domain.Entities;

public static class RateDeclarationModes
{
    public const string Basic = "basic";
    public const string Advanced = "advanced";

    public static bool IsKnown(string? value) =>
        string.Equals(value, Basic, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Advanced, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// One rate card keeps either basic pricing rules or the weight-break matrix.
/// This type does not calculate a rate.
/// </summary>
public static class RateDeclarationPolicy
{
    public const string DualPayloadMessage =
        "Một bảng giá chỉ dùng một hình thức khai báo. Không gửi đồng thời quy tắc giá và ma trận khung giá.";

    public const string UnknownCurrencyMessage =
        "Mã tiền tệ không có trong danh mục tiền tệ đang dùng.";

    public const string MissingRouteMessage =
        "Tuyến không tồn tại hoặc không thuộc thuê bao hiện tại.";

    public const string InactiveRouteMessage =
        "Tuyến đã ngừng sử dụng. Chọn tuyến đang hoạt động trong danh mục.";

    public const string ModeChangeConfirmMessage =
        "Đổi hình thức khai báo cần xác nhận. Dữ liệu hình thức hiện tại trên bản nháp sẽ được gỡ.";

    public const string PublishedModeLockedMessage =
        "Bảng giá đã có phiên bản phát hành. Không đổi hình thức khai báo.";

    public const string UnknownModeMessage =
        "Hình thức khai báo không hợp lệ. Chọn bảng giá cơ bản hoặc bảng giá nâng cao.";

    public const string MixedLegacyMessage =
        "Bảng giá lịch sử đang có cả quy tắc và ma trận. Không ghi đè và không xóa cho đến khi có quyết định xử lý.";

    public static bool IsMatrixMethod(string? calcMethod) =>
        string.Equals(calcMethod, PricingCalcMethods.WeightBreakPivot, StringComparison.OrdinalIgnoreCase);

    public static bool IsDual(bool hasBasicPayload, bool hasMatrixPayload) =>
        hasBasicPayload && hasMatrixPayload;

    /// <summary>Null means empty, or mixed legacy that must not be guessed.</summary>
    public static string? Infer(bool hasMatrix, bool hasBasic)
    {
        if (hasMatrix && hasBasic)
        {
            return null;
        }

        if (hasMatrix)
        {
            return RateDeclarationModes.Advanced;
        }

        if (hasBasic)
        {
            return RateDeclarationModes.Basic;
        }

        return null;
    }

    public static string? CurrencyBlockReason(bool existsAndActive, bool unchanged) =>
        unchanged || existsAndActive ? null : UnknownCurrencyMessage;

    public static string? RouteBlockReason(bool foundInTenant, bool active, bool sameAsStored)
    {
        if (!foundInTenant)
        {
            return MissingRouteMessage;
        }

        if (!active && !sameAsStored)
        {
            return InactiveRouteMessage;
        }

        return null;
    }

    public static (string Mode, bool RetireOtherShape, string? BlockMessage) DecideMode(
        string? current,
        string requested,
        bool confirm,
        bool hasPublished,
        bool draftHasOtherShape)
    {
        if (!RateDeclarationModes.IsKnown(requested))
        {
            return ("", false, UnknownModeMessage);
        }

        var mode = requested.Trim().ToLowerInvariant();
        var currentNorm = string.IsNullOrWhiteSpace(current) ? null : current.Trim().ToLowerInvariant();
        if (currentNorm == mode)
        {
            return (mode, false, null);
        }

        if (hasPublished && currentNorm is not null)
        {
            return (currentNorm, false, PublishedModeLockedMessage);
        }

        if (hasPublished && draftHasOtherShape)
        {
            return (currentNorm ?? "", false, PublishedModeLockedMessage);
        }

        if (currentNorm is null && !draftHasOtherShape)
        {
            return (mode, false, null);
        }

        if (draftHasOtherShape && !confirm)
        {
            return (currentNorm ?? "", false, ModeChangeConfirmMessage);
        }

        return (mode, true, null);
    }
}
