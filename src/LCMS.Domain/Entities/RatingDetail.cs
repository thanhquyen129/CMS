using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: rating_details (D04) — Chi tiết kết quả tính giá (Expected snapshot).
/// Denormalized codes/names so historical rating survives later rate edits via new versions.
/// </summary>
public sealed class RatingDetail : TenantEntityBase
{
    public Guid RatingId { get; set; }
    public Guid? PricingRuleId { get; set; }
    public Guid? PricingRuleComponentId { get; set; }

    public string RuleCode { get; set; } = string.Empty;
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string FinancialNature { get; set; } = "cost";

    /// <summary>expected — seed for Cost Expected (Sprint 4).</summary>
    public string FinancialMaturity { get; set; } = "expected";

    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public string? FormulaText { get; set; }

    /// <summary>base_rate | surcharge. Historical rows stay base_rate.</summary>
    public string SourceType { get; set; } = RatingSourceTypes.BaseRate;

    public Guid? SourceId { get; set; }
    public Guid? SourceVersionId { get; set; }
    public decimal? AmountOriginal { get; set; }
    public string? OriginalCurrency { get; set; }
    public decimal? ReportingAmount { get; set; }
    public decimal? LineFxRate { get; set; }
    public DateOnly? LineFxAsOf { get; set; }
    public string? LineFxSource { get; set; }

    public Rating? Rating { get; set; }
}
