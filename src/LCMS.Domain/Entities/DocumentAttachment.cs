using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: document_attachments — binary files (images/PDFs) stored on Contabo VPS local disk (/opt/cms/attachments)
/// and linked to a financial_document, bill, cost, revenue, payment, or collection.
/// Enforces tenant isolation and Cost ≠ Revenue SoD on read/write/delete.
/// </summary>
public sealed class DocumentAttachment : TenantEntityBase
{
    /// <summary>financial_document | bill | cost | revenue | payment | collection</summary>
    public string ObjectType { get; set; } = AttachmentObjectTypes.FinancialDocument;

    public Guid ObjectId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }

    /// <summary>SHA-256 hex digest of the stored file bytes for integrity verification.</summary>
    public string Sha256Hash { get; set; } = string.Empty;

    /// <summary>Relative path under Attachments:StoragePath (e.g. {tenantId}/{yyyy}/{MM}/{id}.jpg).</summary>
    public string StorageRelativePath { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public Guid? UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? DeleteReason { get; set; }
}

public static class AttachmentObjectTypes
{
    public const string FinancialDocument = "financial_document";
    public const string Bill = "bill";
    public const string Cost = "cost";
    public const string Revenue = "revenue";
    public const string Payment = "payment";
    public const string Collection = "collection";

    public static bool IsValid(string? type) =>
        type is FinancialDocument or Bill or Cost or Revenue or Payment or Collection;
}

