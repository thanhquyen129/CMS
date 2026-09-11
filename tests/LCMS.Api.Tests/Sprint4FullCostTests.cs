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
public sealed class Sprint4FullCostTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint4FullCostTests(LcmsApiFactory factory)
    {
        _factory = factory;
    }

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
    public async Task AllocationBases_Equal_quantity_manualRatio_Conserve_And_ReallocationSupersedes()
    {
        var tenantId = await CreateTenantAsync("TN-S4F-ALLOC", "S4 Full Alloc");
        var billA = await CreateBillAsync(tenantId, "BL-S4F-A", "freight");
        var billB = await CreateBillAsync(tenantId, "BL-S4F-B", "freight");
        var sharedId = await CreateSharedCostAsync(tenantId, 100m, "VND");

        // equal → 50/50 (client basis ignored / forced to 1)
        var equalId = await CreateAllocationAsync(tenantId, sharedId, "equal",
        [
            new { billId = billA, basisValue = (decimal?)null },
            new { billId = billB, basisValue = (decimal?)99m }
        ]);
        await FinalizeAsync(tenantId, equalId);
        var afterEqual = await GetCostAsync(tenantId, sharedId);
        var eq = Assert.Single(afterEqual.Allocations, a => a.AllocationStatus == "finalized");
        Assert.Equal("equal", eq.AllocationBasis);
        Assert.Equal(100m, eq.Details.Sum(d => d.AllocatedAmount));
        Assert.All(eq.Details, d => Assert.Equal(1m, d.BasisValue));
        Assert.Equal(50m, eq.Details.OrderBy(d => d.BillId).ElementAt(0).AllocatedAmount);
        Assert.Equal(50m, eq.Details.OrderBy(d => d.BillId).ElementAt(1).AllocatedAmount);

        // quantity 1:3 → 25/75; supersedes equal
        var qtyId = await CreateAllocationAsync(tenantId, sharedId, "quantity",
        [
            new { billId = billA, basisValue = (decimal?)1m },
            new { billId = billB, basisValue = (decimal?)3m }
        ]);
        await FinalizeAsync(tenantId, qtyId);
        var afterQty = await GetCostAsync(tenantId, sharedId);
        Assert.Equal(2, afterQty.Allocations.Count);
        var superseded = Assert.Single(afterQty.Allocations, a => a.AllocationStatus == "superseded");
        Assert.Equal(equalId, superseded.Id);
        var qty = Assert.Single(afterQty.Allocations, a => a.AllocationStatus == "finalized");
        Assert.Equal(qtyId, qty.Id);
        Assert.Equal(equalId, qty.SupersedesAllocationId);
        Assert.Equal("quantity", qty.AllocationBasis);
        Assert.Equal(100m, qty.Details.Sum(d => d.AllocatedAmount));
        Assert.Equal(25m, qty.Details.Single(d => d.BillId == billA).AllocatedAmount);
        Assert.Equal(75m, qty.Details.Single(d => d.BillId == billB).AllocatedAmount);

        // Single Economic Cost
        Assert.Single(await ListCostsAsync(tenantId));

        // manual_ratio 2:2 → 50/50; supersedes quantity
        var ratioId = await CreateAllocationAsync(tenantId, sharedId, "manual_ratio",
        [
            new { billId = billA, basisValue = (decimal?)2m },
            new { billId = billB, basisValue = (decimal?)2m }
        ]);
        await FinalizeAsync(tenantId, ratioId);
        var afterRatio = await GetCostAsync(tenantId, sharedId);
        Assert.Equal(3, afterRatio.Allocations.Count);
        Assert.Equal(2, afterRatio.Allocations.Count(a => a.AllocationStatus == "superseded"));
        var ratio = Assert.Single(afterRatio.Allocations, a => a.AllocationStatus == "finalized");
        Assert.Equal(qtyId, ratio.SupersedesAllocationId);
        Assert.Equal(100m, ratio.Details.Sum(d => d.AllocatedAmount));

        // C-006: unknown basis rejected
        using (var bad = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{sharedId}/allocations")
        {
            Content = JsonContent.Create(new
            {
                allocationBasis = "weight",
                details = new[] { new { billId = billA, basisValue = 1m } }
            })
        })
        {
            bad.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(bad)).StatusCode);
        }

        // Direct cannot allocate
        var directId = await CreateDirectCostAsync(tenantId, billA, 10m, "VND");
        using (var directAlloc = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{directId}/allocations")
        {
            Content = JsonContent.Create(new
            {
                allocationBasis = "equal",
                details = new[] { new { billId = billA, basisValue = 1m } }
            })
        })
        {
            directAlloc.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(directAlloc);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }
    }

    [Fact]
    public async Task SharedVsDirect_Rules_And_FxStub_BaseAmount()
    {
        var tenantId = await CreateTenantAsync("TN-S4F-FX", "S4 Full FX");
        var billId = await CreateBillAsync(tenantId, "BL-S4F-FX", "freight");

        using (var noBill = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                attributionType = "direct",
                amount = 100m,
                currencyCode = "VND"
            })
        })
        {
            noBill.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(noBill)).StatusCode);
        }

        using (var sharedBill = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "shared",
                amount = 100m,
                currencyCode = "VND"
            })
        })
        {
            sharedBill.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(sharedBill)).StatusCode);
        }

        var vndId = await CreateDirectCostAsync(tenantId, billId, 1_000m, "VND");
        var vnd = await GetCostAsync(tenantId, vndId);
        Assert.Equal(1_000m, vnd.BaseAmount);
        Assert.Null(vnd.FxRateId);

        var usdId = await CreateDirectCostAsync(tenantId, billId, 2m, "USD");
        var usd = await GetCostAsync(tenantId, usdId);
        Assert.Equal(50_000m, usd.BaseAmount);
        Assert.Null(usd.FxRateId);

        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{usdId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 3m })
        };
        confirm.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(confirm)).StatusCode);
        var afterConfirm = await GetCostAsync(tenantId, usdId);
        Assert.Equal(2m, afterConfirm.ExpectedAmount);
        Assert.Equal(3m, afterConfirm.ConfirmedAmount);
        Assert.Equal(75_000m, afterConfirm.BaseAmount);
    }

    [Fact]
    public async Task Confirm_ApprovalThreshold_Blocks_UntilApproved()
    {
        using var factory = new ThresholdApiFactory(thresholdBase: 10_000m);
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var tenantId = await CreateTenantAsync(client, "TN-S4F-THR", "S4 Threshold");
        var billId = await CreateBillAsync(client, tenantId, "BL-S4F-THR", "freight");

        Guid underId;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 100m,
                currencyCode = "VND",
                costTypeCode = "SMALL"
            })
        })
        {
            create.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await client.SendAsync(create);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            underId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        var under = await GetCostAsync(client, tenantId, underId);
        Assert.Equal("not_required", under.ApprovalStatus);
        using (var confirmUnder = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{underId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 100m })
        })
        {
            confirmUnder.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(confirmUnder)).StatusCode);
        }

        Guid overId;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 50_000m,
                currencyCode = "VND",
                costTypeCode = "BIG"
            })
        })
        {
            create.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await client.SendAsync(create);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            overId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        var over = await GetCostAsync(client, tenantId, overId);
        Assert.Equal("pending", over.ApprovalStatus);
        Assert.Equal(50_000m, over.BaseAmount);

        using (var blocked = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{overId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 50_000m })
        })
        {
            blocked.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await client.SendAsync(blocked);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("phê duyệt", err!.Message, StringComparison.OrdinalIgnoreCase);
        }

        Guid approvalId;
        using (var req = new HttpRequestMessage(HttpMethod.Post, "/api/approvals")
        {
            Content = JsonContent.Create(new
            {
                objectType = "cost",
                objectId = overId,
                requestReason = "Phê duyệt chi phí lớn"
            })
        })
        {
            req.Headers.Add("X-Tenant-Id", tenantId.ToString());
            req.Headers.Add("X-User-Id", Guid.NewGuid().ToString());
            var res = await client.SendAsync(req);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            approvalId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        using (var decide = new HttpRequestMessage(HttpMethod.Post, $"/api/approvals/{approvalId}/approve")
        {
            Content = JsonContent.Create(new { decisionReason = "OK" })
        })
        {
            decide.Headers.Add("X-Tenant-Id", tenantId.ToString());
            decide.Headers.Add("X-User-Id", Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(decide)).StatusCode);
        }

        var approved = await GetCostAsync(client, tenantId, overId);
        Assert.Equal("approved", approved.ApprovalStatus);

        using (var confirmOver = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{overId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 50_000m })
        })
        {
            confirmOver.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(confirmOver)).StatusCode);
        }

        var confirmed = await GetCostAsync(client, tenantId, overId);
        Assert.Equal("confirmed", confirmed.FinancialMaturity);
        Assert.Equal(50_000m, confirmed.ExpectedAmount);
        Assert.Equal(50_000m, confirmed.ConfirmedAmount);
    }

    private sealed class ThresholdApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"lcms-s4f-{Guid.NewGuid():N}.db");
        private readonly decimal _threshold;

        public ThresholdApiFactory(decimal thresholdBase) => _threshold = thresholdBase;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("Auth:RequireJwt", "false");
            builder.UseSetting("Auth:AllowHeaderBootstrap", "true");
            builder.UseSetting("Auth:Jwt:SigningKey", LCMS.Api.Auth.AuthServiceCollectionExtensions.DevFallbackSigningKey);
            builder.UseSetting(
                "Cost:ConfirmApprovalThresholdBase",
                _threshold.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Cost:BaseCurrency", "VND");
            builder.UseSetting("Cost:StubFxRatesToBase:USD", "25000");
            builder.UseSetting("Cost:StubFxRatesToBase:EUR", "27000");

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

    private async Task<Guid> CreateTenantAsync(string code, string name) =>
        await CreateTenantAsync(_client, code, name);

    private static async Task<Guid> CreateTenantAsync(HttpClient client, string code, string name)
    {
        var response = await client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo, string billType) =>
        await CreateBillAsync(_client, tenantId, billNo, billType);

    private static async Task<Guid> CreateBillAsync(HttpClient client, Guid tenantId, string billNo, string billType)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo, billType })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateSharedCostAsync(Guid tenantId, decimal amount, string currency)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                attributionType = "shared",
                amount,
                currencyCode = currency,
                costTypeCode = "SHARED"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateDirectCostAsync(Guid tenantId, Guid billId, decimal amount, string currency)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount,
                currencyCode = currency,
                costTypeCode = "DIRECT"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateAllocationAsync(
        Guid tenantId,
        Guid costId,
        string basis,
        object[] details)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/allocations")
        {
            Content = JsonContent.Create(new { allocationBasis = basis, details })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task FinalizeAsync(Guid tenantId, Guid allocationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/finalize");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<CostResponse> GetCostAsync(Guid tenantId, Guid costId) =>
        await GetCostAsync(_client, tenantId, costId);

    private static async Task<CostResponse> GetCostAsync(HttpClient client, Guid tenantId, Guid costId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/costs/{costId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CostResponse>(JsonOptions))!;
    }

    private async Task<List<CostListItem>> ListCostsAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/costs");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<CostListItem>>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record CostListItem(Guid Id, Guid? BillId, string AttributionType);

    private sealed record AllocationDetailResponse(
        Guid Id,
        Guid BillId,
        decimal BasisValue,
        decimal BasisRatio,
        decimal AllocatedAmount);

    private sealed record AllocationResponse(
        Guid Id,
        int VersionNo,
        string AllocationBasis,
        string AllocationStatus,
        decimal AllocatableAmount,
        decimal AllocatedAmount,
        DateTimeOffset? FinalizedAt,
        Guid? SupersedesAllocationId,
        List<AllocationDetailResponse> Details);

    private sealed record CostResponse(
        Guid Id,
        Guid? BillId,
        string AttributionType,
        string FinancialMaturity,
        decimal ExpectedAmount,
        decimal? ConfirmedAmount,
        decimal? ActualAmount,
        decimal Amount,
        string CurrencyCode,
        decimal? BaseAmount,
        Guid? FxRateId,
        string ApprovalStatus,
        List<AllocationResponse> Allocations);

    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
