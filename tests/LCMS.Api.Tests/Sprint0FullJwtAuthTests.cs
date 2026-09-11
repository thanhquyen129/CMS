using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using LCMS.Api.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace LCMS.Api.Tests;

/// <summary>Sprint 0 FULL — JWT path + Production RequireJwt 401.</summary>
public sealed class Sprint0FullJwtAuthTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task DevToken_ThenBearer_CreatesBill_WithoutTenantHeader()
    {
        await using var factory = new LcmsApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var tenantId = await CreateTenantAsync(client, "TN-JWT", "JWT Tenant");
        var userId = await CreateAdminUserAsync(client, tenantId, useBearer: false, bearer: null, "jwt.dev@example.com");

        var tokenRes = await client.PostAsJsonAsync("/api/dev/token", new { tenantId, userId });
        Assert.Equal(HttpStatusCode.OK, tokenRes.StatusCode);
        var tokenBody = await tokenRes.Content.ReadFromJsonAsync<DevTokenResponse>(JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(tokenBody?.AccessToken));

        using var createBill = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "JWT-BILL-1", billType = "freight" })
        };
        createBill.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenBody!.AccessToken);
        var created = await client.SendAsync(createBill);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var metrics = await client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, metrics.StatusCode);
        var metricsText = await metrics.Content.ReadAsStringAsync();
        Assert.Contains("# TYPE", metricsText, StringComparison.Ordinal);
        Assert.Contains("http_", metricsText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JwtPath_CostCreate_WritesAudit_WithActorAndCorrelation()
    {
        await using var factory = new LcmsApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var tenantId = await CreateTenantAsync(client, "TN-JWT-AUD", "JWT Audit Tenant");
        var userId = await CreateAdminUserAsync(client, tenantId, useBearer: false, bearer: null, "jwt.audit@example.com");
        var tokenRes = await client.PostAsJsonAsync("/api/dev/token", new { tenantId, userId });
        var token = (await tokenRes.Content.ReadFromJsonAsync<DevTokenResponse>(JsonOptions))!.AccessToken;
        var correlationId = $"jwt-corr-{Guid.NewGuid():N}";

        using var billReq = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "JWT-AUD-B1", billType = "freight" })
        };
        billReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        billReq.Headers.Add("X-Correlation-Id", correlationId);
        var billRes = await client.SendAsync(billReq);
        Assert.Equal(HttpStatusCode.Created, billRes.StatusCode);
        var bill = await billRes.Content.ReadFromJsonAsync<IdBody>(JsonOptions);

        using var costReq = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId = bill!.Id,
                attributionType = "direct",
                amount = 500m,
                currencyCode = "VND",
                costTypeCode = "FREIGHT"
            })
        };
        costReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        costReq.Headers.Add("X-Correlation-Id", correlationId);
        var costRes = await client.SendAsync(costReq);
        Assert.Equal(HttpStatusCode.Created, costRes.StatusCode);

        using var auditReq = new HttpRequestMessage(HttpMethod.Get, $"/api/audit-events?action=cost.create&correlationId={correlationId}");
        auditReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var auditRes = await client.SendAsync(auditReq);
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);
        var events = await auditRes.Content.ReadFromJsonAsync<List<AuditBody>>(JsonOptions);
        var evt = Assert.Single(events!);
        Assert.Equal(userId, evt.ActorId);
        Assert.Equal(correlationId, evt.CorrelationId);
        Assert.Equal("cost.create", evt.Action);
    }

    [Fact]
    public async Task Production_WithoutToken_ProtectedApi_Returns401_Vietnamese()
    {
        await using var factory = new ProductionLcmsApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/tenants", new { code = "TN-P", name = "Prod" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(JsonOptions);
        Assert.Equal("unauthorized", body!.Code);
        Assert.Contains("xác thực", body.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(body.CorrelationId));
    }

    [Fact]
    public async Task Production_WithValidJwt_AllowsProtectedApi_HeaderBootstrapDisabled()
    {
        await using var factory = new ProductionLcmsApiFactory();
        await factory.InitializeDatabaseAsync();
        using var client = factory.CreateClient();

        var bootstrapToken = IssueTestJwt(
            tenantId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            userId: Guid.Parse("33333333-3333-3333-3333-333333333333"));

        using var createTenant = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(new { code = "TN-JWT-P", name = "Prod JWT" })
        };
        createTenant.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bootstrapToken);
        var tenantRes = await client.SendAsync(createTenant);
        Assert.Equal(HttpStatusCode.Created, tenantRes.StatusCode);
        var tenant = await tenantRes.Content.ReadFromJsonAsync<IdBody>(JsonOptions);
        Assert.NotNull(tenant);

        var tenantScopedToken = IssueTestJwt(tenant!.Id, Guid.Parse("33333333-3333-3333-3333-333333333333"));
        var userId = await CreateAdminUserAsync(client, tenant.Id, useBearer: true, bearer: tenantScopedToken, "jwt.prod@example.com");

        var billToken = IssueTestJwt(tenant.Id, userId);
        using var createBill = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "PROD-JWT-1", billType = "freight" })
        };
        createBill.Headers.Authorization = new AuthenticationHeaderValue("Bearer", billToken);
        createBill.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());
        var billRes = await client.SendAsync(createBill);
        Assert.Equal(HttpStatusCode.Created, billRes.StatusCode);

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private static async Task<Guid> CreateTenantAsync(HttpClient client, string code, string name)
    {
        var res = await client.PostAsJsonAsync("/api/tenants", new { code, name });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdBody>(JsonOptions);
        return body!.Id;
    }

    private static async Task<Guid> CreateAdminUserAsync(
        HttpClient client,
        Guid tenantId,
        bool useBearer,
        string? bearer,
        string email)
    {
        using var createUser = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email, displayName = "JWT Admin" })
        };
        ApplyTenantAuth(createUser, tenantId, useBearer, bearer);
        var userRes = await client.SendAsync(createUser);
        userRes.EnsureSuccessStatusCode();
        var user = await userRes.Content.ReadFromJsonAsync<IdBody>(JsonOptions);

        using var listRoles = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        ApplyTenantAuth(listRoles, tenantId, useBearer, bearer);
        var roles = await (await client.SendAsync(listRoles)).Content
            .ReadFromJsonAsync<List<RoleBody>>(JsonOptions);
        var adminRole = Assert.Single(roles!, r => r.Code == "Admin");

        using var assign = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/users/{user!.Id}/roles/{adminRole.Id}");
        ApplyTenantAuth(assign, tenantId, useBearer, bearer);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(assign)).StatusCode);

        return user.Id;
    }

    private static void ApplyTenantAuth(
        HttpRequestMessage req,
        Guid tenantId,
        bool useBearer,
        string? bearer)
    {
        if (useBearer)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        else
        {
            req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        }
    }

    private static string IssueTestJwt(Guid tenantId, Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthServiceCollectionExtensions.DevFallbackSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "lcms-api",
            audience: "lcms-api",
            claims:
            [
                new Claim("sub", userId.ToString("D")),
                new Claim(JwtClaimNames.TenantId, tenantId.ToString("D"))
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record DevTokenResponse(string AccessToken, string TokenType, int ExpiresIn);
    private sealed record IdBody(Guid Id);
    private sealed record RoleBody(Guid Id, string Code, string Name, bool IsSystem);
    private sealed record ErrorBody(string CorrelationId, string Code, string Message);
    private sealed record AuditBody(Guid Id, Guid? ActorId, string Action, string ObjectType, Guid ObjectId, string? CorrelationId);
}

/// <summary>Production-like host: RequireJwt, no header bootstrap.</summary>
public sealed class ProductionLcmsApiFactory : LcmsApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Production");
        builder.UseSetting("Auth:RequireJwt", "true");
        builder.UseSetting("Auth:AllowHeaderBootstrap", "false");
        builder.UseSetting("Auth:Jwt:Issuer", "lcms-api");
        builder.UseSetting("Auth:Jwt:Audience", "lcms-api");
        builder.UseSetting("Auth:Jwt:SigningKey", AuthServiceCollectionExtensions.DevFallbackSigningKey);
        builder.UseSetting("Database:MigrateOnStartup", "false");
    }
}
