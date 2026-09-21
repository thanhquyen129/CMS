using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: tenant_license_modules — plan inclusion + operator enable flag (H-001: hide UI, do not fork schema).
/// </summary>
public sealed class TenantLicenseModule : TenantEntityBase
{
    public Guid LicenseId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public bool IncludedInPlan { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
}
