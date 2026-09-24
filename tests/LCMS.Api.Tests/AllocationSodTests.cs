using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class AllocationSodTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public AllocationSodTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Creator_CannotFinalize_WhenActingAsUser_OtherUserCan()
    {
        var tenantId = await CreateTenantAsync();
        var creatorId = await CreateUserAsync(tenantId, "alloc-creator@example.com", "Creator");
        var finisherId = await CreateUserAsync(tenantId, "alloc-finisher@example.com", "Finisher");

        var billA = await CreateBillAsync(tenantId, "BL-SOD-A");
        var billB = await CreateBillAsync(tenantId, "BL-SOD-B");
        var sharedId = await CreateSharedAsync(tenantId, 100m);
        var allocationId = await Allocate(tenantId, creatorId, sharedId, "equal", new[] { billA, billB });

        using (var self = Tenant(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/finalize", tenantId, creatorId))
        {
            var blocked = await _client.SendAsync(self);
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            Assert.Contains("PC-21", await blocked.Content.ReadAsStringAsync());
        }

        using (var ok = Tenant(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/finalize", tenantId, finisherId))
        {
            var res = await _client.SendAsync(ok);
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        }

        var cost = await GetCost(tenantId, sharedId);
        var session = Assert.Single(cost.Allocations);
        Assert.Equal("finalized", session.AllocationStatus);
    }

    [Fact]
    public async Task Finalize_StaleIfMatch_Conflicts_AndLeavesTheSessionOpen()
    {
        var tenantId = await CreateTenantAsync();
        var billA = await CreateBillAsync(tenantId, "BL-SOD-VER-A");
        var billB = await CreateBillAsync(tenantId, "BL-SOD-VER-B");
        var sharedId = await CreateSharedAsync(tenantId, 80m);
        var allocationId = await Allocate(tenantId, null, sharedId, "equal", new[] { billA, billB });

        using var fin = Tenant(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/finalize", tenantId);
        fin.Headers.TryAddWithoutValidation("If-Match", "not-a-version");
        var blocked = await _client.SendAsync(fin);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<ErrorBody>(Json);
        Assert.Equal("concurrency_conflict", err!.Code);

        var cost = await GetCost(tenantId, sharedId);
        Assert.NotEqual("finalized", Assert.Single(cost.Allocations).AllocationStatus);
    }

    [Fact]
    public async Task StaleIfMatch_DoesNotCalculateSubmitOrCancelAllocation()
    {
        var tenantId = await CreateTenantAsync();
        var billA = await CreateBillAsync(tenantId, "BL-SOD-STEP-A");
        var billB = await CreateBillAsync(tenantId, "BL-SOD-STEP-B");
        var sharedId = await CreateSharedAsync(tenantId, 60m);
        var allocationId = await Allocate(tenantId, null, sharedId, "equal", new[] { billA, billB });

        using (var calc = Tenant(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/calculate", tenantId))
        {
            calc.Headers.TryAddWithoutValidation("If-Match", "not-a-version");
            var blocked = await _client.SendAsync(calc);
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            var err = await blocked.Content.ReadFromJsonAsync<ErrorBody>(Json);
            Assert.Equal("concurrency_conflict", err!.Code);
        }

        Assert.Equal("draft", (await GetCost(tenantId, sharedId)).Allocations.Single().AllocationStatus);

        using (var calc = Tenant(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/calculate", tenantId))
        {
            (await _client.SendAsync(calc)).EnsureSuccessStatusCode();
        }

        using (var submit = Tenant(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/submit", tenantId))
        {
            submit.Headers.TryAddWithoutValidation("If-Match", "not-a-version");
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(submit)).StatusCode);
        }

        using (var cancel = Tenant(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/cancel", tenantId))
        {
            cancel.Headers.TryAddWithoutValidation("If-Match", "not-a-version");
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(cancel)).StatusCode);
        }

        Assert.Equal("calculated", (await GetCost(tenantId, sharedId)).Allocations.Single().AllocationStatus);
    }

    [Fact]
    public async Task Finalize_WithoutUserHeader_StillAllowed_ForBootstrapTests()
    {
        var tenantId = await CreateTenantAsync();
        var billA = await CreateBillAsync(tenantId, "BL-SOD-X");
        var billB = await CreateBillAsync(tenantId, "BL-SOD-Y");
        var sharedId = await CreateSharedAsync(tenantId, 50m);
        var allocationId = await Allocate(tenantId, null, sharedId, "equal", new[] { billA, billB });
        using var fin = Tenant(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/finalize", tenantId);
        var res = await _client.SendAsync(fin);
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tenants",
            new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "SoD Alloc" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateUserAsync(Guid tenantId, string email, string displayName)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email, displayName })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = Tenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new
        {
            billNo,
            billType = "house",
            sourceSystem = "lcms_manual",
            externalId = billNo
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateSharedAsync(Guid tenantId, decimal amount)
    {
        using var req = Tenant(HttpMethod.Post, "/api/costs", tenantId);
        req.Content = JsonContent.Create(new
        {
            attributionType = "shared",
            amount,
            currencyCode = "VND",
            costTypeCode = "SHARED"
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> Allocate(
        Guid tenantId,
        Guid? userId,
        Guid costId,
        string basis,
        Guid[] billIds)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/costs/{costId}/allocations", tenantId, userId);
        req.Content = JsonContent.Create(new
        {
            allocationBasis = basis,
            details = billIds.Select(id => new { billId = id }).ToArray()
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<CostBody> GetCost(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/costs/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CostBody>(Json))!;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId, Guid? userId = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (userId.HasValue)
        {
            req.Headers.Add("X-User-Id", userId.Value.ToString());
        }
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record ErrorBody(string Code);
    private sealed record CostBody(List<AllocBody> Allocations);
    private sealed record AllocBody(Guid Id, string AllocationStatus);
}
