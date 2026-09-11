using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint1IdentityMasterTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint1IdentityMasterTests(LcmsApiFactory factory)
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
    public async Task CreateTenant_SeedsAdminRole()
    {
        var tenantId = await CreateTenantAsync("TN-SEED", "Seed Tenant");

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var roles = await res.Content.ReadFromJsonAsync<List<RoleResponse>>(JsonOptions);
        Assert.NotNull(roles);
        Assert.Contains(roles!, r => r.Code == "Admin" && r.IsSystem);
    }

    [Fact]
    public async Task GetUser_CrossTenant_Returns404()
    {
        var tenantA = await CreateTenantAsync("TN-U-A", "User Tenant A");
        var tenantB = await CreateTenantAsync("TN-U-B", "User Tenant B");

        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email = "a@example.com", displayName = "User A" })
        };
        create.Headers.Add("X-Tenant-Id", tenantA.ToString());
        var created = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = await created.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);

        using var getAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{user!.Id}");
        getAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        var leaked = await _client.SendAsync(getAsB);
        Assert.Equal(HttpStatusCode.NotFound, leaked.StatusCode);
    }

    [Fact]
    public async Task GetOrganization_AndParty_CrossTenant_Returns404()
    {
        var tenantA = await CreateTenantAsync("TN-M-A", "Master A");
        var tenantB = await CreateTenantAsync("TN-M-B", "Master B");

        using var createOrg = new HttpRequestMessage(HttpMethod.Post, "/api/organizations")
        {
            Content = JsonContent.Create(new { code = "HQ", name = "Trụ sở", parentId = (Guid?)null })
        };
        createOrg.Headers.Add("X-Tenant-Id", tenantA.ToString());
        var orgRes = await _client.SendAsync(createOrg);
        Assert.Equal(HttpStatusCode.Created, orgRes.StatusCode);
        var org = await orgRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);

        using var createParty = new HttpRequestMessage(HttpMethod.Post, "/api/business-parties")
        {
            Content = JsonContent.Create(new { code = "CUS-1", name = "Khách hàng 1" })
        };
        createParty.Headers.Add("X-Tenant-Id", tenantA.ToString());
        var partyRes = await _client.SendAsync(createParty);
        Assert.Equal(HttpStatusCode.Created, partyRes.StatusCode);
        var party = await partyRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);

        using var getOrgB = new HttpRequestMessage(HttpMethod.Get, $"/api/organizations/{org!.Id}");
        getOrgB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(getOrgB)).StatusCode);

        using var getPartyB = new HttpRequestMessage(HttpMethod.Get, $"/api/business-parties/{party!.Id}");
        getPartyB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(getPartyB)).StatusCode);
    }

    [Fact]
    public async Task CreateBill_WithoutPermission_Returns403_Vietnamese_WithCorrelationId()
    {
        var tenantId = await CreateTenantAsync("TN-PERM", "Perm Tenant");

        using var createUser = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email = "noperm@example.com", displayName = "No Perm" })
        };
        createUser.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var userRes = await _client.SendAsync(createUser);
        Assert.Equal(HttpStatusCode.Created, userRes.StatusCode);
        var user = await userRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);

        const string cid = "sprint1-perm-deny-001";
        using var billReq = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "BL-DENY", billType = "freight" })
        };
        billReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        billReq.Headers.Add("X-User-Id", user!.Id.ToString());
        billReq.Headers.Add("X-Correlation-Id", cid);

        var denied = await _client.SendAsync(billReq);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var err = await denied.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("forbidden", err!.Code);
        Assert.Contains("không có quyền", err.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(cid, err.CorrelationId);
        Assert.True(denied.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Equal(cid, Assert.Single(values));
    }

    [Fact]
    public async Task AssignAdminRole_ThenCreateBill_Succeeds()
    {
        var tenantId = await CreateTenantAsync("TN-OK", "Ok Tenant");

        using var createUser = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email = "admin.user@example.com", displayName = "Admin User" })
        };
        createUser.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var user = (await (await _client.SendAsync(createUser)).Content
            .ReadFromJsonAsync<IdResponse>(JsonOptions))!;

        using var listRoles = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        listRoles.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var roles = await (await _client.SendAsync(listRoles)).Content
            .ReadFromJsonAsync<List<RoleResponse>>(JsonOptions);
        var adminRole = Assert.Single(roles!, r => r.Code == "Admin");

        using var assign = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/users/{user.Id}/roles/{adminRole.Id}");
        assign.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(assign)).StatusCode);

        using var billReq = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "BL-OK", billType = "freight" })
        };
        billReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        billReq.Headers.Add("X-User-Id", user.Id.ToString());
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(billReq)).StatusCode);
    }

    [Fact]
    public async Task UpsertCurrency_ThenList_ReturnsCurrency()
    {
        using var upsert = new HttpRequestMessage(HttpMethod.Put, "/api/currencies")
        {
            Content = JsonContent.Create(new
            {
                code = "VND",
                name = "Đồng Việt Nam",
                decimalPlaces = 0,
                isActive = true
            })
        };
        var upsertRes = await _client.SendAsync(upsert);
        Assert.Equal(HttpStatusCode.OK, upsertRes.StatusCode);

        var list = await _client.GetFromJsonAsync<List<CurrencyResponse>>("/api/currencies", JsonOptions);
        Assert.Contains(list!, c => c.Code == "VND" && c.Name == "Đồng Việt Nam" && c.DecimalPlaces == 0);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record RoleResponse(Guid Id, string Code, string Name, bool IsSystem);
    private sealed record CurrencyResponse(Guid Id, string Code, string Name, int DecimalPlaces, bool IsActive);
    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
