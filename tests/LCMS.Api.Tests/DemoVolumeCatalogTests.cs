using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.Demo;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class DemoVolumeCatalogTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public DemoVolumeCatalogTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task SeedDemo_CreatesAtLeast120PerListType_AndIsIdempotent()
    {
        var first = await _client.PostAsJsonAsync("/api/dev/seed-demo", new { });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<SeedResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.TenantId);
        Assert.NotNull(body.Counts);
        foreach (var (key, count) in body.Counts)
        {
            Assert.True(
                count >= DemoVolumeCatalogSeeder.TargetCount,
                $"{key}={count} < {DemoVolumeCatalogSeeder.TargetCount}");
        }

        using var listBills = WithTenant(HttpMethod.Get, "/api/bills", body.TenantId);
        var billsRes = await _client.SendAsync(listBills);
        Assert.Equal(HttpStatusCode.OK, billsRes.StatusCode);

        using var listOrders = WithTenant(HttpMethod.Get, "/api/orders", body.TenantId);
        var orders = await (await _client.SendAsync(listOrders))
            .Content.ReadFromJsonAsync<List<IdRow>>(JsonOptions);
        Assert.True(orders!.Count >= DemoVolumeCatalogSeeder.TargetCount);

        var second = await _client.PostAsJsonAsync("/api/dev/seed-demo", new { });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var again = await second.Content.ReadFromJsonAsync<SeedResponse>(JsonOptions);
        Assert.Equal(body.Counts!["bill"], again!.Counts!["bill"]);
        Assert.Equal(body.Counts["order"], again.Counts["order"]);
        Assert.True(again.Skipped);
    }

    [Fact]
    public async Task SampleDataApi_UsesCurrentTenant()
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(new { code = "TN-VOL-CUR", name = "Volume tenant" })
        };
        var created = await _client.SendAsync(create);
        created.EnsureSuccessStatusCode();
        var tenant = await created.Content.ReadFromJsonAsync<IdRow>(JsonOptions);

        using var ensure = WithTenant(HttpMethod.Post, "/api/sample-data/ensure", tenant!.Id);
        var res = await _client.SendAsync(ensure);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<SeedResponse>(JsonOptions);
        Assert.Equal(tenant.Id, body!.TenantId);
        Assert.True(body.Counts!["customer"] >= DemoVolumeCatalogSeeder.TargetCount);
        Assert.True(body.Counts["document"] >= DemoVolumeCatalogSeeder.TargetCount);

        using var statusReq = WithTenant(HttpMethod.Get, "/api/sample-data", tenant.Id);
        var statusRes = await _client.SendAsync(statusReq);
        Assert.Equal(HttpStatusCode.OK, statusRes.StatusCode);
        var status = await statusRes.Content.ReadFromJsonAsync<StatusResponse>(JsonOptions);
        Assert.True(status!.Complete);
        Assert.Equal(DemoVolumeCatalogSeeder.TargetCount, status.TargetCount);
    }

    private static HttpRequestMessage WithTenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record SeedResponse(
        Guid TenantId,
        bool Skipped,
        string Summary,
        Dictionary<string, int>? Counts);

    private sealed record StatusResponse(Guid TenantId, int TargetCount, Dictionary<string, int> Counts, bool Complete);

    private sealed record IdRow(Guid Id);
}
