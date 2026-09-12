using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: approvals (D10) — phê duyệt as independent workflow on a financial object ref.
/// Permission ≠ Approval: this is not RBAC Action×Role; does not grant/check permissions.
/// </summary>
public sealed class Approval : TenantEntityBase
{
    /// <summary>cost | revenue | document | payment | collection | settlement | variance | exception | other</summary>
    public string ObjectType { get; set; } = string.Empty;

    public Guid ObjectId { get; set; }

    /// <summary>pending | approved | rejected | cancelled</summary>
    public string Status { get; set; } = ApprovalStatuses.Pending;

    /// <summary>
    /// Multi-step stub: how many sequential approve steps required (1 or 2).
    /// Permission ≠ Approval — levels are workflow only, not RBAC.
    /// </summary>
    public int RequiredLevel { get; set; } = 1;

    /// <summary>How many approve steps completed so far (0 until first approve).</summary>
    public int CurrentLevel { get; set; }

    public Guid? RequestedBy { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? RequestReason { get; set; }

    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }

    public string? Notes { get; set; }
}

public static class ApprovalObjectTypes
{
    public const string Cost = "cost";
    public const string Revenue = "revenue";
    public const string Document = "document";
    public const string Payment = "payment";
    public const string Collection = "collection";
    public const string Settlement = "settlement";
    public const string Variance = "variance";
    public const string Exception = "exception";
    public const string Other = "other";
}

public static class ApprovalStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
}
