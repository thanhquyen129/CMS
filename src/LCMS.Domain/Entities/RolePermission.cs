using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: role_permissions — Role × Action (+ stub Data Scope).</summary>
public sealed class RolePermission : TenantEntityBase
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }

    /// <summary>Stub Data Scope: all | own (full matrix deferred).</summary>
    public string DataScope { get; set; } = DataScopes.All;
}

public static class DataScopes
{
    public const string All = "all";
    public const string Own = "own";
}
