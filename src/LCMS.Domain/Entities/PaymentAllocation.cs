using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: payment_allocations (D09) — N:N Payment ↔ AccountsPayable.
/// Only finalized rows affect AP.FinalizedSettledAmount / outstanding (AC-007).
/// Reversal sets status reversed — no hard delete (C-013).
/// </summary>
public sealed class PaymentAllocation : TenantEntityBase
{
    public Guid PaymentId { get; set; }
    public Guid AccountsPayableId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>draft | finalized | reversed</summary>
    public string AllocationStatus { get; set; } = SettlementAllocationStatuses.Draft;

    public DateTimeOffset? FinalizedAt { get; set; }
    public Guid? FinalizedBy { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public Guid? ReversedBy { get; set; }
    public string? ReverseReason { get; set; }
    public string? Notes { get; set; }

    public Payment? Payment { get; set; }
    public AccountsPayable? AccountsPayable { get; set; }
}

public static class SettlementAllocationStatuses
{
    public const string Draft = "draft";
    public const string Finalized = "finalized";
    public const string Reversed = "reversed";
}
