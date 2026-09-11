using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: accounts_payable (D08) — recognized AP record (separate from payable_exposures).
/// Outstanding is derived (C-015): RecognizedAmount + AdjustmentAmount − FinalizedSettledAmount.
/// FinalizedSettledAmount increases only via finalized payment_allocations (AC-007 / C-008).
/// </summary>
public sealed class AccountsPayable : TenantEntityBase
{
    public Guid PayableExposureId { get; set; }
    public Guid? BillId { get; set; }
    public Guid? CounterpartyId { get; set; }

    /// <summary>Amount recognized onto AP (SoT for recognition slice).</summary>
    public decimal RecognizedAmount { get; set; }

    /// <summary>Net adjustment after recognition (can be negative).</summary>
    public decimal AdjustmentAmount { get; set; }

    /// <summary>Sum of finalized (non-reversed) payment allocations — not user-entered.</summary>
    public decimal FinalizedSettledAmount { get; set; }

    public string CurrencyCode { get; set; } = "VND";
    public DateOnly? DueDate { get; set; }

    /// <summary>open | partially_settled | settled — derived from settlement progress, not exposure status.</summary>
    public string SettlementStatus { get; set; } = ApArSettlementStatuses.Open;

    public DateTimeOffset RecognizedAt { get; set; }
    public Guid? RecognizedBy { get; set; }
    public string? Notes { get; set; }
    public string RecordStatus { get; set; } = "active";

    public PayableExposure? PayableExposure { get; set; }
    public Bill? Bill { get; set; }

    /// <summary>C-015: never accept this from user input as SoT.</summary>
    public decimal DeriveOutstanding() =>
        RecognizedAmount + AdjustmentAmount - FinalizedSettledAmount;
}

public static class ApArSettlementStatuses
{
    public const string Open = "open";
    public const string PartiallySettled = "partially_settled";
    public const string Settled = "settled";
}
