using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Identity;

/// <summary>Seeds global permission catalog and tenant system roles (ADR-0016).</summary>
public static class TenantAccessSeeder
{
    public const string AdminRoleCode = SystemRoleCatalog.Admin;

    public static async Task EnsurePermissionCatalogAsync(ILcmsDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Permissions
            .Select(p => p.ActionCode)
            .ToListAsync(cancellationToken);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, name) in PermissionCodes.CoreCatalog)
        {
            if (set.Contains(code))
            {
                continue;
            }

            db.Permissions.Add(new Permission { ActionCode = code, Name = name });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Backward-compatible entry: seeds Admin + other system roles.</summary>
    public static Task SeedAdminRoleAsync(ILcmsDbContext db, Guid tenantId, CancellationToken cancellationToken) =>
        SeedSystemRolesAsync(db, tenantId, cancellationToken);

    public static async Task SeedSystemRolesAsync(ILcmsDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsurePermissionCatalogAsync(db, cancellationToken);

        var permissions = await db.Permissions
            .Where(p => PermissionCodes.CoreActionCodes.Contains(p.ActionCode))
            .ToDictionaryAsync(p => p.ActionCode, p => p.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var def in SystemRoleCatalog.All)
        {
            var role = await db.Roles
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code == def.Code, cancellationToken);

            if (role is null)
            {
                role = new Role
                {
                    TenantId = tenantId,
                    Code = def.Code,
                    Name = def.Name,
                    IsSystem = true
                };
                db.Roles.Add(role);
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (!role.IsSystem)
            {
                role.IsSystem = true;
                await db.SaveChangesAsync(cancellationToken);
            }

            // Insert missing default grants for every system role (new catalog actions on existing tenants).
            // Does not restore rows an Admin already revoked (match by permission id).
            await EnsureRolePermissionsAsync(
                db, tenantId, role.Id, def.Permissions, permissions, cancellationToken);
        }
    }

    public static async Task SeedSystemRolesForAllTenantsAsync(
        ILcmsDbContext db,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionCatalogAsync(db, cancellationToken);
        var tenantIds = await db.Tenants.Select(t => t.Id).ToListAsync(cancellationToken);
        foreach (var tenantId in tenantIds)
        {
            await SeedSystemRolesAsync(db, tenantId, cancellationToken);
        }
    }

    private static async Task EnsureRolePermissionsAsync(
        ILcmsDbContext db,
        Guid tenantId,
        Guid roleId,
        IReadOnlyList<(string ActionCode, string DataScope)> grants,
        IReadOnlyDictionary<string, Guid> permissions,
        CancellationToken cancellationToken)
    {
        var existingRows = await db.RolePermissions
            .IgnoreQueryFilters()
            .Where(rp => rp.TenantId == tenantId && rp.RoleId == roleId)
            .ToListAsync(cancellationToken);
        var byPermissionId = existingRows.ToDictionary(rp => rp.PermissionId);

        foreach (var (actionCode, dataScope) in grants)
        {
            if (!permissions.TryGetValue(actionCode, out var permissionId))
            {
                continue;
            }

            if (byPermissionId.ContainsKey(permissionId))
            {
                // Keep Admin soft-revokes; only insert missing catalog rows.
                continue;
            }

            db.RolePermissions.Add(new RolePermission
            {
                TenantId = tenantId,
                RoleId = roleId,
                PermissionId = permissionId,
                DataScope = dataScope
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
