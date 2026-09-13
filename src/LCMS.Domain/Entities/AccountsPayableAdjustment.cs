using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: accounts_payable_adjustments — adjustment / write-off / reverse-recognize history (C-013 / C-015).
/// Never silent-overwrite AP amounts without a row here.
/// </summary>
public sealed class AccountsPayableAdjustment : TenantEntityBase
{
    public Guid AccountsPayableId { get; set; }

    /// <summary>adjustment | write_off | reverse_recognize</summary>
    public string AdjustmentType { get; set; } = ApArAdjustmentTypes.Adjustment;

    public decimal DeltaAmount { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public string Reason { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }

    public decimal AdjustmentAmountBefore { get; set; }
    public decimal AdjustmentAmountAfter { get; set; }
    public decimal OutstandingBefore { get; set; }
    public decimal OutstandingAfter { get; set; }

    public AccountsPayable? AccountsPayable { get; set; }
}

/// <summary>
/// Table: accounts_receivable_adjustments — AR adjustment history (C-013 / C-015).
/// </summary>
public sealed class AccountsReceivableAdjustment : TenantEntityBase
{
    public Guid AccountsReceivableId { get; set; }

    /// <summary>adjustment | write_off | reverse_recognize</summary>
    public string AdjustmentType { get; set; } = ApArAdjustmentTypes.Adjustment;

    public decimal DeltaAmount { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public string Reason { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }

    public decimal AdjustmentAmountBefore { get; set; }
    public decimal AdjustmentAmountAfter { get; set; }
    public decimal OutstandingBefore { get; set; }
    public decimal OutstandingAfter { get; set; }

    public AccountsReceivable? AccountsReceivable { get; set; }
}

public static class ApArAdjustmentTypes
{
    public const string Adjustment = "adjustment";
    public const string WriteOff = "write_off";
    public const string ReverseRecognize = "reverse_recognize";
}

public static class ApArRecordStatuses
{
    public const string Active = "active";
    public const string Reversed = "reversed";
}
