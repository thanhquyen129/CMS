using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: document_match_details (D07) — N:N detail matching.
/// Targets: another document line, or stub link to existing Cost/Revenue (no invent).
/// </summary>
public sealed class DocumentMatchDetail : TenantEntityBase
{
    public Guid MatchId { get; set; }
    public Guid SourceLineId { get; set; }

    /// <summary>Line↔line target (nullable when linking Cost/Revenue).</summary>
    public Guid? TargetLineId { get; set; }

    /// <summary>Stub link to existing Cost — must not create Cost (C-003).</summary>
    public Guid? TargetCostId { get; set; }

    /// <summary>Stub link to existing Revenue — must not create Revenue (C-004).</summary>
    public Guid? TargetRevenueId { get; set; }

    public decimal MatchedAmount { get; set; }

    public DocumentMatch? Match { get; set; }
    public FinancialDocumentLine? SourceLine { get; set; }
    public FinancialDocumentLine? TargetLine { get; set; }
    public Cost? TargetCost { get; set; }
    public Revenue? TargetRevenue { get; set; }
}
