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

    /// <summary>Txn currency (copied from payment; C-014 same-currency allocate).</summary>
    public string CurrencyCode { get; set; } = "VND";

    /// <summary>Allocated amount in base currency (FX stub).</summary>
    public decimal? BaseAmount { get; set; }

    /// <summary>Null while FX stub is in use.</summary>
    public Guid? FxRateId { get; set; }

    /// <summary>Amount in the payment currency before conversion.</summary>
    public decimal? OriginalAmount { get; set; }

    /// <summary>Amount applied to the payable, in the payable currency.</summary>
    public decimal? SettledAmount { get; set; }

    public decimal? FxRate { get; set; }
    public string? FxSource { get; set; }
    public DateOnly? FxRateDate { get; set; }

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
