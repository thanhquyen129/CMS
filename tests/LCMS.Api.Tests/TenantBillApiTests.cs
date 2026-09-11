using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

public sealed class TenantBillApiTests : IClassFixture<LcmsApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public TenantBillApiTests(LcmsApiFactory factory)
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
    public async Task CreateTenant_ThenGet_ReturnsTenant()
    {
        var create = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-A", name = "Thuê bao A" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        Assert.NotNull(created);

        var get = await _client.GetAsync($"/api/tenants/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var tenant = await get.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions);
        Assert.Equal("TN-A", tenant!.Code);
        Assert.Equal("Thuê bao A", tenant.Name);
    }

    [Fact]
    public async Task CreateBill_WithoutTenantHeader_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/bills", new
        {
            billNo = "BILL-1",
            billType = "freight"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("tenant_required", body!.Code);
        Assert.Contains("thuê bao", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetBill_CrossTenant_Returns404_NotLeaked()
    {
        var tenantA = await CreateTenantAsync("TN-ISO-A", "Tenant A");
        var tenantB = await CreateTenantAsync("TN-ISO-B", "Tenant B");

        using var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "BL-100", billType = "freight" })
        };
        createReq.Headers.Add("X-Tenant-Id", tenantA.ToString());
        var createRes = await _client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var bill = await createRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        Assert.NotNull(bill);

        using var getAsA = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{bill!.Id}");
        getAsA.Headers.Add("X-Tenant-Id", tenantA.ToString());
        var ok = await _client.SendAsync(getAsA);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        using var getAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{bill.Id}");
        getAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        var leaked = await _client.SendAsync(getAsB);
        Assert.Equal(HttpStatusCode.NotFound, leaked.StatusCode);

        var err = await leaked.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("not_found", err!.Code);
        Assert.DoesNotContain(bill.Id.ToString(), err.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateBill_DuplicateBillNo_Returns409()
    {
        var tenantId = await CreateTenantAsync("TN-DUP", "Tenant Dup");

        using var first = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "DUP-1", billType = "freight" })
        };
        first.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(first)).StatusCode);

        using var second = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "DUP-1", billType = "freight" })
        };
        second.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var conflict = await _client.SendAsync(second);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record TenantResponse(Guid Id, string Code, string Name, bool IsActive);
    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
