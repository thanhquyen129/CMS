using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: revenues (D06) — stub; Bill-attributable. Full TD1 fields in later slice.
/// </summary>
public sealed class Revenue : TenantEntityBase
{
    public Guid BillId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public string FinancialMaturity { get; set; } = "expected";
    public string RecordStatus { get; set; } = "active";
}
