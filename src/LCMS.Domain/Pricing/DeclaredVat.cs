namespace LCMS.Domain.Pricing;

/// <summary>
/// Declared VAT on a price version. Null is undeclared and is not 0%.
/// Unit prices are before VAT. Economic cost and revenue stay on the net amount.
/// </summary>
public static class DeclaredVat
{
    public const decimal MaxPercent = 100m;

    public static bool IsValid(decimal? rate) =>
        rate is null || (rate.Value >= 0m && rate.Value <= MaxPercent);

    public static (decimal Net, decimal? Vat, decimal? Gross) Split(decimal net, decimal? rate)
    {
        if (!IsValid(rate))
        {
            throw new ArgumentOutOfRangeException(nameof(rate), "Thuế suất VAT phải từ 0 đến 100.");
        }

        var roundedNet = decimal.Round(net, 4, MidpointRounding.AwayFromZero);
        if (rate is null)
        {
            return (roundedNet, null, null);
        }

        var vat = decimal.Round(roundedNet * rate.Value / 100m, 4, MidpointRounding.AwayFromZero);
        var gross = decimal.Round(roundedNet + vat, 4, MidpointRounding.AwayFromZero);
        return (roundedNet, vat, gross);
    }
}
