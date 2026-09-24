using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Ux06TenantIsolationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Ux06TenantIsolationTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task TenantA_CannotReadBillOrCostOfTenantB_EvenWithSpoofedHeader()
    {
        var tenantA = await CreateTenantAsync("TN-UX06-A", "Cô lập A");
        var tenantB = await CreateTenantAsync("TN-UX06-B", "Cô lập B");

        var billB = await PostIdAsync(tenantB, "/api/bills", new { billNo = "BL-UX06-B", billType = "freight" });
        var costB = await PostIdAsync(tenantB, "/api/costs", new
        {
            billId = billB,
            attributionType = "direct",
            amount = 8800m,
            currencyCode = "VND",
            costTypeCode = "FREIGHT"
        });

        var billsAsA = await ListAsync(tenantA, "/api/bills", bearer: null, spoofTenant: null);
        var costsAsA = await ListAsync(tenantA, "/api/costs", bearer: null, spoofTenant: null);
        Assert.DoesNotContain("BL-UX06-B", billsAsA, StringComparison.Ordinal);
        Assert.DoesNotContain("8800", costsAsA, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(tenantA, $"/api/bills/{billB}", null, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(tenantA, $"/api/costs/{costB}", null, null)).StatusCode);

        var token = await TokenAsync(tenantA);
        var spoofedBills = await ListAsync(tenantA, "/api/bills", token, tenantB);
        var spoofedCosts = await ListAsync(tenantA, "/api/costs", token, tenantB);
        Assert.DoesNotContain("BL-UX06-B", spoofedBills, StringComparison.Ordinal);
        Assert.DoesNotContain("8800", spoofedCosts, StringComparison.Ordinal);

        var spoofedBill = await GetAsync(tenantA, $"/api/bills/{billB}", token, tenantB);
        var spoofedCost = await GetAsync(tenantA, $"/api/costs/{costB}", token, tenantB);
        Assert.Equal(HttpStatusCode.NotFound, spoofedBill.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, spoofedCost.StatusCode);
        var billErr = await spoofedBill.Content.ReadFromJsonAsync<ErrorBody>(JsonOptions);
        var costErr = await spoofedCost.Content.ReadFromJsonAsync<ErrorBody>(JsonOptions);
        Assert.Equal("not_found", billErr!.Code);
        Assert.Equal("not_found", costErr!.Code);
        Assert.DoesNotContain("BL-UX06-B", billErr.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(costB.ToString(), costErr.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UiSession_DoesNotForwardTenantHeader_AndCost404IsANotFoundPage()
    {
        var bff = File.ReadAllText(Find("apps", "web", "lib", "bff-api.ts"));
        Assert.Contains("Authorization: `Bearer ${token}`", bff, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Tenant-Id", bff, StringComparison.Ordinal);

        var costPage = File.ReadAllText(Find("apps", "web", "app", "costs", "[id]", "page.tsx"));
        Assert.Contains("costRes.status === 404", costPage, StringComparison.Ordinal);
        Assert.Contains("Không tìm thấy {costLabel}", costPage, StringComparison.Ordinal);

        var billPage = File.ReadAllText(Find("apps", "web", "app", "bills", "[id]", "page.tsx"));
        Assert.Contains("billRes.status === 404", billPage, StringComparison.Ordinal);
        Assert.Contains("Không tìm thấy {billLabel}", billPage, StringComparison.Ordinal);
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

    private async Task<string> TokenAsync(Guid tenantId)
    {
        var res = await _client.PostAsJsonAsync("/api/dev/token", new { tenantId, userId = Guid.NewGuid() });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<TokenBody>(JsonOptions))!.AccessToken;
    }

    private async Task<string> ListAsync(Guid tenantId, string path, string? bearer, Guid? spoofTenant)
    {
        var res = await GetAsync(tenantId, path, bearer, spoofTenant);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> GetAsync(Guid tenantId, string path, string? bearer, Guid? spoofTenant)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        if (bearer is null)
        {
            req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        }
        else
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            if (spoofTenant is Guid spoof)
            {
                req.Headers.Add("X-Tenant-Id", spoof.ToString());
            }
        }

        return await _client.SendAsync(req);
    }

    private static string Find(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }

    private sealed record IdBody(Guid Id);
    private sealed record TokenBody(string AccessToken);
    private sealed record ErrorBody(string Code, string Message);
}
