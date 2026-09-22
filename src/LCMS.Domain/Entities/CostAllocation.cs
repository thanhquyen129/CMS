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

    /// <summary>equal | quantity | gross_kg | chargeable | cbm | package_count | teu | manual_ratio | manual_percent | manual_amount</summary>
    public string AllocationBasis { get; set; } = CostAllocationBases.Equal;

    /// <summary>explicit | leg | movement | condition</summary>
    public string ApplicabilityMode { get; set; } = "explicit";

    public Guid? ScopeId { get; set; }
    public string? ConditionCode { get; set; }
    public decimal AllocatableAmount { get; set; }
    public decimal AllocatedAmount { get; set; }

    /// <summary>draft | calculated | pending_approval | finalized | cancelled | superseded</summary>
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
    public const string GrossKg = "gross_kg";
    public const string Chargeable = "chargeable";
    public const string Cbm = "cbm";
    public const string PackageCount = "package_count";
    public const string Teu = "teu";
    public const string ManualRatio = "manual_ratio";
    public const string ManualPercent = "manual_percent";
    public const string ManualAmount = "manual_amount";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Equal, Quantity, GrossKg, Chargeable, Cbm, PackageCount, Teu, ManualRatio, ManualPercent, ManualAmount
    };

    public static readonly HashSet<string> FromMeasurement = new(StringComparer.OrdinalIgnoreCase)
    {
        GrossKg, Chargeable, Cbm, PackageCount, Teu
    };

    public static bool IsSupported(string? basis) =>
        basis is not null && All.Contains(basis);
}

public static class CostAllocationStatuses
{
    public const string Draft = "draft";
    public const string Calculated = "calculated";
    public const string PendingApproval = "pending_approval";
    public const string Finalized = "finalized";
    public const string Cancelled = "cancelled";
    public const string Superseded = "superseded";

    public static bool IsOpen(string? status) =>
        status is Draft or Calculated or PendingApproval;
}
