using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: financial_close_snapshot_details (D11) — metrics/components/source references of an as-closed snapshot.
/// Immutable with parent snapshot (C-010).
/// </summary>
public sealed class FinancialCloseSnapshotDetail : TenantEntityBase
{
    public Guid SnapshotId { get; set; }

    public int LineNo { get; set; }

    /// <summary>cost_total | revenue_total | ap_outstanding | ar_outstanding | cost_count | …</summary>
    public string MetricKey { get; set; } = string.Empty;

    public decimal MetricValue { get; set; }
    public string? CurrencyCode { get; set; }

    /// <summary>Optional source aggregate type (cost | revenue | accounts_payable | …).</summary>
    public string? SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public string? Notes { get; set; }

    public FinancialCloseSnapshot? Snapshot { get; set; }
}
