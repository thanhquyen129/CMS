using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: reconciliations (D10) — phiên đối soát (Type/rule/version/status).
/// Independent state dimension from matching/settlement/close.
/// </summary>
public sealed class Reconciliation : TenantEntityBase
{
    /// <summary>manual | payment_ap | collection_ar | document | cost_revenue</summary>
    public string ReconciliationType { get; set; } = ReconciliationTypes.Manual;

    /// <summary>Optional control rule code / policy key.</summary>
    public string? RuleCode { get; set; }

    public int VersionNo { get; set; } = 1;

    /// <summary>draft | in_progress | completed | cancelled</summary>
    public string Status { get; set; } = ReconciliationStatuses.Draft;

    public Guid? BillId { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public Guid? StartedBy { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CompletedBy { get; set; }

    public Bill? Bill { get; set; }
}

public static class ReconciliationTypes
{
    public const string Manual = "manual";
    public const string PaymentAp = "payment_ap";
    public const string CollectionAr = "collection_ar";
    public const string Document = "document";
    public const string CostRevenue = "cost_revenue";
}

public static class ReconciliationStatuses
{
    public const string Draft = "draft";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
}
