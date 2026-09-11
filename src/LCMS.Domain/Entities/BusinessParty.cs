using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: business_parties (D02) — stub skeleton.</summary>
public sealed class BusinessParty : TenantEntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
