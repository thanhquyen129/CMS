using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: tenant_settings — per-tenant JSON settings (ADR-0014 / P19).
/// Financial knobs live under FinancialJson (P20).
/// </summary>
public sealed class TenantSetting : TenantEntityBase
{
    /// <summary>Optional UI defaults JSON (theme/layout).</summary>
    public string? UiJson { get; set; }

    /// <summary>
    /// Financial overrides JSON, e.g.
    /// { "maxWriteOffAmount": 5000, "recognitionPolicyMode": "require_document_link",
    ///   "defaultExceptionSlaHours": 48, "confirmApprovalThresholdBase": 100000 }
    /// </summary>
    public string? FinancialJson { get; set; }

    /// <summary>JSON: required Bill party roles and whether walk-in capture is allowed.</summary>
    public string? BillPartyPolicyJson { get; set; }
}

/// <summary>Deserialized shape of TenantSetting.FinancialJson (P16/P20).</summary>
public sealed class TenantFinancialSettings
{
    public decimal? MaxWriteOffAmount { get; set; }
    public decimal? ConfirmApprovalThresholdBase { get; set; }

    /// <summary>manual | require_document_link</summary>
    public string? RecognitionPolicyMode { get; set; }

    public string? RecognitionPolicyVersion { get; set; }

    /// <summary>Upper day bound of the first past-due aging bucket (default 30).</summary>
    public int? AgingBucket1Days { get; set; }

    /// <summary>Upper day bound of the second past-due aging bucket (default 60).</summary>
    public int? AgingBucket2Days { get; set; }

    /// <summary>Upper day bound of the third past-due aging bucket (default 90).</summary>
    public int? AgingBucket3Days { get; set; }
}

public static class RecognitionPolicyModes
{
    public const string Manual = "manual";
    /// <summary>Recognize only when exposure has FinancialDocumentId (matched/linked doc).</summary>
    public const string RequireDocumentLink = "require_document_link";
}
