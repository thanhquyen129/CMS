using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: document_matches (D07) — header for one matching session (method/status/version).
/// Does not create Cost/Revenue (C-003/C-004); only links via details.
/// </summary>
public sealed class DocumentMatch : TenantEntityBase
{
    /// <summary>line_to_line | line_to_cost | line_to_revenue</summary>
    public string MatchMethod { get; set; } = DocumentMatchMethods.LineToLine;

    /// <summary>draft | confirmed | cancelled</summary>
    public string MatchStatus { get; set; } = DocumentMatchStatuses.Draft;

    public int VersionNo { get; set; } = 1;

    /// <summary>Optional primary document anchoring this match session.</summary>
    public Guid? PrimaryDocumentId { get; set; }

    /// <summary>Absolute over-match tolerance for C-007 (from policy or override).</summary>
    public decimal ToleranceAmount { get; set; }

    /// <summary>Percent of line amount allowed as over-match (policy stub; 0 = off).</summary>
    public decimal TolerancePercent { get; set; }

    public string? Notes { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancelReason { get; set; }

    public FinancialDocument? PrimaryDocument { get; set; }
}

public static class DocumentMatchMethods
{
    public const string LineToLine = "line_to_line";
    public const string LineToCost = "line_to_cost";
    public const string LineToRevenue = "line_to_revenue";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        LineToLine,
        LineToCost,
        LineToRevenue
    };
}

public static class DocumentMatchStatuses
{
    public const string Draft = "draft";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
}
