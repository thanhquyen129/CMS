using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: costs (D05) — stub; Single Economic Cost. Full TD1 fields in later slice.
/// </summary>
public sealed class Cost : TenantEntityBase
{
    public Guid? BillId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public string FinancialMaturity { get; set; } = "expected";
    public string RecordStatus { get; set; } = "active";
}
