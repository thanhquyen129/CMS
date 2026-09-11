using LCMS.Application.Abstractions;
using LCMS.Domain.Common;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Infrastructure.Persistence;

public sealed class LcmsDbContext : DbContext, ILcmsDbContext
{
    private readonly ITenantContext _tenantContext;

    public LcmsDbContext(DbContextOptions<LcmsDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BusinessParty> BusinessParties => Set<BusinessParty>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Cost> Costs => Set<Cost>();
    public DbSet<Revenue> Revenues => Set<Revenue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LcmsDbContext).Assembly);
        ApplyTenantAndSoftDeleteFilters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditAndConcurrency();
        RejectHardDeletes();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditAndConcurrency();
        RejectHardDeletes();
        return base.SaveChanges();
    }

    /// <summary>
    /// C-001 global tenant filter + C-013 soft-delete filter.
    /// Tenant root table filters by soft-delete only (Id is the tenant).
    /// </summary>
    private void ApplyTenantAndSoftDeleteFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>().HasQueryFilter(t => t.DeletedAt == null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType == typeof(Tenant))
            {
                continue;
            }

            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                // Captures this DbContext instance for current tenant resolution.
                var method = typeof(LcmsDbContext)
                    .GetMethod(nameof(SetTenantScopedFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
            else
            {
                var method = typeof(LcmsDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
        }
    }

    private void SetTenantScopedFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantScoped, ISoftDeletable
    {
        // Expression-tree safe: do not call ITenantContext.HasTenant default member.
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            e.DeletedAt == null
            && (_tenantContext.TenantId == null
                || _tenantContext.TenantId == Guid.Empty
                || e.TenantId == _tenantContext.TenantId));
    }

    private void SetSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.DeletedAt == null);
    }

    private void StampAuditAndConcurrency()
    {
        var utc = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<EntityBase>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.TouchRowVersion();
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = utc;
                }
                else
                {
                    entry.Entity.UpdatedAt = utc;
                }
            }
        }
    }

    /// <summary>C-013 / TD1-DB-005: block physical DELETE; use SoftDelete().</summary>
    private void RejectHardDeletes()
    {
        var hardDeletes = ChangeTracker.Entries<ISoftDeletable>()
            .Where(e => e.State == EntityState.Deleted)
            .ToList();

        if (hardDeletes.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Cấm xóa vật lý (hard delete). Dùng SoftDelete() hoặc cancel/reversal/adjustment theo TD1 C-013.");
    }
}
