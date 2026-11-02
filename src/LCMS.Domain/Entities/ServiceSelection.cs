using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: service_selections — persisted optional service opt-in for Bill/Shipment/Leg.</summary>
public sealed class ServiceSelection : TenantEntityBase
{
    public string ContextType { get; set; } = ServiceSelectionContexts.Bill;
    public Guid ContextId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string Scope { get; set; } = ServiceSelectionContexts.Bill;
    public decimal Quantity { get; set; } = 1m;
    public string? Uom { get; set; }
    public bool IsSelected { get; set; }
    public string? SourceRef { get; set; }
    public Guid? ConfirmedByUserId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public int Revision { get; set; } = 1;
}

/// <summary>Table: service_selection_audits — append-only history; uncheck does not delete.</summary>
public sealed class ServiceSelectionAudit : TenantEntityBase
{
    public Guid ServiceSelectionId { get; set; }
    public string ContextType { get; set; } = ServiceSelectionContexts.Bill;
    public Guid ContextId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public bool OldIsSelected { get; set; }
    public bool NewIsSelected { get; set; }
    public decimal OldQuantity { get; set; }
    public decimal NewQuantity { get; set; }
    public int Revision { get; set; }
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset ActedAt { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Table: surcharge_activation_migration_audits — classification cutover evidence.</summary>
public sealed class SurchargeActivationMigrationAudit : TenantEntityBase
{
    public string SurchargeCode { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public Guid? OldVersionId { get; set; }
    public Guid? NewVersionId { get; set; }
    public string MappingPolicy { get; set; } = SurchargeActivationPolicies.UnclassifiedLegacy;
    public string? ApprovalRef { get; set; }
    public string? Approver { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
    public int ImpactCount { get; set; }
    public string? Notes { get; set; }
}
