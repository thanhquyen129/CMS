using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: revenue_adjustments (D06) — adjustment/reversal history (C-009).
/// Never silent-overwrite Revenue amounts without a row here.
/// </summary>
public sealed class RevenueAdjustment : TenantEntityBase
{
    public Guid RevenueId { get; set; }

    /// <summary>adjustment | reversal</summary>
    public string AdjustmentType { get; set; } = RevenueAdjustmentTypes.Adjustment;

    public decimal DeltaAmount { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public string Reason { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }

    /// <summary>Maturity layer the delta was applied to.</summary>
    public string AppliedToMaturity { get; set; } = RevenueMaturities.Expected;

    public decimal AmountBefore { get; set; }
    public decimal AmountAfter { get; set; }

    public Revenue? Revenue { get; set; }
}

public static class RevenueAdjustmentTypes
{
    public const string Adjustment = "adjustment";
    public const string Reversal = "reversal";
}
