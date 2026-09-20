using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: party_contacts — partner contact persons.</summary>
public sealed class PartyContact : TenantEntityBase
{
    public Guid PartyId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }

    /// <summary>general | billing | ops | legal</summary>
    public string FunctionCode { get; set; } = PartyContactFunctions.General;
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
}
