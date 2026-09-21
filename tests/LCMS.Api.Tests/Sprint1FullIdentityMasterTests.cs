using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

/// <summary>Pass 2 Sprint 1 FULL — Data Scope × Permission, inactive user, party_roles, org tree, currency harden.</summary>
[Collection("Api")]
public sealed class Sprint1FullIdentityMasterTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint1FullIdentityMasterTests(LcmsApiFactory factory)
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
    public async Task DataScope_Own_FiltersBillAndCost_ListAndGet()
    {
        var tenantId = await CreateTenantAsync("TN-DS-OWN", "Data Scope Own");
        var orgId = await CreateOrgAsync(tenantId, "HQ", "Trụ sở", null);

        var alice = await CreateUserAsync(tenantId, "alice@example.com", "Alice", orgId);
        var bob = await CreateUserAsync(tenantId, "bob@example.com", "Bob", orgId);

        var ownRole = await CreateRoleWithPermissionAsync(tenantId, "ReaderOwn", "bill.read", "own");
        await AssignPermissionAsync(tenantId, ownRole, "cost.read", "own");
        await AssignPermissionAsync(tenantId, ownRole, "bill.create", "own");
        await AssignPermissionAsync(tenantId, ownRole, "cost.create", "own");
        await AssignUserRoleAsync(tenantId, alice, ownRole);
        await AssignUserRoleAsync(tenantId, bob, ownRole);

        var billAlice = await CreateBillAsync(tenantId, alice, "BL-ALICE", orgId);
        var billBob = await CreateBillAsync(tenantId, bob, "BL-BOB", orgId);
        var costAlice = await CreateCostAsync(tenantId, alice, billAlice, 100m, orgId);
        _ = await CreateCostAsync(tenantId, bob, billBob, 200m, orgId);

        using var listBills = WithUser(HttpMethod.Get, "/api/bills", tenantId, alice);
        var listBillsRes = await _client.SendAsync(listBills);
        Assert.Equal(HttpStatusCode.OK, listBillsRes.StatusCode);
        var bills = await listBillsRes.Content.ReadFromJsonAsync<List<BillListItem>>(JsonOptions);
        Assert.NotNull(bills);
        Assert.Single(bills);
        Assert.Equal(billAlice, bills[0].Id);

        using var getBobBill = WithUser(HttpMethod.Get, $"/api/bills/{billBob}", tenantId, alice);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(getBobBill)).StatusCode);

        using var listCosts = WithUser(HttpMethod.Get, "/api/costs", tenantId, alice);
        var costs = await (await _client.SendAsync(listCosts)).Content
            .ReadFromJsonAsync<List<CostListItem>>(JsonOptions);
        Assert.Single(costs!);
        Assert.Equal(costAlice, costs[0].Id);
    }

    [Fact]
    public async Task DataScope_Organization_IncludesSubtree_ExcludesOtherBranch()
    {
        var tenantId = await CreateTenantAsync("TN-DS-ORG", "Data Scope Org");
        var hq = await CreateOrgAsync(tenantId, "HQ", "HQ", null);
        var north = await CreateOrgAsync(tenantId, "NORTH", "Miền Bắc", hq);
        var south = await CreateOrgAsync(tenantId, "SOUTH", "Miền Nam", hq);
        var hanoi = await CreateOrgAsync(tenantId, "HN", "Hà Nội", north);

        var northUser = await CreateUserAsync(tenantId, "north@example.com", "North", north);
        var southUser = await CreateUserAsync(tenantId, "south@example.com", "South", south);

        var orgRole = await CreateRoleWithPermissionAsync(tenantId, "OrgReader", "bill.read", "organization");
        await AssignPermissionAsync(tenantId, orgRole, "cost.read", "organization");
        await AssignPermissionAsync(tenantId, orgRole, "bill.create", "organization");
        await AssignPermissionAsync(tenantId, orgRole, "cost.create", "organization");
        await AssignUserRoleAsync(tenantId, northUser, orgRole);
        await AssignUserRoleAsync(tenantId, southUser, orgRole);

        var billNorth = await CreateBillAsync(tenantId, northUser, "BL-N", north);
        var billHanoi = await CreateBillAsync(tenantId, northUser, "BL-HN", hanoi);
        var billSouth = await CreateBillAsync(tenantId, southUser, "BL-S", south);
        _ = await CreateCostAsync(tenantId, northUser, billNorth, 10m, north);
        _ = await CreateCostAsync(tenantId, southUser, billSouth, 20m, south);

        using var listBills = WithUser(HttpMethod.Get, "/api/bills", tenantId, northUser);
        var listBillsRes = await _client.SendAsync(listBills);
        Assert.Equal(HttpStatusCode.OK, listBillsRes.StatusCode);
        var bills = await listBillsRes.Content.ReadFromJsonAsync<List<BillListItem>>(JsonOptions);
        Assert.NotNull(bills);
        Assert.Equal(2, bills.Count);
        Assert.Contains(bills, b => b.Id == billNorth);
        Assert.Contains(bills, b => b.Id == billHanoi);
        Assert.DoesNotContain(bills, b => b.Id == billSouth);

        using var getSouth = WithUser(HttpMethod.Get, $"/api/bills/{billSouth}", tenantId, northUser);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(getSouth)).StatusCode);

        using var listCosts = WithUser(HttpMethod.Get, "/api/costs", tenantId, northUser);
        var costs = await (await _client.SendAsync(listCosts)).Content
            .ReadFromJsonAsync<List<CostListItem>>(JsonOptions);
        Assert.Single(costs!);
        Assert.Equal(north, costs[0].OrganizationId);
    }

    [Fact]
    public async Task InactiveUser_Denied_EvenWithRole()
    {
        var tenantId = await CreateTenantAsync("TN-INACT", "Inactive User");
        var userId = await CreateUserAsync(tenantId, "inactive@example.com", "Inactive", null);

        using var listRoles = WithTenant(HttpMethod.Get, "/api/roles", tenantId);
        var roles = await (await _client.SendAsync(listRoles)).Content
            .ReadFromJsonAsync<List<RoleResponse>>(JsonOptions);
        var admin = Assert.Single(roles!, r => r.Code == "Admin");
        var keeperId = await CreateUserAsync(tenantId, "keeper@example.com", "Keeper", null);
        await AssignUserRoleAsync(tenantId, keeperId, admin.Id);
        await AssignUserRoleAsync(tenantId, userId, admin.Id);

        using var deactivate = WithTenant(HttpMethod.Put, $"/api/users/{userId}", tenantId);
        deactivate.Content = JsonContent.Create(new
        {
            displayName = "Inactive",
            isActive = false,
            organizationId = (Guid?)null
        });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(deactivate)).StatusCode);

        const string cid = "sprint1-full-inactive-001";
        using var billReq = WithUser(HttpMethod.Post, "/api/bills", tenantId, userId);
        billReq.Headers.Add("X-Correlation-Id", cid);
        billReq.Content = JsonContent.Create(new { billNo = "BL-INACT", billType = "freight" });
        var denied = await _client.SendAsync(billReq);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var err = await denied.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("forbidden", err!.Code);
        Assert.Contains("không còn hiệu lực", err.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(cid, err.CorrelationId);
    }

    [Fact]
    public async Task JwtSub_StampsCreatedBy_OnBill()
    {
        var tenantId = await CreateTenantAsync("TN-JWT-ACT", "JWT Actor");
        var userId = await CreateUserAsync(tenantId, "actor@example.com", "Actor", null);
        using var listRoles = WithTenant(HttpMethod.Get, "/api/roles", tenantId);
        var roles = await (await _client.SendAsync(listRoles)).Content
            .ReadFromJsonAsync<List<RoleResponse>>(JsonOptions);
        var admin = Assert.Single(roles!, r => r.Code == "Admin");
        await AssignUserRoleAsync(tenantId, userId, admin.Id);

        var tokenRes = await _client.PostAsJsonAsync("/api/dev/token", new { tenantId, userId });
        Assert.Equal(HttpStatusCode.OK, tokenRes.StatusCode);
        var token = (await tokenRes.Content.ReadFromJsonAsync<DevTokenResponse>(JsonOptions))!.AccessToken;

        using var createBill = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "BL-ACTOR", billType = "freight" })
        };
        createBill.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var created = await _client.SendAsync(createBill);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var billId = (await created.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var getBill = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billId}");
        getBill.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var bill = await (await _client.SendAsync(getBill)).Content
            .ReadFromJsonAsync<BillDetail>(JsonOptions);
        Assert.Equal(userId, bill!.CreatedBy);
    }

    [Fact]
    public async Task PartyRoles_AssignListRevoke_AndOrgTreeChildren()
    {
        var tenantId = await CreateTenantAsync("TN-PARTY", "Party Org");
        var hq = await CreateOrgAsync(tenantId, "HQ", "HQ", null);
        var child = await CreateOrgAsync(tenantId, "BR1", "Chi nhánh 1", hq);

        using var treeReq = WithTenant(HttpMethod.Get, "/api/organizations/tree", tenantId);
        var tree = await (await _client.SendAsync(treeReq)).Content
            .ReadFromJsonAsync<List<OrgTreeNode>>(JsonOptions);
        var root = Assert.Single(tree!);
        Assert.Equal(hq, root.Id);
        Assert.Single(root.Children);
        Assert.Equal(child, root.Children[0].Id);

        using var childrenReq = WithTenant(HttpMethod.Get, $"/api/organizations/{hq}/children", tenantId);
        var children = await (await _client.SendAsync(childrenReq)).Content
            .ReadFromJsonAsync<List<OrgDto>>(JsonOptions);
        Assert.Single(children!);
        Assert.Equal(child, children[0].Id);

        using var createParty = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        createParty.Content = JsonContent.Create(new { code = "VND-1", name = "Vendor 1" });
        var partyId = (await (await _client.SendAsync(createParty)).Content
            .ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var assign = WithTenant(HttpMethod.Post, $"/api/business-parties/{partyId}/roles", tenantId);
        assign.Content = JsonContent.Create(new { roleCode = "vendor" });
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(assign)).StatusCode);

        using var listRoles = WithTenant(HttpMethod.Get, $"/api/business-parties/{partyId}/roles", tenantId);
        var roles = await (await _client.SendAsync(listRoles)).Content
            .ReadFromJsonAsync<List<PartyRoleDto>>(JsonOptions);
        Assert.Contains(roles!, r => r.RoleCode == "vendor");

        using var revoke = WithTenant(HttpMethod.Delete, $"/api/business-parties/{partyId}/roles/vendor", tenantId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(revoke)).StatusCode);

        using var listAfter = WithTenant(HttpMethod.Get, $"/api/business-parties/{partyId}/roles", tenantId);
        var after = await (await _client.SendAsync(listAfter)).Content
            .ReadFromJsonAsync<List<PartyRoleDto>>(JsonOptions);
        Assert.Empty(after!);
    }

    [Fact]
    public async Task Currency_Harden_RejectsInactive_AndProtectsBaselineDecimals()
    {
        var list = await _client.GetFromJsonAsync<List<CurrencyResponse>>("/api/currencies?activeOnly=true", JsonOptions);
        Assert.Contains(list!, c => c.Code == "VND" && c.DecimalPlaces == 0);
        Assert.Contains(list!, c => c.Code == "USD");

        var vnd = await _client.GetFromJsonAsync<CurrencyResponse>("/api/currencies/VND", JsonOptions);
        Assert.Equal(0, vnd!.DecimalPlaces);

        using var badDecimals = new HttpRequestMessage(HttpMethod.Put, "/api/currencies")
        {
            Content = JsonContent.Create(new
            {
                code = "VND",
                name = "Đồng Việt Nam",
                decimalPlaces = 2,
                isActive = true
            })
        };
        var badRes = await _client.SendAsync(badDecimals);
        Assert.Equal(HttpStatusCode.BadRequest, badRes.StatusCode);

        using var deactivate = new HttpRequestMessage(HttpMethod.Put, "/api/currencies")
        {
            Content = JsonContent.Create(new
            {
                code = "XXX",
                name = "Test Inactive",
                decimalPlaces = 2,
                isActive = false
            })
        };
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(deactivate)).StatusCode);

        var tenantId = await CreateTenantAsync("TN-FX", "Currency Harden");
        using var billReq = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        billReq.Content = JsonContent.Create(new { billNo = "BL-FX", billType = "freight" });
        var billId = (await (await _client.SendAsync(billReq)).Content
            .ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var costReq = WithTenant(HttpMethod.Post, "/api/costs", tenantId);
        costReq.Content = JsonContent.Create(new
        {
            billId,
            attributionType = "direct",
            amount = 1m,
            currencyCode = "XXX",
            costTypeCode = "TEST"
        });
        var costRes = await _client.SendAsync(costReq);
        Assert.Equal(HttpStatusCode.BadRequest, costRes.StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateOrgAsync(Guid tenantId, string code, string name, Guid? parentId)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/organizations", tenantId);
        req.Content = JsonContent.Create(new { code, name, parentId });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateUserAsync(Guid tenantId, string email, string displayName, Guid? organizationId)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/users", tenantId);
        req.Content = JsonContent.Create(new { email, displayName, organizationId });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateRoleWithPermissionAsync(
        Guid tenantId,
        string code,
        string actionCode,
        string dataScope)
    {
        using var create = WithTenant(HttpMethod.Post, "/api/roles", tenantId);
        create.Content = JsonContent.Create(new { code, name = code });
        var roleId = (await (await _client.SendAsync(create)).Content
            .ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        await AssignPermissionAsync(tenantId, roleId, actionCode, dataScope);
        return roleId;
    }

    private async Task AssignPermissionAsync(Guid tenantId, Guid roleId, string actionCode, string dataScope)
    {
        using var req = WithTenant(HttpMethod.Post, $"/api/roles/{roleId}/permissions", tenantId);
        req.Content = JsonContent.Create(new { actionCode, dataScope });
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    private async Task AssignUserRoleAsync(Guid tenantId, Guid userId, Guid roleId)
    {
        using var req = WithTenant(HttpMethod.Post, $"/api/users/{userId}/roles/{roleId}", tenantId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, Guid userId, string billNo, Guid? organizationId)
    {
        using var req = WithUser(HttpMethod.Post, "/api/bills", tenantId, userId);
        req.Content = JsonContent.Create(new { billNo, billType = "freight", organizationId });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateCostAsync(
        Guid tenantId,
        Guid userId,
        Guid billId,
        decimal amount,
        Guid? organizationId)
    {
        using var req = WithUser(HttpMethod.Post, "/api/costs", tenantId, userId);
        req.Content = JsonContent.Create(new
        {
            billId,
            attributionType = "direct",
            amount,
            currencyCode = "VND",
            costTypeCode = "FREIGHT",
            organizationId
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private static HttpRequestMessage WithTenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private static HttpRequestMessage WithUser(HttpMethod method, string url, Guid tenantId, Guid userId)
    {
        var req = WithTenant(method, url, tenantId);
        req.Headers.Add("X-User-Id", userId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record RoleResponse(Guid Id, string Code, string Name, bool IsSystem);
    private sealed record BillListItem(Guid Id, string BillNo, Guid? OrganizationId, Guid? CreatedBy);
    private sealed record BillDetail(Guid Id, Guid? CreatedBy, Guid? OrganizationId);
    private sealed record CostListItem(Guid Id, Guid? OrganizationId, Guid? CreatedBy);
    private sealed record OrgDto(Guid Id, string Code, string Name, Guid? ParentId);
    private sealed record OrgTreeNode(Guid Id, string Code, IReadOnlyList<OrgTreeNode> Children);
    private sealed record PartyRoleDto(Guid Id, Guid PartyId, string RoleCode, bool IsActive);
    private sealed record CurrencyResponse(Guid Id, string Code, string Name, int DecimalPlaces, bool IsActive);
    private sealed record DevTokenResponse(string AccessToken);
    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
