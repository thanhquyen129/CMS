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

    public Tenant? Tenant { get; set; }
}
