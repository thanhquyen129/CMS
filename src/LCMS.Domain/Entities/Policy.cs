using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: policies — versioned tenant policy registry (FR-011 / POL-01).</summary>
public sealed class Policy : TenantEntityBase
{
    /// <summary>Stable key, e.g. DOCUMENT_MATCH_TOLERANCE.</summary>
    public string PolicyKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Owner user id (controller / policy owner).</summary>
    public Guid? OwnerUserId { get; set; }

    public int Version { get; set; } = 1;

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    /// <summary>draft | active | superseded | retired</summary>
    public string Status { get; set; } = PolicyStatuses.Draft;

    /// <summary>JSON body for the policy knobs.</summary>
    public string? BodyJson { get; set; }

    public string? Notes { get; set; }
}

public static class PolicyStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Superseded = "superseded";
    public const string Retired = "retired";
}

/// <summary>Canonical 13 policy keys (MASTER Policy Registry).</summary>
public static class PolicyKeys
{
    public const string OperationalSourceOwnership = "OPERATIONAL_SOURCE_OWNERSHIP";
    public const string Rating = "RATING_POLICY";
    public const string CostAllocation = "COST_ALLOCATION_POLICY";
    public const string RevenueSourceOfTruth = "REVENUE_SOT_POLICY";
    public const string RevenueMapping = "REVENUE_MAPPING_POLICY";
    public const string DocumentMatch = "DOCUMENT_MATCH_POLICY";
    public const string Recognition = "AP_AR_RECOGNITION_POLICY";
    public const string Aging = "AGING_POLICY";
    public const string Settlement = "SETTLEMENT_POLICY";
    public const string Tolerance = "RECON_TOLERANCE_POLICY";
    public const string Approval = "APPROVAL_POLICY";
    public const string FinancialClose = "FINANCIAL_CLOSE_POLICY";
    public const string ReportingFx = "REPORTING_FX_POLICY";

    public static readonly IReadOnlyList<string> All =
    [
        OperationalSourceOwnership,
        Rating,
        CostAllocation,
        RevenueSourceOfTruth,
        RevenueMapping,
        DocumentMatch,
        Recognition,
        Aging,
        Settlement,
        Tolerance,
        Approval,
        FinancialClose,
        ReportingFx
    ];

    public static readonly IReadOnlyDictionary<string, string> DefaultTitles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [OperationalSourceOwnership] = "Sở hữu nguồn vận hành",
            [Rating] = "Chính sách tính giá",
            [CostAllocation] = "Phân bổ chi phí",
            [RevenueSourceOfTruth] = "Nguồn sự thật doanh thu",
            [RevenueMapping] = "Chia doanh thu nhiều Bill",
            [DocumentMatch] = "Khớp chứng từ",
            [Recognition] = "Ghi nhận AP/AR",
            [Aging] = "Tuổi nợ",
            [Settlement] = "Tất toán",
            [Tolerance] = "Dung sai đối soát / khớp",
            [Approval] = "Phê duyệt",
            [FinancialClose] = "Chốt tài chính",
            [ReportingFx] = "Tỷ giá báo cáo"
        };
}
