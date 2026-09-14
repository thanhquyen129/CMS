using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Api.Auth;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LCMS.Application.Abstractions;
using LCMS.Application.Currencies;

namespace LCMS.Api.Tests;

public sealed class RoleDemoUserSeederTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task EnsureAsync_CreatesAccounts_CostCanLogin_CannotManageRoles()
    {
        await using var factory = new RoleDemoApiFactory();
        await factory.InitializeDatabaseAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            db.Tenants.Add(new Tenant { Code = "ops", Name = "Vận hành", IsActive = true });
            await db.SaveChangesAsync();
        }

        await RoleDemoUserSeeder.EnsureAsync(factory.Services);

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "cost@cms.local",
            password = "abc123"
        });
        var raw = await login.Content.ReadAsStringAsync();
        Assert.True(login.StatusCode == HttpStatusCode.OK, raw);

        var body = JsonSerializer.Deserialize<LoginOk>(raw, JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(body?.AccessToken));

        using var rolesReq = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        rolesReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(rolesReq)).StatusCode);

        var adminLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@cms.local",
            password = "abc123"
        });
        Assert.Equal(HttpStatusCode.OK, adminLogin.StatusCode);
    }

    private sealed class RoleDemoApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"lcms-role-demo-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("Demo:SeedOnStartup", "false");
            builder.UseSetting("Auth:RequireJwt", "true");
            builder.UseSetting("Auth:AllowHeaderBootstrap", "false");
            builder.UseSetting("Auth:Jwt:SigningKey", AuthServiceCollectionExtensions.DevFallbackSigningKey);
            builder.UseSetting("Auth:RoleDemoUsers:Enabled", "true");
            builder.UseSetting("Auth:RoleDemoUsers:Password", "abc123");
            builder.UseSetting("Auth:RoleDemoUsers:TenantCode", "ops");

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
                    // best-effort
                }
            }
        }
    }

    private sealed record LoginOk(string AccessToken, string TokenType, int ExpiresIn);
}
