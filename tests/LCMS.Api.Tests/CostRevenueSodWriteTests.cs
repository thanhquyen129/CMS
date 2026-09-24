using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Currencies;
using LCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class CostRevenueSodWriteTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public CostRevenueSodWriteTests(LcmsApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.InitializeDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CostAccountant_CannotCreateRevenue_RevenueAccountant_CannotCreateCost()
    {
        var tenantId = await CreateTenantAsync("TN-SOD-WR", "SoD Write");
        var billId = await CreateBillAsync(tenantId, "BL-SOD-1");
        var costUser = await UserInRoleAsync(tenantId, "cost.sod@example.com", "CostAccountant");
        var revenueUser = await UserInRoleAsync(tenantId, "rev.sod@example.com", "RevenueAccountant");

        using (var deniedRevenue = Post("/api/revenues", tenantId, costUser, new
        {
            billId,
            amount = 1000m,
            currencyCode = "VND",
            revenueTypeCode = "FREIGHT"
        }))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(deniedRevenue)).StatusCode);
        }

        using (var deniedCost = Post("/api/costs", tenantId, revenueUser, new
        {
            billId,
            attributionType = "direct",
            amount = 500m,
            currencyCode = "VND",
            costTypeCode = "FREIGHT"
        }))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(deniedCost)).StatusCode);
        }

        using (var allowedCost = Post("/api/costs", tenantId, costUser, new
        {
            billId,
            attributionType = "direct",
            amount = 500m,
            currencyCode = "VND",
            costTypeCode = "FREIGHT"
        }))
        {
            Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(allowedCost)).StatusCode);
        }

        using (var allowedRevenue = Post("/api/revenues", tenantId, revenueUser, new
        {
            billId,
            amount = 1000m,
            currencyCode = "VND",
            revenueTypeCode = "FREIGHT"
        }))
        {
            Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(allowedRevenue)).StatusCode);
        }

        using (var hidden = new HttpRequestMessage(HttpMethod.Get, "/api/revenues"))
        {
            hidden.Headers.Add("X-Tenant-Id", tenantId.ToString());
            hidden.Headers.Add("X-User-Id", costUser.ToString());
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(hidden)).StatusCode);
        }
    }

    [Fact]
    public async Task ProductionFxFlag_RejectsCrossCurrencyWithoutDatedRate()
    {
        await using var factory = new NoStubFxFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var tenant = await client.PostAsJsonAsync("/api/tenants", new { code = "TN-FX-NOSTUB", name = "FX" });
        tenant.EnsureSuccessStatusCode();
        var tenantId = (await tenant.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var billReq = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "BL-FX-1", billType = "freight" })
        };
        billReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var billRes = await client.SendAsync(billReq);
        billRes.EnsureSuccessStatusCode();
        var billId = (await billRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var costReq = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 10m,
                currencyCode = "USD",
                costTypeCode = "FREIGHT"
            })
        };
        costReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var costRes = await client.SendAsync(costReq);
        Assert.Equal(HttpStatusCode.BadRequest, costRes.StatusCode);
        var err = await costRes.Content.ReadFromJsonAsync<ValidationBody>(JsonOptions);
        var detail = Assert.Single(err!.Errors["CurrencyCode"]);
        Assert.Contains("sổ tỷ giá", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StubFxRatesToBase", detail, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo, billType = "freight" })
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> UserInRoleAsync(Guid tenantId, string email, string roleCode)
    {
        using var createUser = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email, displayName = roleCode })
        };
        createUser.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var userRes = await _client.SendAsync(createUser);
        userRes.EnsureSuccessStatusCode();
        var userId = (await userRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var listRoles = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        listRoles.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var roles = await (await _client.SendAsync(listRoles)).Content
            .ReadFromJsonAsync<List<RoleDto>>(JsonOptions);
        var role = Assert.Single(roles!, r => r.Code == roleCode);

        using var assign = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/roles/{role.Id}");
        assign.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(assign)).StatusCode);
        return userId;
    }

    private static HttpRequestMessage Post(string url, Guid tenantId, Guid userId, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record RoleDto(Guid Id, string Code);
    private sealed record ValidationBody(Dictionary<string, string[]> Errors);

    private sealed class NoStubFxFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"lcms-fx-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("Demo:SeedOnStartup", "false");
            builder.UseSetting("Auth:RequireJwt", "false");
            builder.UseSetting("Auth:AllowHeaderBootstrap", "true");
            builder.UseSetting("Auth:Jwt:SigningKey", LCMS.Api.Auth.AuthServiceCollectionExtensions.DevFallbackSigningKey);
            builder.UseSetting("OutboxWorker:Enabled", "false");
            builder.UseSetting("Cost:AllowStubFxFallback", "false");
            builder.UseSetting("Revenue:AllowStubFxFallback", "false");
            builder.UseSetting("Settlement:AllowStubFxFallback", "false");

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
}
