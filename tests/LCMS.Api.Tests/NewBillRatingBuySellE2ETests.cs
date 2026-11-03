using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

/// <summary>
/// LCMS-UAT-RATING-NEW-BILL-ERROR-01 — real new Bill BUY/SELL calculate (no pre-seeded Rating/Cost/Revenue).
/// </summary>
[Collection("Api")]
public sealed class NewBillRatingBuySellE2ETests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public NewBillRatingBuySellE2ETests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task NewBill_CalculateBuyAndSell_WithoutSeeding_ThenApply_NoDoubleCount()
    {
        var tenantId = await CreateTenantAsync();
        var billNo = "LSSTEST03112026001-" + Guid.NewGuid().ToString("N")[..6];
        var billId = await CreateBillAsync(tenantId, billNo);

        var (_, buyVersion) = await PublishedCardAsync(tenantId, "BUY-NB", "vendor", 12000m);
        var (_, sellVersion) = await PublishedCardAsync(tenantId, "SELL-NB", "customer", 18000m);

        Assert.Equal(0, await CountCostsAsync(tenantId, billId));
        Assert.Equal(0, await CountRevenuesAsync(tenantId, billId));

        var buy1 = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = buyVersion,
            quantity = 120m,
            seedExpectedCosts = false,
            seedExpectedRevenues = false
        });
        Assert.NotEqual(Guid.Empty, buy1);
        Assert.Equal(0, await CountCostsAsync(tenantId, billId));
        Assert.Equal(0, await CountRevenuesAsync(tenantId, billId));

        var sell1 = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = sellVersion,
            quantity = 120m,
            seedExpectedCosts = false,
            seedExpectedRevenues = false
        });
        Assert.NotEqual(Guid.Empty, sell1);
        Assert.Equal(0, await CountCostsAsync(tenantId, billId));
        Assert.Equal(0, await CountRevenuesAsync(tenantId, billId));

        var buy2 = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = buyVersion,
            quantity = 120m,
            seedExpectedCosts = false,
            seedExpectedRevenues = false
        });
        Assert.NotEqual(buy1, buy2);

        await ApplyAsync(tenantId, buy2);
        await ApplyAsync(tenantId, buy2); // idempotent / second apply
        var costsAfterBuy = await CountActiveExpectedCostsAsync(tenantId, billId);
        Assert.True(costsAfterBuy >= 1);

        await ApplyAsync(tenantId, sell1);
        await ApplyAsync(tenantId, sell1);
        var revsAfterSell = await CountActiveExpectedRevenuesAsync(tenantId, billId);
        Assert.True(revsAfterSell >= 1);

        var costsBeforeThirdApply = await CountActiveExpectedCostsAsync(tenantId, billId);
        await ApplyAsync(tenantId, buy2);
        Assert.Equal(costsBeforeThirdApply, await CountActiveExpectedCostsAsync(tenantId, billId));
    }

    [Fact]
    public async Task ServiceSelections_Query_IncludesDeletedBy_WithoutThrowing()
    {
        _ = await CreateTenantAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        // Soft-delete filter SELECT must include deleted_by (Postgres 42703 regression).
        _ = await db.ServiceSelections.AsNoTracking().CountAsync();
    }

    private async Task ApplyAsync(Guid tenantId, Guid ratingId)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/ratings/{ratingId}/apply-financials", tenantId);
        req.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        req.Content = JsonContent.Create(new { idempotencyKey = Guid.NewGuid().ToString("N") });
        var res = await _client.SendAsync(req);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<int> CountCostsAsync(Guid tenantId, Guid billId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        return await db.Costs.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId && c.BillId == billId);
    }

    private async Task<int> CountRevenuesAsync(Guid tenantId, Guid billId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        return await db.Revenues.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId && c.BillId == billId);
    }

    private async Task<int> CountActiveExpectedCostsAsync(Guid tenantId, Guid billId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        return await db.Costs.IgnoreQueryFilters().CountAsync(c =>
            c.TenantId == tenantId && c.BillId == billId && c.DeletedAt == null
            && c.RecordStatus == "active" && c.FinancialMaturity == "expected");
    }

    private async Task<int> CountActiveExpectedRevenuesAsync(Guid tenantId, Guid billId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        return await db.Revenues.IgnoreQueryFilters().CountAsync(c =>
            c.TenantId == tenantId && c.BillId == billId && c.DeletedAt == null
            && c.RecordStatus == "active" && c.FinancialMaturity == "expected");
    }

    private async Task<(Guid CardId, Guid VersionId)> PublishedCardAsync(
        Guid tenantId, string code, string partyType, decimal unitAmount)
    {
        var cardId = await PostIdAsync(tenantId, "/api/rate-cards",
            new { code, name = code, partyType, currencyCode = "VND" });
        var versionId = await PostIdAsync(tenantId, $"/api/rate-cards/{cardId}/versions",
            new { note = "v", effectiveFrom = "2020-01-01T00:00:00Z" });
        using var ruleReq = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId);
        ruleReq.Content = JsonContent.Create(new
        {
            code = "FREIGHT",
            name = "Cước chính",
            calcMethod = "unit_rate",
            unitAmount,
            currencyCode = "VND",
            sortOrder = 1
        });
        (await _client.SendAsync(ruleReq)).EnsureSuccessStatusCode();
        using var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        (await _client.SendAsync(publish)).EnsureSuccessStatusCode();
        return (cardId, versionId);
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        var res = await PostAsync(tenantId, "/api/ratings", body);
        Assert.True(
            res.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            await res.Content.ReadAsStringAsync());
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        return await PostIdAsync(tenantId, "/api/bills",
            new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants",
            new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "NewBillRating" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> PostIdAsync(Guid tenantId, string url, object body)
    {
        var res = await PostAsync(tenantId, url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<HttpResponseMessage> PostAsync(Guid tenantId, string url, object body)
    {
        using var req = Tenant(HttpMethod.Post, url, tenantId);
        req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
}
