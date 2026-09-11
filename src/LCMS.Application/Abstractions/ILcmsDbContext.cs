using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Abstractions;

public interface ILcmsDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Bill> Bills { get; }
    DbSet<Organization> Organizations { get; }
    DbSet<User> Users { get; }
    DbSet<BusinessParty> BusinessParties { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<Cost> Costs { get; }
    DbSet<Revenue> Revenues { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
