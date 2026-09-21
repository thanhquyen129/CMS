using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: orders (D03) — operational reference; Ops SoT identity via source_system + external_id (C-002).
/// </summary>
public sealed class Order : TenantEntityBase
{
    /// <summary>Business reference number (display / ops). Not the external sync key alone.</summary>
    public string OrderNo { get; set; } = string.Empty;

    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalVersion { get; set; }

    public string OperationalStatus { get; set; } = "active";
    public bool IsActive { get; set; } = true;

    /// <summary>Primary customer for list/filter and later Bill linking. Not a TMS booking owner.</summary>
    public Guid? CustomerPartyId { get; set; }

    public Guid? AssignedUserId { get; set; }

    /// <summary>air / sea / road / rail — rating context, not a dispatch mode.</summary>
    public string? TransportMode { get; set; }

    public string? OriginCode { get; set; }
    public string? DestinationCode { get; set; }
    public string? RouteCode { get; set; }

    public DateTimeOffset? EtdAt { get; set; }
    public DateTimeOffset? EtaAt { get; set; }

    public string? CustomerReference { get; set; }
    public string? Description { get; set; }

    /// <summary>Remaining create-form fields (cargo, extra services) as JSON — not TMS execution state.</summary>
    public string? ContextJson { get; set; }

    public Tenant? Tenant { get; set; }
}
