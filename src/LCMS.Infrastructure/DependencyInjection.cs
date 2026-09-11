using LCMS.Application.Abstractions;
using LCMS.Infrastructure.Persistence;
using LCMS.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<HttpTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<HttpTenantContext>());

        var connectionString = configuration.GetConnectionString("LcmsDb")
            ?? "Host=localhost;Port=5432;Database=lcms;Username=lcms;Password=lcms";

        services.AddDbContext<LcmsDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "public");
            });
            options.UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
