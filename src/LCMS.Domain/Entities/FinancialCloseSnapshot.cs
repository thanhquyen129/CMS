using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: financial_close_snapshots (D11) — bản chốt bất biến (as-closed history).
/// C-010 / AC-008 / TD1-DB-006: never UPDATE/DELETE business fields after insert; reopen/reclose appends new version.
/// </summary>
public sealed class FinancialCloseSnapshot : TenantEntityBase
{
    public Guid FinancialCloseId { get; set; }

    /// <summary>Copied from close at lock time for reproducibility.</summary>
    public string ScopeType { get; set; } = string.Empty;

    public Guid? ScopeId { get; set; }

    /// <summary>Monotonic snapshot version within the close (1, 2, … after reopen/reclose).</summary>
    public int SnapshotVersion { get; set; } = 1;

    public DateTimeOffset ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }

    public string PolicyVersion { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = "VND";

    /// <summary>SHA-256 hex of canonical detail payload (immutable reference).</summary>
    public string ImmutableHash { get; set; } = string.Empty;

    public FinancialClose? FinancialClose { get; set; }
}
