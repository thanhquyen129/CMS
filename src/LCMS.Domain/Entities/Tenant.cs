using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>
/// Table: tenants (D01). Root of C-001 isolation — <see cref="EntityBase.Id"/> is the tenant identity.
/// TD1: không xóa vật lý khi đã có dữ liệu → soft-delete only.
/// </summary>
public sealed class Tenant : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
