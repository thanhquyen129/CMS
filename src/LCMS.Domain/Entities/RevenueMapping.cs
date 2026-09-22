using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: revenue_mappings — one economic revenue split across bills. Finalize is immutable.
/// </summary>
public sealed class RevenueMapping : TenantEntityBase
{
    public Guid RevenueId { get; set; }
    public int VersionNo { get; set; } = 1;
    public string AllocationBasis { get; set; } = CostAllocationBases.Equal;
    public string ApplicabilityMode { get; set; } = "explicit";
    public Guid? ScopeId { get; set; }
    public string? ConditionCode { get; set; }
    public decimal AllocatableAmount { get; set; }
    public decimal AllocatedAmount { get; set; }

    /// <summary>Maturity layer that was split. Other layers stay out of this snapshot.</summary>
    public string MappedMaturity { get; set; } = RevenueMaturities.Expected;

    /// <summary>draft | finalized | cancelled | superseded</summary>
    public string MappingStatus { get; set; } = CostAllocationStatuses.Draft;

    public DateTimeOffset? FinalizedAt { get; set; }
    public Guid? FinalizedBy { get; set; }
    public Guid? SupersedesMappingId { get; set; }

    public Revenue? Revenue { get; set; }
}

/// <summary>Table: revenue_mapping_details — one bill line of a revenue split.</summary>
public sealed class RevenueMappingDetail : TenantEntityBase
{
    public Guid MappingId { get; set; }
    public Guid BillId { get; set; }
    public decimal BasisValue { get; set; }
    public decimal BasisRatio { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal RoundingAdjustment { get; set; }
    public decimal? ManualOverrideAmount { get; set; }
    public string? OverrideReason { get; set; }
    public decimal? OverrideBeforeAmount { get; set; }

    public RevenueMapping? Mapping { get; set; }
    public Bill? Bill { get; set; }
}
