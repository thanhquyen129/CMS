using System.Globalization;

namespace LCMS.Domain.Pricing;

/// <summary>Half-open weight tier [min, max). Max null means no upper bound.</summary>
public static class WeightBand
{
    public const string Gross = "gross";
    public const string Chargeable = "chargeable";

    public static bool IsKnownBasis(string? basis) =>
        string.IsNullOrWhiteSpace(basis)
        || string.Equals(basis, Gross, StringComparison.OrdinalIgnoreCase)
        || string.Equals(basis, Chargeable, StringComparison.OrdinalIgnoreCase);

    public static string? InvalidReason(decimal? min, decimal? max)
    {
        if (min is null && max is null)
        {
            return null;
        }

        if (min is null)
        {
            return "Nấc trọng lượng cần mức từ.";
        }

        if (min < 0)
        {
            return "Mức từ của nấc trọng lượng không được âm.";
        }

        if (max is decimal upper && upper <= min)
        {
            return "Mức đến phải lớn hơn mức từ.";
        }

        return null;
    }

    public const string UnknownBasis =
        "Điều kiện trọng lượng chỉ gồm Không lọc nấc, Trọng lượng thực hoặc Trọng lượng tính cước.";

    public const string NoFilterWithBand =
        "Điều kiện trọng lượng là Không lọc nấc thì không nhập Từ – Đến. Chọn Trọng lượng thực hoặc Trọng lượng tính cước nếu cần lọc nấc.";

    public const string BandRequired =
        "Đã chọn điều kiện trọng lượng thì phải nhập đủ Từ và Đến. Nếu không lọc theo nấc, chọn Không lọc nấc.";

    public const string LegacyInconsistent =
        "Phiên bản này có nấc trọng lượng cũ không nhất quán. Dữ liệu đã phát hành giữ nguyên; điều chỉnh bằng phiên bản mới.";

    /// <summary>
    /// New declarations: no basis means no tier at all; a basis needs both bounds with 0 ≤ min &lt; max.
    /// Published rows are not re-validated.
    /// </summary>
    public static string? DeclarationError(string? basis, decimal? min, decimal? max)
    {
        if (!IsKnownBasis(basis))
        {
            return UnknownBasis;
        }

        if (string.IsNullOrWhiteSpace(basis))
        {
            return min is null && max is null ? null : NoFilterWithBand;
        }

        if (min is null || max is null)
        {
            return BandRequired;
        }

        return InvalidReason(min, max);
    }

    public static bool Contains(decimal weight, decimal min, decimal? max) =>
        weight >= min && (max is null || weight < max);

    public static bool Overlaps(decimal? minA, decimal? maxA, decimal? minB, decimal? maxB)
    {
        if (minA is null && maxA is null)
        {
            return true;
        }

        if (minB is null && maxB is null)
        {
            return true;
        }

        var a0 = minA ?? 0m;
        var b0 = minB ?? 0m;
        var a1 = maxA ?? decimal.MaxValue;
        var b1 = maxB ?? decimal.MaxValue;
        return a0 < b1 && b0 < a1;
    }

    /// <summary>Visual label. Does not round a decimal to the next integer.</summary>
    public static string Label(decimal min, decimal? max)
    {
        if (max is null)
        {
            return $"≥ {Format(min)}";
        }

        if (min == 0)
        {
            return $"< {Format(max.Value)}";
        }

        return $"{Format(min)} – < {Format(max.Value)}";
    }

    public static string Format(decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);
}
