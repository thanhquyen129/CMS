using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class SystemRoleCatalogAccessTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public SystemRoleCatalogAccessTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task CreateTenant_SeedsSevenSystemRoles()
    {
        var tenantId = await CreateTenantAsync("TN-RBAC7", "RBAC Seven");

        using var req = WithTenant(HttpMethod.Get, "/api/roles", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var roles = await res.Content.ReadFromJsonAsync<List<RoleDto>>(JsonOptions);

        Assert.NotNull(roles);
        Assert.Contains(roles!, r => r.Code == "Admin" && r.IsSystem);
        Assert.Contains(roles!, r => r.Code == "FinancialController" && r.IsSystem);
        Assert.Contains(roles!, r => r.Code == "CostAccountant" && r.IsSystem);
        Assert.Contains(roles!, r => r.Code == "RevenueAccountant" && r.IsSystem);
        Assert.Contains(roles!, r => r.Code == "Ops" && r.IsSystem);
        Assert.Contains(roles!, r => r.Code == "MasterData" && r.IsSystem);
        Assert.Contains(roles!, r => r.Code == "Viewer" && r.IsSystem);
    }

    [Fact]
    public async Task CostAccountant_HasNoRevenueRead_InDefaultMatrix()
    {
        var tenantId = await CreateTenantAsync("TN-RBAC-COST", "RBAC Cost");
        var costRole = await GetRoleAsync(tenantId, "CostAccountant");

        using var req = WithTenant(
            HttpMethod.Get, $"/api/roles/{costRole.Id}/permission-matrix", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var matrix = await res.Content.ReadFromJsonAsync<List<MatrixItem>>(JsonOptions);

        Assert.NotNull(matrix);
        Assert.Contains(matrix!, m => m.ActionCode == "cost.read" && m.Enabled);
        Assert.Contains(matrix!, m => m.ActionCode == "revenue.read" && !m.Enabled);
    }

    [Fact]
    public async Task Admin_CanToggleOpsPermission_OnAndOff()
    {
        var tenantId = await CreateTenantAsync("TN-RBAC-TOG", "RBAC Toggle");
        var ops = await GetRoleAsync(tenantId, "Ops");

        using var off = WithTenant(HttpMethod.Put, $"/api/roles/{ops.Id}/permissions", tenantId);
        off.Content = JsonContent.Create(new { actionCode = "cost.read", enabled = false });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(off)).StatusCode);

        using var matrixOffReq = WithTenant(
            HttpMethod.Get, $"/api/roles/{ops.Id}/permission-matrix", tenantId);
        var matrixOff = await (await _client.SendAsync(matrixOffReq)).Content
            .ReadFromJsonAsync<List<MatrixItem>>(JsonOptions);
        Assert.Contains(matrixOff!, m => m.ActionCode == "cost.read" && !m.Enabled);

        using var on = WithTenant(HttpMethod.Put, $"/api/roles/{ops.Id}/permissions", tenantId);
        on.Content = JsonContent.Create(new
        {
            actionCode = "cost.read",
            enabled = true,
            dataScope = "organization"
        });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(on)).StatusCode);

        using var matrixOnReq = WithTenant(
            HttpMethod.Get, $"/api/roles/{ops.Id}/permission-matrix", tenantId);
        var matrixOn = await (await _client.SendAsync(matrixOnReq)).Content
            .ReadFromJsonAsync<List<MatrixItem>>(JsonOptions);
        Assert.Contains(matrixOn!, m =>
            m.ActionCode == "cost.read" && m.Enabled && m.DataScope == "organization");
    }

    [Fact]
    public async Task CannotDisableAdminRoleManage()
    {
        var tenantId = await CreateTenantAsync("TN-RBAC-LOCK", "RBAC Lock");
        var admin = await GetRoleAsync(tenantId, "Admin");

        using var req = WithTenant(HttpMethod.Put, $"/api/roles/{admin.Id}/permissions", tenantId);
        req.Content = JsonContent.Create(new { actionCode = "role.manage", enabled = false });
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task ActorWithoutRoleManage_Denied()
    {
        var tenantId = await CreateTenantAsync("TN-RBAC-DENY", "RBAC Deny");
        var roleId = await CreateRoleWithPermissionAsync(tenantId, "NoManage", "bill.read", "all");
        var userId = await CreateUserAsync(tenantId, "nomgr@example.com", "No Mgr");
        await AssignUserRoleAsync(tenantId, userId, roleId);

        using var req = WithActor(HttpMethod.Get, "/api/roles", tenantId, userId);
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<RoleDto> GetRoleAsync(Guid tenantId, string code)
    {
        using var req = WithTenant(HttpMethod.Get, "/api/roles", tenantId);
        var roles = await (await _client.SendAsync(req)).Content
            .ReadFromJsonAsync<List<RoleDto>>(JsonOptions);
        return Assert.Single(roles!, r => r.Code == code);
    }

    private async Task<Guid> CreateUserAsync(Guid tenantId, string email, string displayName)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/users", tenantId);
        req.Content = JsonContent.Create(new { email, displayName });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateRoleWithPermissionAsync(
        Guid tenantId, string code, string actionCode, string dataScope)
    {
        using var roleReq = WithTenant(HttpMethod.Post, "/api/roles", tenantId);
        roleReq.Content = JsonContent.Create(new { code, name = code });
        var roleRes = await _client.SendAsync(roleReq);
        roleRes.EnsureSuccessStatusCode();
        var roleId = (await roleRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var perm = WithTenant(HttpMethod.Post, $"/api/roles/{roleId}/permissions", tenantId);
        perm.Content = JsonContent.Create(new { actionCode, dataScope });
        (await _client.SendAsync(perm)).EnsureSuccessStatusCode();
        return roleId;
    }

    private async Task AssignUserRoleAsync(Guid tenantId, Guid userId, Guid roleId)
    {
        using var req = WithTenant(
            HttpMethod.Post, $"/api/users/{userId}/roles/{roleId}", tenantId);
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage WithTenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private static HttpRequestMessage WithActor(
        HttpMethod method, string url, Guid tenantId, Guid userId)
    {
        var req = WithTenant(method, url, tenantId);
        req.Headers.Add("X-User-Id", userId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record RoleDto(Guid Id, string Code, string Name, bool IsSystem, string? SummaryVi);
    private sealed record MatrixItem(string ActionCode, string PermissionName, bool Enabled, string? DataScope, bool Locked);
}
