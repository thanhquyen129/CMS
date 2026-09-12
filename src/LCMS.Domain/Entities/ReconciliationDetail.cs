using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: reconciliation_details (D10) — source/target/matched/variance line.
/// May spawn a <see cref="Variance"/> control fact; never invents Exception.
/// </summary>
public sealed class ReconciliationDetail : TenantEntityBase
{
    public Guid ReconciliationId { get; set; }

    /// <summary>payment | collection | cost | revenue | document | accounts_payable | accounts_receivable | other</summary>
    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }

    public decimal SourceAmount { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal MatchedAmount { get; set; }

    /// <summary>Derived: SourceAmount − MatchedAmount (control delta on this line).</summary>
    public decimal VarianceAmount { get; set; }

    public string CurrencyCode { get; set; } = "VND";

    /// <summary>matched | variance | unmatched</summary>
    public string LineStatus { get; set; } = ReconciliationDetailStatuses.Unmatched;

    /// <summary>Optional link to derived Variance row (control fact).</summary>
    public Guid? VarianceId { get; set; }

    public string? Notes { get; set; }

    public Reconciliation? Reconciliation { get; set; }
    public Variance? Variance { get; set; }
}

public static class ReconciliationDetailStatuses
{
    public const string Matched = "matched";
    public const string Variance = "variance";
    public const string Unmatched = "unmatched";
}

public static class ReconciliationObjectTypes
{
    public const string Payment = "payment";
    public const string Collection = "collection";
    public const string Cost = "cost";
    public const string Revenue = "revenue";
    public const string Document = "document";
    public const string AccountsPayable = "accounts_payable";
    public const string AccountsReceivable = "accounts_receivable";
    public const string BankLine = "bank_line";
    public const string Other = "other";
}
