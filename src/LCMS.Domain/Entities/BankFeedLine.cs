using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Manual / imported bank statement line (cash feed stub — ADR-0013).
/// Not a Payment/Collection; matching happens via reconciliation (sourceType = bank_line).
/// </summary>
public sealed class BankFeedLine : TenantEntityBase
{
    public DateOnly ValueDate { get; set; }

    /// <summary>Signed bank amount as absolute; direction separates in vs out.</summary>
    public decimal Amount { get; set; }

    public string CurrencyCode { get; set; } = "VND";

    /// <summary>credit = tiền vào (thu); debit = tiền ra (chi).</summary>
    public string Direction { get; set; } = BankFeedDirections.Credit;

    public string? BankReference { get; set; }
    public string? CounterpartyName { get; set; }
    public string? Description { get; set; }

    /// <summary>unmatched | matched | ignored</summary>
    public string Status { get; set; } = BankFeedLineStatuses.Unmatched;

    public Guid? MatchedReconciliationDetailId { get; set; }
    public DateTimeOffset? MatchedAt { get; set; }
    public DateTimeOffset? IgnoredAt { get; set; }
    public string? IgnoreReason { get; set; }
}

public static class BankFeedDirections
{
    public const string Credit = "credit";
    public const string Debit = "debit";
}

public static class BankFeedLineStatuses
{
    public const string Unmatched = "unmatched";
    public const string Matched = "matched";
    public const string Ignored = "ignored";
}
