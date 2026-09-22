using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class ReferenceMasterTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public ReferenceMasterTests(LcmsApiFactory factory)
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
    public async Task Location_AliasResolves_AndRouteRejectsSameEndpoints()
    {
        var tenantId = await CreateTenantAsync();
        var sgn = await PutLocationAsync(tenantId, "SGN", "Tân Sơn Nhất", "airport", "VN", "SGN", "VNSGN", ["SGN-OLD"]);
        var lax = await PutLocationAsync(tenantId, "LAX", "Los Angeles", "airport", "US", "LAX", "USLAX", []);

        using var resolve = new HttpRequestMessage(HttpMethod.Get, "/api/locations/resolve?code=SGN-OLD");
        resolve.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var resolved = await _client.SendAsync(resolve);
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var location = await resolved.Content.ReadFromJsonAsync<LocationBody>(Json);
        Assert.Equal(sgn, location!.Id);
        Assert.Equal("SGN", location.Code);

        using var badRoute = new HttpRequestMessage(HttpMethod.Put, "/api/routes")
        {
            Content = JsonContent.Create(new
            {
                code = "BAD",
                name = "Trùng điểm",
                originLocationId = sgn,
                destinationLocationId = sgn,
                isActive = true
            })
        };
        badRoute.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(badRoute)).StatusCode);

        using var route = new HttpRequestMessage(HttpMethod.Put, "/api/routes")
        {
            Content = JsonContent.Create(new
            {
                code = "SGN-LAX",
                name = "SGN đi LAX",
                originLocationId = sgn,
                destinationLocationId = lax,
                transportModeCode = "air",
                isActive = true,
                intermediateLocationIds = Array.Empty<Guid>()
            })
        };
        route.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(route)).StatusCode);
    }

    [Fact]
    public async Task Commodity_StoresRatingFlags()
    {
        var tenantId = await CreateTenantAsync();
        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/commodities")
        {
            Content = JsonContent.Create(new
            {
                code = "DG-COLD",
                name = "Hàng lạnh nguy hiểm",
                category = "Đặc biệt",
                isDangerousGoods = true,
                isTemperatureControlled = true,
                isActive = true
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(req)).StatusCode);

        using var list = new HttpRequestMessage(HttpMethod.Get, "/api/commodities");
        list.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var rows = await (await _client.SendAsync(list)).Content.ReadFromJsonAsync<List<CommodityBody>>(Json);
        Assert.Contains(rows!, c => c.Code == "DG-COLD" && c.IsDangerousGoods && c.IsTemperatureControlled);
    }

    [Fact]
    public async Task PartySnapshot_DoesNotChange_WhenMasterNameChanges()
    {
        var tenantId = await CreateTenantAsync();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/business-parties")
        {
            Content = JsonContent.Create(new { code = "KH-1", name = "Công ty A", roleCodes = new[] { "customer", "shipper" } })
        };
        create.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var created = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var party = await created.Content.ReadFromJsonAsync<IdBody>(Json);

        using var bill = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new
            {
                billNo = "BL-SNAP",
                billType = "house",
                customerPartyId = party!.Id,
                shipperPartyId = party.Id
            })
        };
        bill.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var billRes = await _client.SendAsync(bill);
        Assert.Equal(HttpStatusCode.Created, billRes.StatusCode);
        var billId = (await billRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        using var rename = new HttpRequestMessage(HttpMethod.Put, $"/api/business-parties/{party.Id}")
        {
            Content = JsonContent.Create(new { name = "Công ty B", isActive = true })
        };
        rename.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(rename)).StatusCode);

        using var snaps = new HttpRequestMessage(HttpMethod.Get, $"/api/party-snapshots?objectType=bill&objectId={billId}&currentOnly=true");
        snaps.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var snapRes = await _client.SendAsync(snaps);
        var snapBody = await snapRes.Content.ReadAsStringAsync();
        Assert.True(snapRes.StatusCode == HttpStatusCode.OK, snapBody);
        var rows = JsonSerializer.Deserialize<List<SnapshotBody>>(snapBody, Json);
        Assert.Contains(rows!, s => s.RoleCode == "customer" && s.DisplayName == "Công ty A");
        Assert.Contains(rows!, s => s.RoleCode == "shipper" && s.DisplayName == "Công ty A");
    }

    [Fact]
    public async Task BillContext_RejectsUnknownPlace_WhenLocationsExist()
    {
        var tenantId = await CreateTenantAsync();
        using var open = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "BL-FREE", billType = "house" })
        };
        open.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var openId = (await (await _client.SendAsync(open)).Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var free = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{openId}/context")
        {
            Content = JsonContent.Create(new { originCode = "FREE", destinationCode = "TEXT" })
        };
        free.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(free)).StatusCode);

        await PutLocationAsync(tenantId, "SGN", "Tân Sơn Nhất", "airport", "VN", "SGN", "VNSGN", []);
        using var bill = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "BL-LOC", billType = "house" })
        };
        bill.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var billId = (await (await _client.SendAsync(bill)).Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        using var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/bills/{billId}/context")
        {
            Content = JsonContent.Create(new { originCode = "NOPE", destinationCode = "SGN" })
        };
        patch.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    private async Task<Guid> PutLocationAsync(
        Guid tenantId,
        string code,
        string name,
        string type,
        string country,
        string iata,
        string unlocode,
        string[] aliases)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/locations")
        {
            Content = JsonContent.Create(new
            {
                code,
                name,
                locationType = type,
                countryCode = country,
                iataCode = iata,
                unlocode,
                isActive = true,
                aliases = aliases.Select(a => new { aliasCode = a, sourceSystem = "legacy" })
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Reference" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private sealed record IdBody(Guid Id);
    private sealed record LocationBody(Guid Id, string Code);
    private sealed record CommodityBody(string Code, bool IsDangerousGoods, bool IsTemperatureControlled);
    private sealed record SnapshotBody(string RoleCode, string DisplayName);
}
