using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: financial_documents (D07) — intake of DN/Invoice/other.
/// State dimensions are independent: Received ≠ Accepted ≠ Matched (AC-005 / H-guardrail).
/// Receiving a document must not invent Cost/Revenue (C-003 / C-004).
/// </summary>
public sealed class FinancialDocument : TenantEntityBase
{
    /// <summary>dn | invoice | credit_note | debit_note | other</summary>
    public string DocumentType { get; set; } = FinancialDocumentTypes.Invoice;

    public string DocumentNo { get; set; } = string.Empty;
    public Guid? CounterpartyId { get; set; }
    public Guid? BillId { get; set; }

    /// <summary>payable (vendor DN/invoice) | receivable (customer invoice)</summary>
    public string Direction { get; set; } = FinancialDocumentDirections.Payable;

    public decimal TotalAmount { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public DateOnly DocumentDate { get; set; }

    /// <summary>not_received | received — independent of acceptance/matching.</summary>
    public string ReceiptStatus { get; set; } = FinancialDocumentReceiptStatuses.NotReceived;

    /// <summary>not_accepted | accepted | rejected — independent of receipt/matching.</summary>
    public string AcceptanceStatus { get; set; } = FinancialDocumentAcceptanceStatuses.NotAccepted;

    /// <summary>unmatched | partially_matched | matched — independent of receipt/acceptance.</summary>
    public string MatchingStatus { get; set; } = FinancialDocumentMatchingStatuses.Unmatched;

    public DateTimeOffset? ReceivedAt { get; set; }
    public Guid? ReceivedBy { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? AcceptedBy { get; set; }

    public string RecordStatus { get; set; } = "active";
    public string? Notes { get; set; }
    public string? SourceSystem { get; set; }
    public string? ExternalId { get; set; }

    public Bill? Bill { get; set; }
}

public static class FinancialDocumentTypes
{
    public const string Dn = "dn";
    public const string Invoice = "invoice";
    public const string CreditNote = "credit_note";
    public const string DebitNote = "debit_note";
    public const string Other = "other";
}

public static class FinancialDocumentDirections
{
    public const string Payable = "payable";
    public const string Receivable = "receivable";
}

public static class FinancialDocumentReceiptStatuses
{
    public const string NotReceived = "not_received";
    public const string Received = "received";
}

public static class FinancialDocumentAcceptanceStatuses
{
    public const string NotAccepted = "not_accepted";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
}

public static class FinancialDocumentMatchingStatuses
{
    public const string Unmatched = "unmatched";
    public const string PartiallyMatched = "partially_matched";
    public const string Matched = "matched";
}
