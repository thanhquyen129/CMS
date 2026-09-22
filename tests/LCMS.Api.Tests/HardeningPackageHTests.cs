using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class HardeningPackageHTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public HardeningPackageHTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task PolicyCatalog_SeedsThirteenKeys_AndVersions()
    {
        var tenantId = await CreateTenantAsync();
        using (var ensure = Tenant(HttpMethod.Post, "/api/policies/ensure-catalog", tenantId))
        {
            (await _client.SendAsync(ensure)).EnsureSuccessStatusCode();
        }

        using var list = Tenant(HttpMethod.Get, "/api/policies?latestOnly=true", tenantId);
        var res = await _client.SendAsync(list);
        res.EnsureSuccessStatusCode();
        var rows = await res.Content.ReadFromJsonAsync<List<PolicyRow>>(Json);
        Assert.NotNull(rows);
        Assert.Equal(13, rows.Count);
        Assert.Contains(rows, r => r.PolicyKey == "RECON_TOLERANCE_POLICY");
        Assert.Contains(rows, r => r.PolicyKey == "AP_AR_RECOGNITION_POLICY");

        using var upsert = Tenant(HttpMethod.Put, "/api/policies", tenantId);
        upsert.Content = JsonContent.Create(new
        {
            policyKey = "AGING_POLICY",
            title = "Tuổi nợ v2",
            effectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            status = "active",
            bodyJson = """{"agingBucket1Days":30}""",
            notes = "H package",
            createNewVersion = true
        });
        (await _client.SendAsync(upsert)).EnsureSuccessStatusCode();

        using var active = Tenant(HttpMethod.Get, "/api/policies/AGING_POLICY/active", tenantId);
        var act = await _client.SendAsync(active);
        act.EnsureSuccessStatusCode();
        var policy = await act.Content.ReadFromJsonAsync<PolicyRow>(Json);
        Assert.Equal(2, policy!.Version);
        Assert.Equal("active", policy.Status);
    }

    [Fact]
    public async Task CostCreate_IdempotencyKey_ReturnsSameId()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-H-IDEM");
        const string key = "cost-h-1";

        Guid first;
        using (var req = Tenant(HttpMethod.Post, "/api/costs", tenantId))
        {
            req.Headers.TryAddWithoutValidation("Idempotency-Key", key);
            req.Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 100m,
                currencyCode = "VND"
            });
            var res = await _client.SendAsync(req);
            res.EnsureSuccessStatusCode();
            first = (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        }

        using (var again = Tenant(HttpMethod.Post, "/api/costs", tenantId))
        {
            again.Headers.TryAddWithoutValidation("Idempotency-Key", key);
            again.Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 999m,
                currencyCode = "VND"
            });
            var res = await _client.SendAsync(again);
            res.EnsureSuccessStatusCode();
            var second = (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
            Assert.Equal(first, second);
        }
    }

    [Fact]
    public async Task IntegrationError_RedactsBearerToken_AndJobHealthWorks()
    {
        var tenantId = await CreateTenantAsync();
        Guid recordId;
        using (var upsert = Tenant(HttpMethod.Post, "/api/integration-records", tenantId))
        {
            upsert.Content = JsonContent.Create(new
            {
                sourceSystem = "tms-h",
                externalObjectType = "bill",
                externalId = "EXT-H-1",
                status = "received"
            });
            var res = await _client.SendAsync(upsert);
            res.EnsureSuccessStatusCode();
            recordId = (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        }

        using (var err = Tenant(HttpMethod.Post, "/api/integration-errors", tenantId))
        {
            err.Content = JsonContent.Create(new
            {
                integrationRecordId = recordId,
                errorCode = "AUTH_FAILED",
                message = "upstream said Bearer abc.def.ghi failed",
                detail = "password=super-secret; Authorization=Bearer xyz"
            });
            (await _client.SendAsync(err)).EnsureSuccessStatusCode();
        }

        using var list = Tenant(HttpMethod.Get, "/api/integration-errors?recoveryStatus=pending", tenantId);
        var listed = await _client.SendAsync(list);
        listed.EnsureSuccessStatusCode();
        var errors = await listed.Content.ReadFromJsonAsync<List<ErrRow>>(Json);
        var row = Assert.Single(errors!);
        Assert.DoesNotContain("abc.def.ghi", row.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REDACTED]", row.Message);
        Assert.DoesNotContain("super-secret", row.Detail ?? "", StringComparison.OrdinalIgnoreCase);

        using var health = Tenant(HttpMethod.Get, "/api/integration-errors/job-health", tenantId);
        var hRes = await _client.SendAsync(health);
        if (!hRes.IsSuccessStatusCode)
        {
            var body = await hRes.Content.ReadAsStringAsync();
            Assert.Fail($"{(int)hRes.StatusCode}: {body}");
        }
        var healthBody = await hRes.Content.ReadFromJsonAsync<HealthDto>(Json);
        Assert.True(healthBody!.IntegrationErrorsPending >= 1);
        Assert.NotEmpty(healthBody.TopActionableErrors);
        Assert.Contains("credential", healthBody.TopActionableErrors[0].NextAction ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid> CreateTenantAsync()
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tenants");
        req.Content = JsonContent.Create(new { code = $"H-{Guid.NewGuid():N}"[..12], name = "Pkg H" });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
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

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record PolicyRow(string PolicyKey, int Version, string Status);
    private sealed record ErrRow(string Message, string? Detail);
    private sealed record HealthDto(
        int IntegrationErrorsPending,
        List<Actionable> TopActionableErrors);
    private sealed record Actionable(string? NextAction);
}
