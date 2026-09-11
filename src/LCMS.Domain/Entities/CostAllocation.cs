using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: cost_allocations (D05) — versioned allocation session for a shared Cost.
/// Finalize requires valid basis (C-006) and conservation (C-005).
/// </summary>
public sealed class CostAllocation : TenantEntityBase
{
    public Guid CostId { get; set; }
    public int VersionNo { get; set; } = 1;

    /// <summary>e.g. weight, equal, revenue, manual</summary>
    public string AllocationBasis { get; set; } = "manual";

    public string ApplicabilityMode { get; set; } = "explicit";
    public decimal AllocatableAmount { get; set; }
    public decimal AllocatedAmount { get; set; }

    /// <summary>draft | finalized | superseded</summary>
    public string AllocationStatus { get; set; } = CostAllocationStatuses.Draft;

    public Guid? RuleId { get; set; }
    public string? RuleVersion { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public Guid? FinalizedBy { get; set; }
    public Guid? SupersedesAllocationId { get; set; }

    public Cost? Cost { get; set; }
}

public static class CostAllocationStatuses
{
    public const string Draft = "draft";
    public const string Finalized = "finalized";
    public const string Superseded = "superseded";
}
