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
public sealed class Sprint5FullRevenueProfitabilityTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint5FullRevenueProfitabilityTests(LcmsApiFactory factory)
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
    public async Task FxStub_BaseAmount_And_MissingRateRejected()
    {
        var tenantId = await CreateTenantAsync("TN-S5F-FX", "S5 Full FX");
        var billId = await CreateBillAsync(tenantId, "BL-S5F-FX", "freight");

        var vndId = await CreateRevenueAsync(tenantId, billId, 1_000m, "FREIGHT", "VND");
        var vnd = await GetRevenueAsync(tenantId, vndId);
        Assert.Equal(1_000m, vnd.BaseAmount);
        Assert.Null(vnd.FxRateId);

        var usdId = await CreateRevenueAsync(tenantId, billId, 2m, "SURCHARGE", "USD");
        var usd = await GetRevenueAsync(tenantId, usdId);
        Assert.Equal(50_000m, usd.BaseAmount);
        Assert.Null(usd.FxRateId);

        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{usdId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 3m })
        };
        confirm.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(confirm)).StatusCode);
        var afterConfirm = await GetRevenueAsync(tenantId, usdId);
        Assert.Equal(2m, afterConfirm.ExpectedAmount);
        Assert.Equal(3m, afterConfirm.ConfirmedAmount);
        Assert.Equal(75_000m, afterConfirm.BaseAmount);

        // Seed JPY in catalog (no stub rate configured) — FX stub must reject, not invent 1:1.
        using (var upsert = new HttpRequestMessage(HttpMethod.Put, "/api/currencies")
        {
            Content = JsonContent.Create(new
            {
                code = "JPY",
                name = "Yen",
                decimalPlaces = 0,
                isActive = true
            })
        })
        {
            upsert.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(upsert)).StatusCode);
        }

        using (var jpy = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount = 100m,
                currencyCode = "JPY",
                revenueTypeCode = "OTHER"
            })
        })
        {
            jpy.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(jpy);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.Contains("StubFxRatesToBase", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("JPY", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Confirm_ApprovalThreshold_Blocks_UntilApproved_ReuseApprovals()
    {
        using var factory = new ThresholdApiFactory(thresholdBase: 10_000m);
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var tenantId = await CreateTenantAsync(client, "TN-S5F-THR", "S5 Threshold");
        var billId = await CreateBillAsync(client, tenantId, "BL-S5F-THR", "freight");

        Guid underId;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount = 100m,
                currencyCode = "VND",
                revenueTypeCode = "SMALL"
            })
        })
        {
            create.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await client.SendAsync(create);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            underId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        var under = await GetRevenueAsync(client, tenantId, underId);
        Assert.Equal("not_required", under.ApprovalStatus);
        using (var confirmUnder = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{underId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 100m })
        })
        {
            confirmUnder.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(confirmUnder)).StatusCode);
        }

        Guid overId;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount = 50_000m,
                currencyCode = "VND",
                revenueTypeCode = "BIG"
            })
        })
        {
            create.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await client.SendAsync(create);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            overId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        var over = await GetRevenueAsync(client, tenantId, overId);
        Assert.Equal("pending", over.ApprovalStatus);
        Assert.Equal(50_000m, over.BaseAmount);

        using (var blocked = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{overId}/confirm")
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
                objectType = "revenue",
                objectId = overId,
                requestReason = "Phê duyệt doanh thu lớn"
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

        var approved = await GetRevenueAsync(client, tenantId, overId);
        Assert.Equal("approved", approved.ApprovalStatus);

        using (var confirmOver = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{overId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 50_000m })
        })
        {
            confirmOver.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(confirmOver)).StatusCode);
        }

        var confirmed = await GetRevenueAsync(client, tenantId, overId);
        Assert.Equal("confirmed", confirmed.FinancialMaturity);
        Assert.Equal(50_000m, confirmed.ExpectedAmount);
        Assert.Equal(50_000m, confirmed.ConfirmedAmount);
    }

    [Fact]
    public async Task ProfitabilityViews_ProfileVariance_Allocated_MultiCurrency_And_C004Hardened()
    {
        var tenantId = await CreateTenantAsync("TN-S5F-PROF", "S5 Full Prof");
        var billA = await CreateBillAsync(tenantId, "BL-S5F-A", "freight");
        var billB = await CreateBillAsync(tenantId, "BL-S5F-B", "freight");

        var revId = await CreateRevenueAsync(tenantId, billA, 10_000m, "FREIGHT", "VND");
        await ConfirmRevenueAsync(tenantId, revId, 11_000m);
        await ActualizeRevenueAsync(tenantId, revId, 10_500m);
        await CreateRevenueAsync(tenantId, billA, 100m, "SURCHARGE", "USD");

        await CreateDirectCostAsync(tenantId, billA, 3_000m, "VND");
        var sharedId = await CreateSharedCostAsync(tenantId, 100m, "VND");
        var allocId = await CreateAllocationAsync(tenantId, sharedId,
        [
            new { billId = billA, basisValue = (decimal?)60m },
            new { billId = billB, basisValue = (decimal?)40m }
        ]);
        await FinalizeAsync(tenantId, allocId);

        var profile = await GetFinancialProfileAsync(tenantId, billA);
        Assert.Equal("best_available", profile.ViewKind);
        Assert.True(profile.HasMixedCurrencies);
        var vnd = profile.ByCurrency.Single(b => b.CurrencyCode == "VND");
        Assert.Equal(10_500m, vnd.RevenueBestAvailable);
        Assert.Equal(3_060m, vnd.CostBestAvailable); // direct 3000 + allocated 60
        Assert.Equal(60m, vnd.AllocatedCostAmount);
        Assert.Equal(7_440m, vnd.ProfitBestAvailable);
        Assert.Equal(10_000m - 10_500m, vnd.RevenueVarianceExpectedVsActual);
        Assert.Equal(3_000m - 0m, vnd.DirectCostVarianceExpectedVsActual); // cost not actualized

        var usd = profile.ByCurrency.Single(b => b.CurrencyCode == "USD");
        Assert.Equal(100m, usd.RevenueBestAvailable);
        Assert.Equal(0m, usd.CostBestAvailable);

        var expectedView = await GetProfitabilityAsync(tenantId, billA, "expected");
        Assert.Equal("expected", expectedView.View);
        var expVnd = expectedView.ByCurrency.Single(b => b.CurrencyCode == "VND");
        Assert.Equal(10_000m, expVnd.RevenueAmount);
        Assert.Equal(3_000m, expVnd.DirectCostAmount);
        Assert.Equal(60m, expVnd.AllocatedCostAmount);
        Assert.Equal(3_060m, expVnd.CostAmount);
        Assert.Equal(10_000m - 3_060m, expVnd.ProfitAmount);

        var confirmedView = await GetProfitabilityAsync(tenantId, billA, "confirmed");
        var confVnd = confirmedView.ByCurrency.Single(b => b.CurrencyCode == "VND");
        Assert.Equal(11_000m, confVnd.RevenueAmount);
        Assert.Equal(0m, confVnd.DirectCostAmount); // direct still Expected only → 0 for confirmed view

        var actualView = await GetProfitabilityAsync(tenantId, billA, "actual");
        var actVnd = actualView.ByCurrency.Single(b => b.CurrencyCode == "VND");
        Assert.Equal(10_500m, actVnd.RevenueAmount);

        var bestView = await GetProfitabilityAsync(tenantId, billA, "best");
        Assert.Equal("best", bestView.View);
        var bestVnd = bestView.ByCurrency.Single(b => b.CurrencyCode == "VND");
        Assert.Equal(10_500m, bestVnd.RevenueAmount);
        Assert.Equal(3_060m, bestVnd.CostAmount);
        Assert.True(bestView.HasMixedCurrencies);
        Assert.Contains("tiền tệ", bestView.Note, StringComparison.OrdinalIgnoreCase);

        using (var badView = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billA}/profitability?view=fantasy"))
        {
            badView.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(badView)).StatusCode);
        }

        // C-004 hardened: document / AR / aliases rejected with VI message
        foreach (var source in new[] { "document", "DOCUMENT", "accounts_receivable", "AR", "financial_document", "doc" })
        {
            using var bad = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
            {
                Content = JsonContent.Create(new
                {
                    billId = billA,
                    amount = 1m,
                    currencyCode = "VND",
                    sourceType = source,
                    sourceId = Guid.NewGuid()
                })
            };
            bad.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(bad);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var body = await res.Content.ReadAsStringAsync();
            Assert.Contains("C-004", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class ThresholdApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"lcms-s5f-{Guid.NewGuid():N}.db");
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
                "Revenue:ConfirmApprovalThresholdBase",
                _threshold.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Revenue:BaseCurrency", "VND");
            builder.UseSetting("Revenue:StubFxRatesToBase:USD", "25000");
            builder.UseSetting("Revenue:StubFxRatesToBase:EUR", "27000");

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

    private async Task<Guid> CreateRevenueAsync(
        Guid tenantId,
        Guid billId,
        decimal amount,
        string revenueTypeCode,
        string currency)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount,
                currencyCode = currency,
                revenueTypeCode
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task ConfirmRevenueAsync(Guid tenantId, Guid revenueId, decimal confirmedAmount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task ActualizeRevenueAsync(Guid tenantId, Guid revenueId, decimal actualAmount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueId}/actualize")
        {
            Content = JsonContent.Create(new { actualAmount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
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

    private async Task<Guid> CreateAllocationAsync(Guid tenantId, Guid costId, object[] details)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/allocations")
        {
            Content = JsonContent.Create(new { allocationBasis = "manual_ratio", details })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task FinalizeAsync(Guid tenantId, Guid allocationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/finalize");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<RevenueResponse> GetRevenueAsync(Guid tenantId, Guid id) =>
        await GetRevenueAsync(_client, tenantId, id);

    private static async Task<RevenueResponse> GetRevenueAsync(HttpClient client, Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/revenues/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<RevenueResponse>(JsonOptions))!;
    }

    private async Task<FinancialProfileResponse> GetFinancialProfileAsync(Guid tenantId, Guid billId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billId}/financial-profile");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<FinancialProfileResponse>(JsonOptions))!;
    }

    private async Task<ProfitabilityResponse> GetProfitabilityAsync(Guid tenantId, Guid billId, string view)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billId}/profitability?view={view}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ProfitabilityResponse>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record ErrorResponse(string Code, string Message);

    private sealed record RevenueResponse(
        Guid Id,
        string FinancialMaturity,
        decimal ExpectedAmount,
        decimal? ConfirmedAmount,
        decimal? ActualAmount,
        decimal Amount,
        string CurrencyCode,
        decimal? BaseAmount,
        Guid? FxRateId,
        string ApprovalStatus);

    private sealed record MaturityBreakdown(decimal ExpectedTotal, decimal ConfirmedTotal, decimal ActualTotal);

    private sealed record CurrencyBucket(
        string CurrencyCode,
        decimal RevenueBestAvailable,
        decimal CostBestAvailable,
        decimal ProfitBestAvailable,
        decimal DirectCostBestAvailable,
        decimal AllocatedCostAmount,
        MaturityBreakdown RevenueMaturity,
        MaturityBreakdown DirectCostMaturity,
        decimal RevenueVarianceExpectedVsActual,
        decimal DirectCostVarianceExpectedVsActual,
        decimal ProfitVarianceExpectedVsActual);

    private sealed record FinancialProfileResponse(
        Guid BillId,
        string ViewKind,
        bool HasMixedCurrencies,
        string Note,
        IReadOnlyList<CurrencyBucket> ByCurrency);

    private sealed record ProfitBucket(
        string CurrencyCode,
        decimal RevenueAmount,
        decimal DirectCostAmount,
        decimal AllocatedCostAmount,
        decimal CostAmount,
        decimal ProfitAmount,
        decimal RevenueVarianceExpectedVsActual,
        decimal CostVarianceExpectedVsActual,
        decimal ProfitVarianceExpectedVsActual);

    private sealed record ProfitabilityResponse(
        Guid BillId,
        string View,
        bool HasMixedCurrencies,
        string Note,
        IReadOnlyList<ProfitBucket> ByCurrency);
}
