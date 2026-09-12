using LCMS.Api.Auth;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Api.Auth;

/// <summary>
/// Ensures one operator user from env (Auth:Bootstrap:*) — never commit secrets.
/// Idempotent: updates password hash when email already exists in tenant.
/// </summary>
public static class BootstrapUserSeeder
{
    public static async Task EnsureAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value.Bootstrap;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("BootstrapUserSeeder");

        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            logger.LogInformation("Auth bootstrap skipped (Auth:Bootstrap:Email/Password not set).");
            return;
        }

        if (options.Password.Length < 8)
        {
            logger.LogWarning("Auth bootstrap skipped: password must be at least 8 characters.");
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        var tenantId = await ResolveTenantAsync(db, options, logger, cancellationToken);
        if (tenantId is null)
        {
            return;
        }

        await TenantAccessSeeder.SeedAdminRoleAsync(db, tenantId.Value, cancellationToken);

        var email = options.Email.Trim().ToLowerInvariant();
        var displayName = string.IsNullOrWhiteSpace(options.DisplayName)
            ? "Operator"
            : options.DisplayName.Trim();

        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Email == email && u.DeletedAt == null, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                TenantId = tenantId.Value,
                Email = email,
                DisplayName = displayName,
                IsActive = true
            };
            user.PasswordHash = hasher.HashPassword(user, options.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Auth bootstrap created user {Email} for tenant {TenantId}.", email, tenantId);
        }
        else
        {
            user.DisplayName = displayName;
            user.IsActive = true;
            user.PasswordHash = hasher.HashPassword(user, options.Password);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Auth bootstrap refreshed password for {Email}.", email);
        }

        var adminRole = await db.Roles
            .FirstAsync(r => r.TenantId == tenantId && r.Code == TenantAccessSeeder.AdminRoleCode, cancellationToken);

        var hasRole = await db.UserRoles.AnyAsync(
            ur => ur.TenantId == tenantId && ur.UserId == user.Id && ur.RoleId == adminRole.Id,
            cancellationToken);
        if (!hasRole)
        {
            db.UserRoles.Add(new UserRole
            {
                TenantId = tenantId.Value,
                UserId = user.Id,
                RoleId = adminRole.Id
            });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<Guid?> ResolveTenantAsync(
        LcmsDbContext db,
        BootstrapOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (options.TenantId is Guid tid && tid != Guid.Empty)
        {
            var exists = await db.Tenants.AnyAsync(t => t.Id == tid, cancellationToken);
            if (exists)
            {
                return tid;
            }

            logger.LogWarning("Auth bootstrap TenantId {TenantId} not found.", tid);
            return null;
        }

        var code = string.IsNullOrWhiteSpace(options.TenantCode) ? "ops" : options.TenantCode.Trim();
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
        if (tenant is not null)
        {
            return tenant.Id;
        }

        tenant = new Tenant
        {
            Code = code,
            Name = string.IsNullOrWhiteSpace(options.TenantName) ? "Vận hành" : options.TenantName.Trim(),
            IsActive = true
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Auth bootstrap created tenant {Code} ({TenantId}).", code, tenant.Id);
        return tenant.Id;
    }
}
