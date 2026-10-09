using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: financial_document_lines (D07) — detail lines for N:N matching (C-007).
/// Open amount = Amount − MatchedAmount; over-match forbidden (tolerance stub = 0).
/// </summary>
public sealed class FinancialDocumentLine : TenantEntityBase
{
    public Guid DocumentId { get; set; }
    public int LineNo { get; set; }
    public string? Description { get; set; }

    /// <summary>Line face amount (open amount baseline).</summary>
    public decimal Amount { get; set; }

    /// <summary>Cached sum of active match details involving this line.</summary>
    public decimal MatchedAmount { get; set; }

    public string CurrencyCode { get; set; } = "VND";
    public Guid? BillId { get; set; }
    public string? CostTypeCode { get; set; }
    public string? RevenueTypeCode { get; set; }
    public Guid? EconomicChargeTypeId { get; set; }
    public Guid? RatingDetailId { get; set; }
    public decimal? NetAmount { get; set; }
    public decimal? VatRate { get; set; }
    public decimal? VatAmount { get; set; }
    public decimal? GrossAmount { get; set; }

    /// <summary>Document VAT minus the rating snapshot. The rating row is not rewritten.</summary>
    public decimal? VatVarianceAmount { get; set; }

    public FinancialDocument? Document { get; set; }
}
