using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: fx_rates (D02) — dated FX rates (source/date/from/to/version).
/// Rate multiplies FromCurrency → ToCurrency (typically tenant base).
/// </summary>
public sealed class FxRate : TenantEntityBase
{
    public string FromCurrencyCode { get; set; } = string.Empty;
    public string ToCurrencyCode { get; set; } = string.Empty;

    /// <summary>Business date the rate applies (UTC calendar date).</summary>
    public DateOnly RateDate { get; set; }

    /// <summary>Multiply amount in From by Rate to get To.</summary>
    public decimal Rate { get; set; }

    /// <summary>manual | import | stub_seed | …</summary>
    public string Source { get; set; } = FxRateSources.Manual;

    /// <summary>Version for same from/to/date (TD1); default 1.</summary>
    public int Version { get; set; } = 1;

    public string? Note { get; set; }
}

public static class FxRateSources
{
    public const string Manual = "manual";
    public const string Import = "import";
    public const string StubSeed = "stub_seed";
    public const string Vcb = "vcb";
}
