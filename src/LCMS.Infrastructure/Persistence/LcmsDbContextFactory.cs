using LCMS.Application.Abstractions;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LCMS.Infrastructure.Persistence;

/// <summary>Design-time factory for <c>dotnet ef migrations</c>.</summary>
public sealed class LcmsDbContextFactory : IDesignTimeDbContextFactory<LcmsDbContext>
{
    public LcmsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LcmsDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=lcms;Username=lcms;Password=lcms")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new LcmsDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
