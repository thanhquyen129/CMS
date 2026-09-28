using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

/// <summary>
/// FIN-DATA-01 (UAT follow-up 2026-09-28): inventory of AR/AP whose stored balance drifted from the ledger,
/// and controlled, audited correction only for pre-fix cross-currency allocation reversals.
/// </summary>
[Collection("Api")]
public sealed class ApArBalanceReconciliationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public ApArBalanceReconciliationTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task LegacyCrossCurrencyReversal_IsInventoried_AndCorrectedWithAudit()
    {
        var tenantId = await CreateTenantAsync("TN-FINDATA-1", "FIN-DATA-01");
        var billId = await PostIdAsync(tenantId, "/api/bills", new { billNo = "HAWB-FINDATA-1", billType = "freight" });
        var arId = await RecognizeArAsync(tenantId, billId, 280m, "USD");

        var colA = await PostIdAsync(tenantId, "/api/collections", new { amount = 100m, currencyCode = "USD", billId });
        var allocA = await PostIdAsync(tenantId, $"/api/collections/{colA}/allocations",
            new { accountsReceivableId = arId, amount = 100m });
        await PostExpectAsync(tenantId, $"/api/collection-allocations/{allocA}/finalize", null, HttpStatusCode.NoContent);

        var colB = await PostIdAsync(tenantId, "/api/collections", new { amount = 50m, currencyCode = "USD", billId });
        var allocB = await PostIdAsync(tenantId, $"/api/collections/{colB}/allocations",
            new { accountsReceivableId = arId, amount = 50m });
        await PostExpectAsync(tenantId, $"/api/collection-allocations/{allocB}/finalize", null, HttpStatusCode.NoContent);
        await PostExpectAsync(tenantId, $"/api/collection-allocations/{allocB}/reverse",
            new { reason = "Phân bổ nhầm" }, HttpStatusCode.NoContent);

        // Pre-fix state: VND cash settled 50 USD, old reverse released the cash amount (clamped to 0).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var collection = await db.Collections.IgnoreQueryFilters().FirstAsync(c => c.Id == colB);
            collection.CurrencyCode = "VND";
            collection.Amount = 1_250_000m;
            var allocation = await db.CollectionAllocations.IgnoreQueryFilters().FirstAsync(a => a.Id == allocB);
            allocation.CurrencyCode = "VND";
            allocation.Amount = 1_250_000m;
            allocation.OriginalAmount = 1_250_000m;
            allocation.SettledAmount = 50m;
            var ar = await db.AccountsReceivable.IgnoreQueryFilters().FirstAsync(a => a.Id == arId);
            ar.FinalizedSettledAmount = 0m;
            await db.SaveChangesAsync();
        }

        var ledgerBefore = await GetAsync<JsonElement>(tenantId, $"/api/accounts-receivable/{arId}/ledger");
        Assert.False(ledgerBefore.GetProperty("reconciled").GetBoolean());
        Assert.Equal(180m, ledgerBefore.GetProperty("ledgerBalance").GetDecimal());

        var report = await GetAsync<ReportDto>(tenantId, "/api/ap-ar/balance-reconciliation");
        var item = Assert.Single(report.Items);
        Assert.Equal("ar", item.Kind);
        Assert.Equal(arId, item.AccountId);
        Assert.Equal("HAWB-FINDATA-1", item.BillNo);
        Assert.Equal(280m, item.CurrentOutstanding);
        Assert.Equal(180m, item.LedgerBalance);
        Assert.Equal(-100m, item.Difference);
        Assert.Equal(0m, item.StoredSettledAmount);
        Assert.Equal(100m, item.DerivedSettledAmount);
        Assert.Equal("legacy_cross_currency_reversal", item.Cause);
        Assert.True(item.Correctable);
        var legacy = Assert.Single(item.LegacyAllocations);
        Assert.Equal(allocB, legacy.AllocationId);
        Assert.Equal("VND", legacy.CashCurrencyCode);
        Assert.Equal(1_250_000m, legacy.CashAmount);
        Assert.Equal(50m, legacy.SettledAmount);

        await PostExpectAsync(tenantId, $"/api/accounts-receivable/{arId}/settlement-correction",
            new { reason = "" }, HttpStatusCode.BadRequest);

        var correction = await PostJsonAsync<CorrectionDto>(tenantId,
            $"/api/accounts-receivable/{arId}/settlement-correction",
            new { reason = "Đối soát FIN-DATA-01: hủy phân bổ VND trước bản sửa" });
        Assert.Equal(280m, correction.OutstandingBefore);
        Assert.Equal(180m, correction.OutstandingAfter);
        Assert.Equal(0m, correction.SettledBefore);
        Assert.Equal(100m, correction.SettledAfter);

        var ledgerAfter = await GetAsync<JsonElement>(tenantId, $"/api/accounts-receivable/{arId}/ledger");
        Assert.True(ledgerAfter.GetProperty("reconciled").GetBoolean());
        var arAfter = await GetAsync<JsonElement>(tenantId, $"/api/accounts-receivable/{arId}");
        Assert.Equal(180m, arAfter.GetProperty("outstanding").GetDecimal());

        var reportAfter = await GetAsync<ReportDto>(tenantId, "/api/ap-ar/balance-reconciliation");
        Assert.Empty(reportAfter.Items);
        var logged = Assert.Single(reportAfter.RecentCorrections);
        Assert.Equal(arId, logged.AccountId);
        Assert.Equal(280m, logged.OutstandingBefore);
        Assert.Equal(180m, logged.OutstandingAfter);

        await PostExpectAsync(tenantId, $"/api/accounts-receivable/{arId}/settlement-correction",
            new { reason = "Lặp" }, HttpStatusCode.Conflict);

        var audit = await GetAsync<List<JsonElement>>(tenantId,
            $"/api/audit-events?objectType=accounts_receivable&objectId={arId}&take=50");
        Assert.Contains(audit, e => e.GetProperty("action").GetString() == "accounts_receivable.settlement_correction"
                                    && e.GetProperty("reason").GetString()!.StartsWith("Đối soát FIN-DATA-01"));

        var allocation1 = await GetAsync<JsonElement>(tenantId, $"/api/collections/{colB}");
        Assert.Equal(1_250_000m, allocation1.GetProperty("allocations")[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task UnexplainedMismatch_IsReported_ButNotAutoCorrected()
    {
        var tenantId = await CreateTenantAsync("TN-FINDATA-2", "FIN-DATA-01 unexplained");
        var billId = await PostIdAsync(tenantId, "/api/bills", new { billNo = "HAWB-FINDATA-2", billType = "freight" });
        var arId = await RecognizeArAsync(tenantId, billId, 100m, "VND");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var ar = await db.AccountsReceivable.IgnoreQueryFilters().FirstAsync(a => a.Id == arId);
            ar.FinalizedSettledAmount = 10m;
            await db.SaveChangesAsync();
        }

        var report = await GetAsync<ReportDto>(tenantId, "/api/ap-ar/balance-reconciliation");
        var item = Assert.Single(report.Items);
        Assert.Equal("unexplained", item.Cause);
        Assert.False(item.Correctable);
        Assert.Empty(item.LegacyAllocations);

        await PostExpectAsync(tenantId, $"/api/accounts-receivable/{arId}/settlement-correction",
            new { reason = "Thử sửa" }, HttpStatusCode.Conflict);

        var otherTenant = await CreateTenantAsync("TN-FINDATA-3", "FIN-DATA-01 other");
        var otherReport = await GetAsync<ReportDto>(otherTenant, "/api/ap-ar/balance-reconciliation");
        Assert.Empty(otherReport.Items);
        await PostExpectAsync(otherTenant, $"/api/accounts-receivable/{arId}/settlement-correction",
            new { reason = "Chéo thuê bao" }, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Reconcile_RequiresPermission_AndKeepsCostRevenueSeparation()
    {
        var tenantId = await CreateTenantAsync("TN-FINDATA-4", "FIN-DATA-01 authz");
        var billId = await PostIdAsync(tenantId, "/api/bills", new { billNo = "HAWB-FINDATA-4", billType = "freight" });
        var arId = await RecognizeArAsync(tenantId, billId, 100m, "VND");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var ar = await db.AccountsReceivable.IgnoreQueryFilters().FirstAsync(a => a.Id == arId);
            ar.FinalizedSettledAmount = 10m;
            await db.SaveChangesAsync();
        }

        var readerRole = await CreateRoleAsync(tenantId, "ArReader", ("revenue.read", "all"));
        var reader = await CreateUserWithRoleAsync(tenantId, "ar-reader@example.com", readerRole);
        using (var req = WithActor(HttpMethod.Get, "/api/ap-ar/balance-reconciliation", tenantId, reader))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(req)).StatusCode);
        }

        using (var req = WithActor(HttpMethod.Post, $"/api/accounts-receivable/{arId}/settlement-correction", tenantId, reader))
        {
            req.Content = JsonContent.Create(new { reason = "Không có quyền" });
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(req)).StatusCode);
        }

        var costRole = await CreateRoleAsync(tenantId, "ApReconciler", ("apar.reconcile", "all"), ("cost.read", "all"));
        var costUser = await CreateUserWithRoleAsync(tenantId, "ap-rec@example.com", costRole);
        using (var req = WithActor(HttpMethod.Get, "/api/ap-ar/balance-reconciliation", tenantId, costUser))
        {
            var res = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var body = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
            Assert.False(body.GetProperty("includesReceivables").GetBoolean());
            Assert.Equal(0, body.GetProperty("items").GetArrayLength());
        }
    }

    private async Task<Guid> CreateRoleAsync(Guid tenantId, string code, params (string Action, string Scope)[] grants)
    {
        var roleId = await PostIdAsync(tenantId, "/api/roles", new { code, name = code });
        foreach (var (action, scope) in grants)
        {
            await PostExpectAsync(tenantId, $"/api/roles/{roleId}/permissions",
                new { actionCode = action, dataScope = scope }, HttpStatusCode.NoContent, HttpStatusCode.Created, HttpStatusCode.OK);
        }

        return roleId;
    }

    private async Task<Guid> CreateUserWithRoleAsync(Guid tenantId, string email, Guid roleId)
    {
        var userId = await PostIdAsync(tenantId, "/api/users", new { email, displayName = email });
        await PostExpectAsync(tenantId, $"/api/users/{userId}/roles/{roleId}", null,
            HttpStatusCode.NoContent, HttpStatusCode.Created, HttpStatusCode.OK);
        return userId;
    }

    private static HttpRequestMessage WithActor(HttpMethod method, string url, Guid tenantId, Guid userId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        return req;
    }

    private async Task<Guid> RecognizeArAsync(Guid tenantId, Guid billId, decimal amount, string currency)
    {
        var exposureId = await PostIdAsync(tenantId, "/api/receivable-exposures",
            new { amount, currencyCode = currency, billId });
        return await PostIdAsync(tenantId, $"/api/receivable-exposures/{exposureId}/recognize", new { amount });
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> PostIdAsync(Guid tenantId, string path, object body) =>
        (await PostJsonAsync<IdResponse>(tenantId, path, body)).Id;

    private async Task<T> PostJsonAsync<T>(Guid tenantId, string path, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task PostExpectAsync(Guid tenantId, string path, object? body, params HttpStatusCode[] expected)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        Assert.True(expected.Contains(response.StatusCode),
            $"{path}: expected {string.Join("/", expected)}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task<T> GetAsync<T>(Guid tenantId, string path)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record LegacyDto(Guid AllocationId, decimal CashAmount, string CashCurrencyCode, decimal SettledAmount);

    private sealed record ItemDto(
        string Kind,
        Guid AccountId,
        string? BillNo,
        decimal CurrentOutstanding,
        decimal LedgerBalance,
        decimal Difference,
        decimal StoredSettledAmount,
        decimal DerivedSettledAmount,
        string Cause,
        bool Correctable,
        List<LegacyDto> LegacyAllocations);

    private sealed record LoggedDto(Guid AccountId, decimal? OutstandingBefore, decimal? OutstandingAfter);

    private sealed record ReportDto(List<ItemDto> Items, List<LoggedDto> RecentCorrections);

    private sealed record CorrectionDto(
        decimal OutstandingBefore,
        decimal OutstandingAfter,
        decimal SettledBefore,
        decimal SettledAfter);
}
