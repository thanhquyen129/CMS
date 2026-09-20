using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: audit_events (D12) — business event trail (actor/action/object/before-after/reason).
/// IDX-013: tenant_id, object_type, object_id, occurred_at.
/// </summary>
public sealed class AuditEvent : TenantEntityBase
{
    /// <summary>Actor principal (X-User-Id / JWT later). Null when system/anonymous.</summary>
    public Guid? ActorId { get; set; }

    /// <summary>Stable action code, e.g. cost.create, cost.confirm, revenue.create.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Object type CodeKey-ish, e.g. cost, revenue, payment_allocation, financial_close_snapshot.</summary>
    public string ObjectType { get; set; } = string.Empty;

    public Guid ObjectId { get; set; }

    /// <summary>Optional JSON snapshot before mutation.</summary>
    public string? BeforeJson { get; set; }

    /// <summary>Optional JSON snapshot after mutation.</summary>
    public string? AfterJson { get; set; }

    public string? Reason { get; set; }

    /// <summary>Request correlation id (X-Correlation-Id).</summary>
    public string? CorrelationId { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class AuditActions
{
    public const string CostCreate = "cost.create";
    public const string CostConfirm = "cost.confirm";
    public const string RevenueCreate = "revenue.create";
    public const string PaymentAllocationFinalize = "payment_allocation.finalize";
    public const string CollectionAllocationFinalize = "collection_allocation.finalize";
    public const string AccountsPayableWriteOff = "accounts_payable.write_off";
    public const string AccountsReceivableWriteOff = "accounts_receivable.write_off";
    public const string AccountsPayableRecognize = "accounts_payable.recognize";
    public const string AccountsReceivableRecognize = "accounts_receivable.recognize";
    public const string AccountsPayableReverseRecognize = "accounts_payable.reverse_recognize";
    public const string AccountsReceivableReverseRecognize = "accounts_receivable.reverse_recognize";
    public const string FinancialDocumentAccept = "financial_document.accept";
    public const string FinancialDocumentLineAdd = "financial_document_line.add";
    public const string FinancialDocumentLineUpdate = "financial_document_line.update";
    public const string FinancialDocumentLineDelete = "financial_document_line.delete";
    public const string DocumentMatchDetailAdd = "document_match.detail_add";
    public const string DocumentMatchConfirm = "document_match.confirm";
    public const string FinancialCloseSnapshotCreate = "financial_close_snapshot.create";
    public const string WaybillCapture = "bill_waybill.capture";
    public const string WaybillUpdate = "bill_waybill.update";
    public const string BusinessPartyCreate = "business_party.create";
    public const string BusinessPartyUpdate = "business_party.update";
    public const string BusinessPartyDelete = "business_party.delete";
    public const string BusinessPartyBlock = "business_party.block";
    public const string BusinessPartyUnblock = "business_party.unblock";
    public const string PartyRoleAssign = "party_role.assign";
    public const string PartyRoleRevoke = "party_role.revoke";
    public const string PartyBankAccountUpsert = "party_bank_account.upsert";
    public const string PartyBankAccountDelete = "party_bank_account.delete";
    public const string PartyContactUpsert = "party_contact.upsert";
    public const string PartyContactDelete = "party_contact.delete";
}

public static class AuditObjectTypes
{
    public const string Cost = "cost";
    public const string Revenue = "revenue";
    public const string PaymentAllocation = "payment_allocation";
    public const string CollectionAllocation = "collection_allocation";
    public const string AccountsPayable = "accounts_payable";
    public const string AccountsReceivable = "accounts_receivable";
    public const string FinancialDocument = "financial_document";
    public const string FinancialDocumentLine = "financial_document_line";
    public const string DocumentMatch = "document_match";
    public const string FinancialCloseSnapshot = "financial_close_snapshot";
    public const string BillWaybill = "bill_waybill";
    public const string BusinessParty = "business_party";
    public const string PartyBankAccount = "party_bank_account";
    public const string PartyContact = "party_contact";
    public const string PartyRole = "party_role";
}
