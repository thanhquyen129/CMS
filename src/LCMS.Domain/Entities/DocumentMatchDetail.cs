using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: document_match_details (D07) — N:N detail matching.
/// Targets: another document line, or stub link to existing Cost/Revenue (no invent).
/// Reverse/cancel sets status — no hard delete (C-013 / RV-003).
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

    /// <summary>active | reversed</summary>
    public string DetailStatus { get; set; } = DocumentMatchDetailStatuses.Active;

    public DateTimeOffset? ReversedAt { get; set; }
    public Guid? ReversedBy { get; set; }
    public string? ReverseReason { get; set; }

    public DocumentMatch? Match { get; set; }
    public FinancialDocumentLine? SourceLine { get; set; }
    public FinancialDocumentLine? TargetLine { get; set; }
    public Cost? TargetCost { get; set; }
    public Revenue? TargetRevenue { get; set; }
}

public static class DocumentMatchDetailStatuses
{
    public const string Active = "active";
    public const string Reversed = "reversed";
}
