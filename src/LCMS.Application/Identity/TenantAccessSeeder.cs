using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Identity;

/// <summary>Seeds global permission catalog and tenant Admin role (Sprint 1).</summary>
public static class TenantAccessSeeder
{
    public const string AdminRoleCode = "Admin";

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

    public static async Task SeedAdminRoleAsync(ILcmsDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsurePermissionCatalogAsync(db, cancellationToken);

        var admin = await db.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Code == AdminRoleCode, cancellationToken);

        if (admin is null)
        {
            admin = new Role
            {
                TenantId = tenantId,
                Code = AdminRoleCode,
                Name = "Quản trị",
                IsSystem = true
            };
            db.Roles.Add(admin);
            await db.SaveChangesAsync(cancellationToken);
        }

        var coreCodes = PermissionCodes.CoreActionCodes;
        var permissionIds = await db.Permissions
            .Where(p => coreCodes.Contains(p.ActionCode))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var existingPermIds = await db.RolePermissions
            .Where(rp => rp.TenantId == tenantId && rp.RoleId == admin.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);
        var existingSet = existingPermIds.ToHashSet();

        foreach (var permissionId in permissionIds)
        {
            if (existingSet.Contains(permissionId))
            {
                continue;
            }

            db.RolePermissions.Add(new RolePermission
            {
                TenantId = tenantId,
                RoleId = admin.Id,
                PermissionId = permissionId,
                DataScope = DataScopes.All
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
