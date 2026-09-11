namespace LCMS.Application.Abstractions;

/// <summary>
/// Current tenant from authenticated session (C-001 isolation).
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    bool HasTenant => TenantId.HasValue && TenantId.Value != Guid.Empty;
}
