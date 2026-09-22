using LCMS.Application.Abstractions;
using LCMS.Application.Currencies;
using LCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LCMS.Api.Tests;

public class LcmsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"lcms-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("Demo:SeedOnStartup", "false");
        builder.UseSetting("Auth:RequireJwt", "false");
        builder.UseSetting("Auth:AllowHeaderBootstrap", "true");
        builder.UseSetting("Auth:Jwt:SigningKey", LCMS.Api.Auth.AuthServiceCollectionExtensions.DevFallbackSigningKey);
        builder.UseSetting("OutboxWorker:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<LcmsDbContext>>();
            services.RemoveAll<LcmsDbContext>();
            services.RemoveAll<ILcmsDbContext>();

            services.AddDbContext<LcmsDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_dbPath}");
                options.UseSnakeCaseNamingConvention();
            });
            services.AddScoped<ILcmsDbContext>(sp => sp.GetRequiredService<LcmsDbContext>());
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await CurrencyCatalogSeeder.EnsureBaselineAsync(db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
