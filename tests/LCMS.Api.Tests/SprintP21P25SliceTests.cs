using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Api.Auth;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class SprintP21P25SliceTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public SprintP21P25SliceTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Auth_Refresh_Rotates_And_Logout_Revokes()
    {
        var tenantId = await CreateTenantAsync("TN-P22", "P22 Auth");
        var email = $"p22-{Guid.NewGuid():N}@example.com";
        const string password = "Passw0rd!";
        await SeedUserWithPasswordAsync(tenantId, email, password);

        var loginRes = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        loginRes.EnsureSuccessStatusCode();
        var tokens = await loginRes.Content.ReadFromJsonAsync<TokenPairDto>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));

        var refreshRes = await _client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        refreshRes.EnsureSuccessStatusCode();
        var rotated = await refreshRes.Content.ReadFromJsonAsync<TokenPairDto>(JsonOptions);
        Assert.NotEqual(tokens.RefreshToken, rotated!.RefreshToken);

        var reuseRes = await _client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseRes.StatusCode);

        (await _client.PostAsJsonAsync(
            "/api/auth/logout", new { refreshToken = rotated.RefreshToken })).EnsureSuccessStatusCode();

        var afterLogout = await _client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task FineGrained_ConfirmCost_Denied_WithoutPermission()
    {
        var tenantId = await CreateTenantAsync("TN-P24", "P24 Perm");
        var roleId = await CreateRoleWithPermissionAsync(tenantId, "ReaderOnly", "cost.read", "all");
        var userId = await CreateUserAsync(tenantId, "p24-reader@example.com", "Reader");
        await AssignUserRoleAsync(tenantId, userId, roleId);

        var billId = await CreateBillAsync(tenantId, "BL-P24");
        var costId = await CreateCostAsync(tenantId, billId, 100m);

        using var confirm = WithActor(
            HttpMethod.Post, $"/api/costs/{costId}/confirm", tenantId, userId);
        confirm.Content = JsonContent.Create(new { confirmedAmount = 100m });
        var res = await _client.SendAsync(confirm);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private async Task SeedUserWithPasswordAsync(Guid tenantId, string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
        var user = new User
        {
            TenantId = tenantId,
            Email = email.ToLowerInvariant(),
            DisplayName = "P22 User",
            IsActive = true
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
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
        await AssignPermissionAsync(tenantId, roleId, actionCode, dataScope);
        return roleId;
    }

    private async Task AssignPermissionAsync(
        Guid tenantId, Guid roleId, string actionCode, string dataScope)
    {
        using var req = WithTenant(HttpMethod.Post, $"/api/roles/{roleId}/permissions", tenantId);
        req.Content = JsonContent.Create(new { actionCode, dataScope });
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    private async Task AssignUserRoleAsync(Guid tenantId, Guid userId, Guid roleId)
    {
        using var req = WithTenant(
            HttpMethod.Post, $"/api/users/{userId}/roles/{roleId}", tenantId);
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType = "freight" });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateCostAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/costs", tenantId);
        req.Content = JsonContent.Create(new
        {
            billId,
            attributionType = "direct",
            amount,
            currencyCode = "VND",
            costTypeCode = "freight"
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

    private static HttpRequestMessage WithActor(HttpMethod method, string url, Guid tenantId, Guid userId)
    {
        var req = WithTenant(method, url, tenantId);
        req.Headers.Add("X-User-Id", userId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record TokenPairDto(string AccessToken, string RefreshToken);
}
