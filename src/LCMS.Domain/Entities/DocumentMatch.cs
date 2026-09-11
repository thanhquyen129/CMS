using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: document_matches (D07) — header for one matching session (method/status/version).
/// Does not create Cost/Revenue (C-003/C-004); only links via details.
/// </summary>
public sealed class DocumentMatch : TenantEntityBase
{
    /// <summary>manual | auto (auto deferred)</summary>
    public string MatchMethod { get; set; } = DocumentMatchMethods.Manual;

    /// <summary>draft | confirmed | cancelled</summary>
    public string MatchStatus { get; set; } = DocumentMatchStatuses.Draft;

    public int VersionNo { get; set; } = 1;

    /// <summary>Optional primary document anchoring this match session.</summary>
    public Guid? PrimaryDocumentId { get; set; }

    /// <summary>Explicit over-match tolerance; Pass 1 stub = 0 (C-007).</summary>
    public decimal ToleranceAmount { get; set; }

    public string? Notes { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }

    public FinancialDocument? PrimaryDocument { get; set; }
}

public static class DocumentMatchMethods
{
    public const string Manual = "manual";
}

public static class DocumentMatchStatuses
{
    public const string Draft = "draft";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
}
