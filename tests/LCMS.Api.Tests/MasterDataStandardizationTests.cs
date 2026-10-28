using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

/// <summary>
/// MASTER DATA STANDARDIZATION: canonical AIR matches historical air, a real mode or weight change still stales,
/// and the rating snapshot plus published rate-card text stay untouched.
/// </summary>
[Collection("Api")]
public sealed class MasterDataStandardizationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public MasterDataStandardizationTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task HistoricalAir_SavedAsAir_DoesNotStale_SeaAndWeightStillDo_SnapshotStays()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-CANON-AIR");
        await EditAsync(tenantId, "bill", billId, new { transport_mode = "air", gross_weight_kg = "85", volume_cbm = "0.9" });
        var versionId = await PublishedUnitRateAsync(tenantId, "RC-CANON-AIR");
        await RateAsync(tenantId, new { billId, rateVersionId = versionId, rateDate = DateTimeOffset.UtcNow });

        var before = await LoadRatingAsync(billId);
        Assert.Null(before.StaleAt);
        var snapshot = before.ContextJson;
        var amount = before.TotalAmount;

        await SetBillModeAsync(billId, "air");

        var sameCase = await EditAsync(tenantId, "bill", billId, new { transport_mode = "  AIR  " });
        Assert.Equal(0, sameCase.RatingsMarkedStale);
        var afterCase = await LoadRatingAsync(billId);
        Assert.Null(afterCase.StaleAt);
        Assert.Equal(snapshot, afterCase.ContextJson);
        Assert.Equal(amount, afterCase.TotalAmount);
        Assert.Equal("air", await BillModeAsync(billId));

        var sea = await EditAsync(tenantId, "bill", billId, new { transport_mode = "SEA" });
        Assert.Equal(1, sea.RatingsMarkedStale);
        var afterSea = await LoadRatingAsync(billId);
        Assert.NotNull(afterSea.StaleAt);
        Assert.Equal(snapshot, afterSea.ContextJson);
        Assert.Equal(amount, afterSea.TotalAmount);
        Assert.Equal("SEA", await BillModeAsync(billId));

        var billIdWeight = await CreateBillAsync(tenantId, "BL-CANON-CW");
        await EditAsync(tenantId, "bill", billIdWeight, new { transport_mode = "AIR", gross_weight_kg = "85", volume_cbm = "0.9" });
        await RateAsync(tenantId, new { billId = billIdWeight, rateVersionId = versionId, rateDate = DateTimeOffset.UtcNow });
        var weighted = await LoadRatingAsync(billIdWeight);
        var weightSnapshot = weighted.ContextJson;
        var weightAmount = weighted.TotalAmount;

        var heavier = await EditAsync(tenantId, "bill", billIdWeight, new { gross_weight_kg = "120" });
        Assert.Equal(1, heavier.RatingsMarkedStale);
        var afterWeight = await LoadRatingAsync(billIdWeight);
        Assert.NotNull(afterWeight.StaleAt);
        Assert.Equal(weightSnapshot, afterWeight.ContextJson);
        Assert.Equal(weightAmount, afterWeight.TotalAmount);
    }

    [Fact]
    public async Task PublishedRateCard_KeepsStoredModeBytes_WhenTheSameModeIsSentInAnotherCase()
    {
        var tenantId = await CreateTenantAsync();
        using var create = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        create.Content = JsonContent.Create(new
        {
            code = "RC-PUB-MODE",
            name = "Giữ chữ",
            partyType = "vendor",
            currencyCode = "VND",
            transportMode = "air"
        });
        var cardId = (await ReadAsync<IdBody>(await _client.SendAsync(create))).Id;

        var created = await GetCardAsync(tenantId, cardId);
        Assert.Equal("AIR", created.TransportMode);

        await SetCardModeAsync(cardId, "air");
        using var versionReq = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        versionReq.Content = JsonContent.Create(new { note = "v1", effectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) });
        var versionId = (await ReadAsync<IdBody>(await _client.SendAsync(versionReq))).Id;
        using var rule = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId);
        rule.Content = JsonContent.Create(new
        {
            code = "KG",
            name = "Theo kg",
            calcMethod = "unit_rate",
            unitAmount = 2,
            currencyCode = "VND",
            sortOrder = 1
        });
        (await _client.SendAsync(rule)).EnsureSuccessStatusCode();
        using var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        var published = await _client.SendAsync(publish);
        if (!published.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await published.Content.ReadAsStringAsync());
        }

        var current = await GetCardAsync(tenantId, cardId);
        using var update = Tenant(HttpMethod.Put, $"/api/rate-cards/{cardId}", tenantId);
        update.Content = JsonContent.Create(new
        {
            name = "Giữ chữ",
            partyType = "vendor",
            currencyCode = "VND",
            isActive = true,
            transportMode = "AIR",
            rowVersion = current.RowVersion
        });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(update)).StatusCode);

        Assert.Equal("air", await CardModeAsync(cardId));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var version = await db.RateVersions.IgnoreQueryFilters().SingleAsync(v => v.Id == versionId);
        Assert.Equal("published", version.Status);
    }

    private async Task SetBillModeAsync(Guid billId, string mode)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var bill = await db.Bills.IgnoreQueryFilters().SingleAsync(b => b.Id == billId);
        bill.TransportMode = mode;
        await db.SaveChangesAsync();
    }

    private async Task<string?> BillModeAsync(Guid billId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        return await db.Bills.IgnoreQueryFilters().Where(b => b.Id == billId).Select(b => b.TransportMode).SingleAsync();
    }

    private async Task SetCardModeAsync(Guid cardId, string mode)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var card = await db.RateCards.IgnoreQueryFilters().SingleAsync(c => c.Id == cardId);
        card.TransportMode = mode;
        await db.SaveChangesAsync();
    }

    private async Task<string?> CardModeAsync(Guid cardId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        return await db.RateCards.IgnoreQueryFilters().Where(c => c.Id == cardId).Select(c => c.TransportMode).SingleAsync();
    }

    private async Task<RatingRow> LoadRatingAsync(Guid billId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var rows = await db.Ratings.IgnoreQueryFilters().Where(r => r.BillId == billId).ToListAsync();
        var rating = rows.OrderByDescending(r => r.RatedAt).First();
        return new RatingRow(rating.ContextJson, rating.TotalAmount, rating.StaleAt);
    }

    private async Task<CardBody> GetCardAsync(Guid tenantId, Guid cardId)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/rate-cards/{cardId}", tenantId);
        return await ReadAsync<CardBody>(await _client.SendAsync(req));
    }

    private async Task<EditResultBody> EditAsync(Guid tenantId, string type, Guid id, object changes)
    {
        using var req = Tenant(HttpMethod.Patch, $"/api/operational-references/{type}/{id}", tenantId);
        req.Content = JsonContent.Create(new { changes });
        return await ReadAsync<EditResultBody>(await _client.SendAsync(req));
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        using var req = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        req.Content = JsonContent.Create(body);
        return (await ReadAsync<IdBody>(await _client.SendAsync(req))).Id;
    }

    private async Task<Guid> PublishedUnitRateAsync(Guid tenantId, string code)
    {
        using var cardReq = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        cardReq.Content = JsonContent.Create(new { code, name = code, partyType = "vendor", currencyCode = "USD" });
        var cardId = (await ReadAsync<IdBody>(await _client.SendAsync(cardReq))).Id;
        using var versionReq = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        versionReq.Content = JsonContent.Create(new { note = code, effectiveFrom = DateTimeOffset.UtcNow.AddDays(-1) });
        var versionId = (await ReadAsync<IdBody>(await _client.SendAsync(versionReq))).Id;
        using var rule = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId);
        rule.Content = JsonContent.Create(new
        {
            code = "KG",
            name = "Theo kg",
            calcMethod = "unit_rate",
            unitAmount = 2,
            currencyCode = "USD",
            volumetricFactor = 167,
            sortOrder = 1
        });
        (await _client.SendAsync(rule)).EnsureSuccessStatusCode();
        using var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        (await _client.SendAsync(publish)).EnsureSuccessStatusCode();
        return versionId;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = Tenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        return (await ReadAsync<IdBody>(await _client.SendAsync(req))).Id;
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Master" });
        return (await ReadAsync<IdBody>(response)).Id;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        }

        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record EditResultBody(List<string> ChangedFields, int RatingsMarkedStale);
    private sealed record CardBody(string? TransportMode, string? RowVersion);
    private sealed record RatingRow(string? ContextJson, decimal TotalAmount, DateTimeOffset? StaleAt);
}
