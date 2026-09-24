using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class MaturityIdempotencyTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public MaturityIdempotencyTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task ConfirmReplay_KeepsTheAmount_AndRejectsTheSameKeyOnAnotherCost()
    {
        var tenantId = await CreateTenantAsync("TN-IDEM-MAT", "Idem maturity");
        var billId = await PostIdAsync(tenantId, "/api/bills", new { billNo = "BL-IDEM-MAT", billType = "freight" });
        var costA = await PostIdAsync(tenantId, "/api/costs", new
        {
            billId,
            attributionType = "direct",
            amount = 1000m,
            currencyCode = "VND",
            costTypeCode = "FREIGHT"
        });
        var costB = await PostIdAsync(tenantId, "/api/costs", new
        {
            billId,
            attributionType = "direct",
            amount = 500m,
            currencyCode = "VND",
            costTypeCode = "THC"
        });

        const string key = "confirm-cost-a";
        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmAsync(tenantId, costA, 1200m, key, ifMatch: null)).StatusCode);

        var replay = await ConfirmAsync(tenantId, costA, 1500m, key, ifMatch: "not-the-current-version");
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);

        var after = await GetCostAsync(tenantId, costA);
        Assert.Equal("confirmed", after.FinancialMaturity);
        Assert.Equal(1000m, after.ExpectedAmount);
        Assert.Equal(1200m, after.ConfirmedAmount);

        var other = await ConfirmAsync(tenantId, costB, 500m, key, ifMatch: null);
        Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
        var err = await other.Content.ReadFromJsonAsync<ErrorBody>(JsonOptions);
        Assert.Contains("bản ghi khác", err!.Message, StringComparison.Ordinal);

        var costBRow = await GetCostAsync(tenantId, costB);
        Assert.Equal("expected", costBRow.FinancialMaturity);
        Assert.Equal(500m, costBRow.Amount);
    }

    private async Task<HttpResponseMessage> ConfirmAsync(
        Guid tenantId,
        Guid costId,
        decimal amount,
        string key,
        string? ifMatch)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        if (ifMatch is not null)
        {
            req.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await _client.SendAsync(req);
    }

    private async Task<CostBody> GetCostAsync(Guid tenantId, Guid costId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/costs/{costId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CostBody>(JsonOptions))!;
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(JsonOptions))!.Id;
    }

    private async Task<Guid> PostIdAsync(Guid tenantId, string path, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(JsonOptions))!.Id;
    }

    private sealed record IdBody(Guid Id);
    private sealed record ErrorBody(string Message);
    private sealed record CostBody(string FinancialMaturity, decimal Amount, decimal ExpectedAmount, decimal? ConfirmedAmount);
}
