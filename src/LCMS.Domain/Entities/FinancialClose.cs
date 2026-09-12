using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: financial_closes (D11) — lần chốt tài chính (open / reopen / reclose version).
/// Close session is mutable for status; as-closed history lives in immutable snapshots (C-010).
/// </summary>
public sealed class FinancialClose : TenantEntityBase
{
    /// <summary>period | bill | tenant</summary>
    public string ScopeType { get; set; } = FinancialCloseScopeTypes.Period;

    /// <summary>Bill id when scope is bill; otherwise optional scope key.</summary>
    public Guid? ScopeId { get; set; }

    public DateOnly? PeriodFrom { get; set; }
    public DateOnly? PeriodTo { get; set; }

    /// <summary>Close cycle version for this scope (increments on reclose via new row or reopen→snapshot).</summary>
    public int VersionNo { get; set; } = 1;

    /// <summary>open | locked | reopened</summary>
    public string Status { get; set; } = FinancialCloseStatuses.Open;

    /// <summary>controlled | strict — Strict forces eligibility + period lock (Pass 2 Sprint 10 FULL).</summary>
    public string PolicyVersion { get; set; } = FinancialClosePolicies.Controlled;

    public string BaseCurrency { get; set; } = "VND";
    public string? Notes { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public Guid? StartedBy { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public Guid? LockedBy { get; set; }
    public DateTimeOffset? ReopenedAt { get; set; }
    public Guid? ReopenedBy { get; set; }
    public string? ReopenReason { get; set; }

    /// <summary>Previous locked close when this row is a reclose superseding version.</summary>
    public Guid? SupersedesCloseId { get; set; }

    public Bill? Bill { get; set; }
}

public static class FinancialCloseScopeTypes
{
    public const string Period = "period";
    public const string Bill = "bill";
    public const string Tenant = "tenant";
}

public static class FinancialCloseStatuses
{
    public const string Open = "open";
    public const string Locked = "locked";
    public const string Reopened = "reopened";
}

public static class FinancialClosePolicies
{
    public const string Controlled = "controlled";
    public const string Strict = "strict";
}
