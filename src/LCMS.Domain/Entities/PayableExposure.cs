using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: payable_exposures (D08) — obligation before recognized AP.
/// Exposure ≠ Recognized AP (CP3/TD4): recognition creates a separate accounts_payable row.
/// </summary>
public sealed class PayableExposure : TenantEntityBase
{
    public Guid? BillId { get; set; }
    public Guid? CounterpartyId { get; set; }

    /// <summary>Optional link to existing Cost (never invents Cost on recognize — C-003).</summary>
    public Guid? CostId { get; set; }

    public Guid? FinancialDocumentId { get; set; }

    /// <summary>Full exposure amount (ceiling for recognition).</summary>
    public decimal Amount { get; set; }

    /// <summary>Sum of linked AP recognized amounts — cache updated on recognize (not user SoT for outstanding).</summary>
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
    public Cost? Cost { get; set; }
    public FinancialDocument? FinancialDocument { get; set; }
}

public static class ExposureStatuses
{
    public const string Open = "open";
    public const string PartiallyRecognized = "partially_recognized";
    public const string FullyRecognized = "fully_recognized";
    public const string Cancelled = "cancelled";
}
