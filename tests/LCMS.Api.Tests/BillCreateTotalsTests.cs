using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class BillCreateTotalsTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public BillCreateTotalsTests(LcmsApiFactory factory) => _factory = factory;

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
    public void Bill_create_screen_keeps_totals_and_hides_piece_grid()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "apps", "web", "components", "CreateBillWorkspaceForm.tsx"));
        Assert.Contains("packageCount", src);
        Assert.Contains("grossWeightKg", src);
        Assert.Contains("volumeCbm", src);
        Assert.DoesNotContain("CreateCargoLineGrid", src);
        Assert.DoesNotContain("submitCargoLines", src);
    }

    [Fact]
    public async Task Air_bill_saves_without_package_lines_and_keeps_entered_totals()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId);
        using (var patch = Tenant(HttpMethod.Patch, $"/api/bills/{billId}/context", tenantId))
        {
            patch.Content = JsonContent.Create(new
            {
                transportMode = "air",
                context = new { grossWeightKg = 12.5m, volumeCbm = 0.2m }
            });
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(patch)).StatusCode);
        }

        var bill = await GetAsync<BillBody>(tenantId, $"/api/bills/{billId}");
        Assert.Equal("air", bill.TransportMode);
        Assert.NotNull(bill.Context);
        Assert.Null(bill.Context!.PackageCount);
        Assert.Null(bill.Context.ContainerCount);
        Assert.Equal(12.5m, bill.Context.GrossWeightKg);
        Assert.Equal(0.2m, bill.Context.VolumeCbm);

        using var waybill = Tenant(HttpMethod.Put, $"/api/bills/{billId}/waybill", tenantId);
        waybill.Content = JsonContent.Create(new
        {
            sender = new { name = "Người gửi" },
            consignee = new { name = "Người nhận" }
        });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(waybill)).StatusCode);
        var saved = await GetAsync<WaybillBody>(tenantId, $"/api/bills/{billId}/waybill");
        Assert.Equal(0, saved.ParcelCount);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cms.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Bill totals" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId)
    {
        using var req = Tenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo = "BL-" + Guid.NewGuid().ToString("N")[..8], billType = "house" });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<T> GetAsync<T>(Guid tenantId, string path)
    {
        using var req = Tenant(HttpMethod.Get, path, tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string path, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record BillBody(string? TransportMode, ContextBody? Context);
    private sealed record ContextBody(int? PackageCount, int? ContainerCount, decimal? GrossWeightKg, decimal? VolumeCbm);
    private sealed record WaybillBody(int ParcelCount);
}
