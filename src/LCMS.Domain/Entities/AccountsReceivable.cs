using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: accounts_receivable (D08) — recognized AR record (separate from receivable_exposures).
/// Outstanding is derived (C-015): RecognizedAmount + AdjustmentAmount − FinalizedSettledAmount.
/// FinalizedSettledAmount increases only via finalized collection_allocations (AC-007 / C-008).
/// </summary>
public sealed class AccountsReceivable : TenantEntityBase, IReportingFx
{
    public Guid ReceivableExposureId { get; set; }
    public Guid? BillId { get; set; }
    public Guid? CounterpartyId { get; set; }

    /// <summary>Amount recognized onto AR (SoT for recognition slice).</summary>
    public decimal RecognizedAmount { get; set; }

    /// <summary>Net adjustment after recognition (can be negative).</summary>
    public decimal AdjustmentAmount { get; set; }

    /// <summary>Sum of finalized (non-reversed) collection allocations — not user-entered.</summary>
    public decimal FinalizedSettledAmount { get; set; }

    public string CurrencyCode { get; set; } = "VND";
    /// <summary>Recognized amount in tenant reporting currency, snapshotted from the source record (FX-ARCH-11).</summary>
    public decimal? ReportingAmount { get; set; }
    public Guid? FxRateId { get; set; }
    public string? ReportingCurrencyCode { get; set; }
    public decimal? FxRate { get; set; }
    public string? FxSourceType { get; set; }
    public string? FxSourceName { get; set; }
    public DateOnly? FxRateDate { get; set; }
    public string? FxOverrideReason { get; set; }
    public Guid? FxAppliedBy { get; set; }
    public DateTimeOffset? FxAppliedAt { get; set; }
    public string FxStatus { get; set; } = FxStatuses.Missing;
    public DateOnly? DueDate { get; set; }

    /// <summary>open | partially_settled | settled — derived from settlement progress, not exposure status.</summary>
    public string SettlementStatus { get; set; } = ApArSettlementStatuses.Open;

    public DateTimeOffset RecognizedAt { get; set; }
    public Guid? RecognizedBy { get; set; }
    public string? Notes { get; set; }
    public string RecordStatus { get; set; } = "active";

    public ReceivableExposure? ReceivableExposure { get; set; }
    public Bill? Bill { get; set; }

    /// <summary>C-015: never accept this from user input as SoT.</summary>
    public decimal DeriveOutstanding() =>
        RecognizedAmount + AdjustmentAmount - FinalizedSettledAmount;
}
