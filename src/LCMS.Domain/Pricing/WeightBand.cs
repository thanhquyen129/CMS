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
