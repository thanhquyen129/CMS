using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: bills (D03) — Financial Anchor (H-003 / TD1).
/// Baseline fields per TD1 Key Aggregates; business number ≠ PK.
/// </summary>
public sealed class Bill : TenantEntityBase
{
    /// <summary>Business reference number (unique per tenant — IDX-001).</summary>
    public string BillNo { get; set; } = string.Empty;

    public string BillType { get; set; } = string.Empty;

    public string? SourceSystem { get; set; }
    public string? ExternalId { get; set; }
    public string? ExternalVersion { get; set; }

    public string OperationalStatus { get; set; } = "active";
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
}
