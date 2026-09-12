using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: collection_allocations (D09) — N:N Collection ↔ AccountsReceivable.
/// Only finalized rows affect AR.FinalizedSettledAmount / outstanding (AC-007).
/// Reversal sets status reversed — no hard delete (C-013).
/// </summary>
public sealed class CollectionAllocation : TenantEntityBase
{
    public Guid CollectionId { get; set; }
    public Guid AccountsReceivableId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Txn currency (copied from collection; C-014 same-currency allocate).</summary>
    public string CurrencyCode { get; set; } = "VND";

    /// <summary>Allocated amount in base currency (FX stub).</summary>
    public decimal? BaseAmount { get; set; }

    /// <summary>Null while FX stub is in use.</summary>
    public Guid? FxRateId { get; set; }

    /// <summary>draft | finalized | reversed</summary>
    public string AllocationStatus { get; set; } = SettlementAllocationStatuses.Draft;

    public DateTimeOffset? FinalizedAt { get; set; }
    public Guid? FinalizedBy { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public Guid? ReversedBy { get; set; }
    public string? ReverseReason { get; set; }
    public string? Notes { get; set; }

    public Collection? Collection { get; set; }
    public AccountsReceivable? AccountsReceivable { get; set; }
}
