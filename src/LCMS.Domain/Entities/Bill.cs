using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: bills (D03) — Financial Anchor (H-003 / TD1).
/// Baseline fields per TD1 Key Aggregates; business number ≠ PK.
/// </summary>
public sealed class Bill : TenantEntityBase
{
    /// <summary>Business reference number (unique per tenant — IDX-001).</summary>
    public string BillNo { get; set; } = string.Empty;

    public string BillType { get; set; } = string.Empty;

    public string? SourceSystem { get; set; }
    public string? ExternalId { get; set; }
    public string? ExternalVersion { get; set; }

    public string OperationalStatus { get; set; } = "active";
    public bool IsActive { get; set; } = true;

    /// <summary>Owning organization for Data Scope = organization.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Primary customer party for Bill Financial View (UI-02). Ops SoT may still sync separately.</summary>
    public Guid? CustomerPartyId { get; set; }

    /// <summary>Route / corridor label (e.g. HCM-LAX). Not a TMS schedule.</summary>
    public string? RouteCode { get; set; }

    public DateTimeOffset? EtdAt { get; set; }
    public DateTimeOffset? EtaAt { get; set; }

    /// <summary>Assignee for financial-ops follow-up (display via Users.DisplayName).</summary>
    public Guid? AssignedUserId { get; set; }

    public string? Description { get; set; }

    /// <summary>Internal working note on Bill Financial View — not a ledger fact.</summary>
    public string? InternalNote { get; set; }

    public Tenant? Tenant { get; set; }
}
