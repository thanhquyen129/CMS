using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: cost_allocation_details (D05) — Bill-level attribution of a shared Cost.
/// Does not create a new economic Cost (C-003).
/// </summary>
public sealed class CostAllocationDetail : TenantEntityBase
{
    public Guid AllocationId { get; set; }
    public Guid BillId { get; set; }
    public decimal BasisValue { get; set; }
    public decimal BasisRatio { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal RoundingAdjustment { get; set; }
    public decimal? ManualOverrideAmount { get; set; }
    public string? OverrideReason { get; set; }
    public decimal? OverrideBeforeAmount { get; set; }

    public CostAllocation? Allocation { get; set; }
    public Bill? Bill { get; set; }
}
