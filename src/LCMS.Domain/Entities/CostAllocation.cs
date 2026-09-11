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

    /// <summary>equal | quantity | manual_ratio</summary>
    public string AllocationBasis { get; set; } = CostAllocationBases.Equal;

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

/// <summary>Supported allocation bases (C-006) — Pass 2 Sprint 4 FULL.</summary>
public static class CostAllocationBases
{
    public const string Equal = "equal";
    public const string Quantity = "quantity";
    public const string ManualRatio = "manual_ratio";

    public static bool IsSupported(string? basis) =>
        basis is Equal or Quantity or ManualRatio;
}

public static class CostAllocationStatuses
{
    public const string Draft = "draft";
    public const string Finalized = "finalized";
    public const string Superseded = "superseded";
}
