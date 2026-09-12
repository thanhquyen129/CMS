using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class SprintP07AgingDashboardPermissionTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public SprintP07AgingDashboardPermissionTests(LcmsApiFactory factory)
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
    public async Task Dashboard_HidesRevenueAndMargin_WhenOnlyCostRead()
    {
        var tenantId = await CreateTenantAsync("TN-P07-DASH", "P07 dash");
        var orgId = await CreateOrgAsync(tenantId, "HQ", "HQ", null);
        var userId = await CreateUserAsync(tenantId, "cost-only@example.com", "Cost Only", orgId);

        var role = await CreateRoleWithPermissionAsync(tenantId, "CostViewer", "cost.read", "all");
        await AssignPermissionAsync(tenantId, role, "bill.read", "all");
        await AssignPermissionAsync(tenantId, role, "bill.create", "all");
        await AssignPermissionAsync(tenantId, role, "cost.create", "all");
        await AssignUserRoleAsync(tenantId, userId, role);

        var billId = await CreateBillAsync(tenantId, userId, "BL-P07", orgId);
        await CreateCostAsync(tenantId, userId, billId, 100m, orgId);
        await CreateRevenueAsync(tenantId, billId, 500m);

        using var req = WithUser(HttpMethod.Get, "/api/dashboard/summary", tenantId, userId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var summary = await res.Content.ReadFromJsonAsync<DashboardDto>(JsonOptions);
        Assert.NotNull(summary);
        Assert.NotNull(summary!.FinancialVisibility);
        Assert.True(summary.FinancialVisibility.CanViewCost);
        Assert.False(summary.FinancialVisibility.CanViewRevenue);
        Assert.False(summary.FinancialVisibility.CanViewMargin);

        var row = Assert.Single(summary.TotalsByCurrency);
        Assert.Equal(100m, row.CostBestAvailable);
        Assert.Null(row.RevenueBestAvailable);
        Assert.Null(row.ProfitBestAvailable);
    }

    [Fact]
    public async Task AgingSummary_RespectsCostVsRevenuePermission()
    {
        var tenantId = await CreateTenantAsync("TN-P07-AGE", "P07 age");
        var orgId = await CreateOrgAsync(tenantId, "HQ", "HQ", null);
        var userId = await CreateUserAsync(tenantId, "rev-only@example.com", "Rev Only", orgId);

        var role = await CreateRoleWithPermissionAsync(tenantId, "RevViewer", "revenue.read", "all");
        await AssignPermissionAsync(tenantId, role, "bill.read", "all");
        await AssignUserRoleAsync(tenantId, userId, role);

        using var summaryReq = WithUser(HttpMethod.Get, "/api/aging/summary", tenantId, userId);
        var summaryRes = await _client.SendAsync(summaryReq);
        summaryRes.EnsureSuccessStatusCode();
        var summary = await summaryRes.Content.ReadFromJsonAsync<AgingSummaryDto>(JsonOptions);
        Assert.NotNull(summary);
        Assert.False(summary!.CanViewPayable);
        Assert.True(summary.CanViewReceivable);
        Assert.Null(summary.Payable);
        Assert.NotNull(summary.Receivable);

        using var apAging = WithUser(HttpMethod.Get, "/api/accounts-payable/aging", tenantId, userId);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(apAging)).StatusCode);

        using var arAging = WithUser(HttpMethod.Get, "/api/accounts-receivable/aging", tenantId, userId);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(arAging)).StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateOrgAsync(Guid tenantId, string code, string name, Guid? parentId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/organizations")
        {
            Content = JsonContent.Create(new { code, name, parentId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateUserAsync(Guid tenantId, string email, string displayName, Guid orgId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email, displayName, organizationId = orgId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
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
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/roles")
        {
            Content = JsonContent.Create(new { code, name = code })
        };
        create.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var created = await _client.SendAsync(create);
        created.EnsureSuccessStatusCode();
        var roleId = (await created.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        await AssignPermissionAsync(tenantId, roleId, actionCode, dataScope);
        return roleId;
    }

    private async Task AssignPermissionAsync(Guid tenantId, Guid roleId, string actionCode, string dataScope)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/roles/{roleId}/permissions")
        {
            Content = JsonContent.Create(new { actionCode, dataScope })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task AssignUserRoleAsync(Guid tenantId, Guid userId, Guid roleId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/roles/{roleId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, Guid userId, string billNo, Guid orgId)
    {
        using var req = WithUser(HttpMethod.Post, "/api/bills", tenantId, userId);
        req.Content = JsonContent.Create(new { billNo, billType = "freight", organizationId = orgId });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task CreateCostAsync(Guid tenantId, Guid userId, Guid billId, decimal amount, Guid orgId)
    {
        using var req = WithUser(HttpMethod.Post, "/api/costs", tenantId, userId);
        req.Content = JsonContent.Create(new
        {
            billId,
            attributionType = "direct",
            amount,
            currencyCode = "VND",
            organizationId = orgId,
            costTypeCode = "P07"
        });
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task CreateRevenueAsync(Guid tenantId, Guid billId, decimal amount)
    {
        // No user header ⇒ permission bootstrap allow create for seed data.
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount,
                currencyCode = "VND",
                revenueTypeCode = "FREIGHT"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private static HttpRequestMessage WithUser(HttpMethod method, string url, Guid tenantId, Guid userId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record FinancialVisibilityDto(bool CanViewCost, bool CanViewRevenue, bool CanViewMargin);

    private sealed record CurrencyTotalsDto(
        string CurrencyCode,
        decimal? CostBestAvailable,
        decimal? RevenueBestAvailable,
        decimal? ProfitBestAvailable);

    private sealed record DashboardDto(
        List<CurrencyTotalsDto> TotalsByCurrency,
        FinancialVisibilityDto FinancialVisibility);

    private sealed record AgingSummaryDto(
        bool CanViewPayable,
        bool CanViewReceivable,
        object? Payable,
        object? Receivable);
}
