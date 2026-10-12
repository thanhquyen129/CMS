using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class BillRateSuggestionApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public BillRateSuggestionApiTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Suggestion_UsesRuleRoute_AndRetiredCardCannotRate()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId);
        await PatchAsync(tenantId, $"/api/bills/{billId}/context", new { routeCode = "SGN-HAN", transportMode = "air" });

        var match = await PublishedCardAsync(tenantId, "RC-MATCH", "vendor", "SGN-HAN", 1000m);
        var wrong = await PublishedCardAsync(tenantId, "RC-WRONG", "vendor", "SGN-DAD", 900m);

        var suggestion = await GetAsync<SuggestionBody>(tenantId, $"/api/bills/{billId}/rate-suggestions");
        Assert.Contains(suggestion.Buy.Options, o => o.VersionId == match.VersionId && o.Selectable);
        var blocked = Assert.Single(suggestion.Buy.Options, o => o.VersionId == wrong.VersionId);
        Assert.False(blocked.Selectable);
        Assert.Equal("Không khớp tuyến", blocked.Reason);

        await PutAsync(tenantId, $"/api/rate-cards/{match.CardId}", new
        {
            name = "RC-MATCH",
            partyType = "vendor",
            currencyCode = "VND",
            description = (string?)null,
            isActive = false,
            transportMode = "air",
            routeCode = "SGN-HAN"
        });

        var after = await GetAsync<SuggestionBody>(tenantId, $"/api/bills/{billId}/rate-suggestions");
        var retired = Assert.Single(after.Buy.Options, o => o.VersionId == match.VersionId);
        Assert.False(retired.Selectable);
        Assert.Contains("ngừng", retired.Reason, StringComparison.OrdinalIgnoreCase);

        using var rate = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        rate.Content = JsonContent.Create(new { billId, rateVersionId = match.VersionId, quantity = 1 });
        var denied = await _client.SendAsync(rate);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
    }

    [Fact]
    public async Task Margin_IsBuySellRatingsOnly_AndHidesProfitWhenFxIsMissing()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId);
        var buy = await PublishedCardAsync(tenantId, "RC-BUY", "vendor", null, 1000m);
        var sell = await PublishedCardAsync(tenantId, "RC-SELL", "customer", null, 1800m);
        await RateAsync(tenantId, billId, buy.VersionId);
        await RateAsync(tenantId, billId, sell.VersionId);

        var margin = await GetAsync<MarginBody>(tenantId, $"/api/bills/{billId}/rating-margin");
        Assert.False(margin.FxMissing);
        Assert.Equal(800m, margin.ReportingProfit);

        var usd = await PublishedCardAsync(tenantId, "RC-USD", "customer", null, 10m, "USD");
        await RateAsync(tenantId, billId, usd.VersionId);
        var missing = await GetAsync<MarginBody>(tenantId, $"/api/bills/{billId}/rating-margin");
        Assert.True(missing.FxMissing);
        Assert.Null(missing.ReportingProfit);
    }

    [Fact]
    public async Task CostAccountant_DoesNotSeeSellRatingOrProfit()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId);
        var buy = await PublishedCardAsync(tenantId, "RC-BUY-SOD", "vendor", null, 1000m);
        var sell = await PublishedCardAsync(tenantId, "RC-SELL-SOD", "customer", null, 1800m);
        await RateAsync(tenantId, billId, buy.VersionId);
        await RateAsync(tenantId, billId, sell.VersionId);
        var costUser = await UserInRoleAsync(tenantId, "cost-margin@example.com", "CostAccountant");

        var margin = await GetAsync<MarginBody>(tenantId, $"/api/bills/{billId}/rating-margin", costUser);
        Assert.Equal(1000m, margin.BuyAmount);
        Assert.Null(margin.SellAmount);
        Assert.Null(margin.ReportingProfit);

        var history = await GetAsync<List<HistoryBody>>(tenantId, $"/api/bills/{billId}/ratings", costUser);
        Assert.DoesNotContain(history, row => string.Equals(row.PartyTypeCode, "customer", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(history, row => string.Equals(row.PartyTypeCode, "vendor", StringComparison.OrdinalIgnoreCase));
    }

    private async Task RateAsync(Guid tenantId, Guid billId, Guid versionId)
    {
        using var rate = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        rate.Content = JsonContent.Create(new { billId, rateVersionId = versionId, quantity = 1 });
        (await _client.SendAsync(rate)).EnsureSuccessStatusCode();
    }

    private async Task<(Guid CardId, Guid VersionId)> PublishedCardAsync(
        Guid tenantId, string code, string party, string? route, decimal amount, string currency = "VND")
    {
        Guid cardId;
        using (var create = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId))
        {
            create.Content = JsonContent.Create(new
            {
                code,
                name = code,
                partyType = party,
                currencyCode = currency,
                transportMode = "air",
                routeCode = (string?)null
            });
            var res = await _client.SendAsync(create);
            res.EnsureSuccessStatusCode();
            cardId = (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        }

        Guid versionId;
        using (var version = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId))
        {
            version.Content = JsonContent.Create(new { note = "v1" });
            var res = await _client.SendAsync(version);
            res.EnsureSuccessStatusCode();
            versionId = (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        }

        using (var rule = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId))
        {
            rule.Content = JsonContent.Create(new
            {
                code = "FREIGHT",
                name = "Cước",
                calcMethod = "fixed",
                unitAmount = amount,
                currencyCode = currency,
                routeCode = route,
                transportMode = route is null ? null : "air",
                sortOrder = 1
            });
            (await _client.SendAsync(rule)).EnsureSuccessStatusCode();
        }

        using var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        (await _client.SendAsync(publish)).EnsureSuccessStatusCode();
        return (cardId, versionId);
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Suggestion" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId)
    {
        using var req = Tenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo = "BL-" + Guid.NewGuid().ToString("N")[..8], billType = "house" });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task PatchAsync(Guid tenantId, string path, object body)
    {
        using var req = Tenant(HttpMethod.Patch, path, tenantId);
        req.Content = JsonContent.Create(body);
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task PutAsync(Guid tenantId, string path, object body)
    {
        using var req = Tenant(HttpMethod.Put, path, tenantId);
        req.Content = JsonContent.Create(body);
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<T> GetAsync<T>(Guid tenantId, string path, Guid? userId = null)
    {
        using var req = Tenant(HttpMethod.Get, path, tenantId, userId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<Guid> UserInRoleAsync(Guid tenantId, string email, string roleCode)
    {
        using var createUser = Tenant(HttpMethod.Post, "/api/users", tenantId);
        createUser.Content = JsonContent.Create(new { email, displayName = roleCode });
        var userRes = await _client.SendAsync(createUser);
        userRes.EnsureSuccessStatusCode();
        var userId = (await userRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        using var listRoles = Tenant(HttpMethod.Get, "/api/roles", tenantId);
        var roles = await (await _client.SendAsync(listRoles)).Content.ReadFromJsonAsync<List<RoleBody>>(Json);
        var role = Assert.Single(roles!, r => r.Code == roleCode);
        using var assign = Tenant(HttpMethod.Post, $"/api/users/{userId}/roles/{role.Id}", tenantId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(assign)).StatusCode);
        return userId;
    }

    private HttpRequestMessage Tenant(HttpMethod method, string path, Guid tenantId, Guid? userId = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (userId is Guid id)
        {
            req.Headers.Add("X-User-Id", id.ToString());
        }

        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record SuggestionBody(SideBody Buy, SideBody Sell);
    private sealed record SideBody(IReadOnlyList<OptionBody> Options);
    private sealed record OptionBody(Guid VersionId, bool Selectable, string? Reason);
    private sealed record MarginBody(decimal? BuyAmount, decimal? SellAmount, decimal? ReportingProfit, bool FxMissing);
    private sealed record HistoryBody(string? PartyTypeCode);
    private sealed record RoleBody(Guid Id, string Code);
}
