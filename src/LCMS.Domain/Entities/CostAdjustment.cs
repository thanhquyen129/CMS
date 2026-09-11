using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: cost_adjustments (D05) — adjustment/reversal history (C-009 / C-013).
/// Never silent-overwrite Cost amounts without a row here.
/// </summary>
public sealed class CostAdjustment : TenantEntityBase
{
    public Guid CostId { get; set; }

    /// <summary>adjustment | reversal</summary>
    public string AdjustmentType { get; set; } = CostAdjustmentTypes.Adjustment;

    public decimal DeltaAmount { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public string Reason { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }

    /// <summary>Maturity layer the delta was applied to.</summary>
    public string AppliedToMaturity { get; set; } = CostMaturities.Expected;

    public decimal AmountBefore { get; set; }
    public decimal AmountAfter { get; set; }

    public Cost? Cost { get; set; }
}

public static class CostAdjustmentTypes
{
    public const string Adjustment = "adjustment";
    public const string Reversal = "reversal";
}
