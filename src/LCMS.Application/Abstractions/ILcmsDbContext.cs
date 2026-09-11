using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Abstractions;

public interface ILcmsDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Bill> Bills { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
