using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: receivable_exposures (D08) — expected collectible before recognized AR.
/// Exposure ≠ Recognized AR (CP3/TD4): recognition creates a separate accounts_receivable row.
/// </summary>
public sealed class ReceivableExposure : TenantEntityBase
{
    public Guid? BillId { get; set; }
    public Guid? CounterpartyId { get; set; }

    /// <summary>Optional link to existing Revenue (never invents Revenue on recognize — C-004).</summary>
    public Guid? RevenueId { get; set; }

    public Guid? FinancialDocumentId { get; set; }

    /// <summary>Full exposure amount (ceiling for recognition).</summary>
    public decimal Amount { get; set; }

    /// <summary>Sum of linked AR recognized amounts — cache updated on recognize.</summary>
    public decimal RecognizedAmount { get; set; }

    public string CurrencyCode { get; set; } = "VND";

    /// <summary>open | partially_recognized | fully_recognized | cancelled</summary>
    public string Status { get; set; } = ExposureStatuses.Open;

    public DateOnly EffectiveDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? Notes { get; set; }
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string RecordStatus { get; set; } = "active";

    public Bill? Bill { get; set; }
    public Revenue? Revenue { get; set; }
    public FinancialDocument? FinancialDocument { get; set; }
}
