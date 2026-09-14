using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Api.Auth;

/// <summary>
/// Idempotent UAT users — one login per system role (ADR-0016).
/// Password from Auth:RoleDemoUsers (env on host). Soft-revived if previously deleted.
/// </summary>
public static class RoleDemoUserSeeder
{
    public static readonly IReadOnlyList<(string Email, string DisplayName, string RoleCode)> Accounts =
    [
        ("admin@cms.local", "Quản trị", SystemRoleCatalog.Admin),
        ("controller@cms.local", "Kiểm soát tài chính", SystemRoleCatalog.FinancialController),
        ("cost@cms.local", "Kế toán chi phí", SystemRoleCatalog.CostAccountant),
        ("revenue@cms.local", "Kế toán doanh thu", SystemRoleCatalog.RevenueAccountant),
        ("ops.user@cms.local", "Điều vận", SystemRoleCatalog.Ops),
        ("master@cms.local", "Quản trị danh mục", SystemRoleCatalog.MasterData),
        ("viewer@cms.local", "Chỉ xem Bill", SystemRoleCatalog.Viewer)
    ];

    public static async Task EnsureAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value.RoleDemoUsers;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RoleDemoUserSeeder");

        if (!options.Enabled)
        {
            logger.LogInformation("Role demo users skipped (Auth:RoleDemoUsers:Enabled=false).");
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogInformation("Role demo users skipped (Auth:RoleDemoUsers:Password not set).");
            return;
        }

        var password = options.Password;
        if (password.Length < 6)
        {
            logger.LogWarning("Role demo users skipped: password must be at least 6 characters.");
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            logger.LogWarning("Role demo users skipped: database unreachable.");
            return;
        }

        // Test hosts may start before EnsureCreated — avoid crashing Process.
        try
        {
            _ = await db.Tenants.AsNoTracking().AnyAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Role demo users skipped: schema not ready.");
            return;
        }

        var tenantCode = string.IsNullOrWhiteSpace(options.TenantCode) ? "ops" : options.TenantCode.Trim();
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Code == tenantCode, cancellationToken);
        if (tenant is null)
        {
            logger.LogWarning("Role demo users skipped: tenant {Code} not found.", tenantCode);
            return;
        }

        await TenantAccessSeeder.SeedSystemRolesAsync(db, tenant.Id, cancellationToken);

        var roles = await db.Roles
            .Where(r => r.TenantId == tenant.Id)
            .ToDictionaryAsync(r => r.Code, r => r, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var (emailRaw, displayName, roleCode) in Accounts)
        {
            if (!roles.TryGetValue(roleCode, out var role))
            {
                logger.LogWarning("Role demo skip {Email}: role {Role} missing.", emailRaw, roleCode);
                continue;
            }

            var email = emailRaw.Trim().ToLowerInvariant();
            var user = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    u => u.TenantId == tenant.Id && u.Email == email && u.DeletedAt == null,
                    cancellationToken);

            if (user is null)
            {
                user = new User
                {
                    TenantId = tenant.Id,
                    Email = email,
                    DisplayName = displayName,
                    IsActive = true
                };
                user.PasswordHash = hasher.HashPassword(user, password);
                db.Users.Add(user);
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Role demo created {Email} → {Role}.", email, roleCode);
            }
            else
            {
                user.DisplayName = displayName;
                user.IsActive = true;
                user.PasswordHash = hasher.HashPassword(user, password);
                await db.SaveChangesAsync(cancellationToken);
            }

            var link = await db.UserRoles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    ur => ur.TenantId == tenant.Id && ur.UserId == user.Id && ur.RoleId == role.Id,
                    cancellationToken);

            if (link is null)
            {
                db.UserRoles.Add(new UserRole
                {
                    TenantId = tenant.Id,
                    UserId = user.Id,
                    RoleId = role.Id
                });
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (link.IsDeleted)
            {
                link.DeletedAt = null;
                link.DeletedBy = null;
                link.TouchRowVersion();
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
