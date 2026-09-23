using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Ui02OperationalCreateContextTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Ui02OperationalCreateContextTests(LcmsApiFactory factory)
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
    public async Task UpsertOrder_WithContext_PersistsRouteAndDoesNotWipeOnIdentityUpsert()
    {
        var tenantId = await CreateTenantAsync("TN-UI02-ORD", "UI02 Order Tenant");

        using var create = new HttpRequestMessage(HttpMethod.Put, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                orderNo = "ORD2609CTX",
                sourceSystem = "lcms_manual",
                externalId = "ORD2609CTX",
                operationalStatus = "draft",
                isActive = true,
                transportMode = "air",
                originCode = "SGN",
                destinationCode = "LAX",
                description = "Hàng điện tử",
                applyContext = true
            })
        };
        create.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var created = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var idBody = await created.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);

        using var identityOnly = new HttpRequestMessage(HttpMethod.Put, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                orderNo = "ORD2609CTX",
                sourceSystem = "lcms_manual",
                externalId = "ORD2609CTX",
                operationalStatus = "draft",
                isActive = true
            })
        };
        identityOnly.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(identityOnly)).StatusCode);

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{idBody!.Id}");
        get.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var order = await res.Content.ReadFromJsonAsync<OrderCtxResponse>(JsonOptions);
        Assert.Equal("air", order!.TransportMode);
        Assert.Equal("SGN → LAX", order.RouteCode);
        Assert.Equal("Hàng điện tử", order.Description);
    }

    [Fact]
    public async Task UpsertOrder_WithCustomerAndContext_PersistsOnFirstSave()
    {
        var tenantId = await CreateTenantAsync("TN-UI02-ORDC", "UI02 Order Customer");
        using var partyReq = new HttpRequestMessage(HttpMethod.Post, "/api/business-parties")
        {
            Content = JsonContent.Create(new { code = "CUS-UAT-001", name = "Công ty ABC Electronics", roleCodes = new[] { "customer" } })
        };
        partyReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var partyRes = await _client.SendAsync(partyReq);
        Assert.Equal(HttpStatusCode.Created, partyRes.StatusCode);
        var partyId = (await partyRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var create = new HttpRequestMessage(HttpMethod.Put, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                orderNo = "ORD-UAT-001",
                sourceSystem = "lcms_manual",
                externalId = "ORD-UAT-001",
                operationalStatus = "draft",
                isActive = false,
                applyContext = true,
                customerPartyId = partyId,
                transportMode = "air",
                originCode = "SGN",
                destinationCode = "LAX",
                context = new { serviceType = "door_to_door", incoterm = "FOB", packageCount = 2 }
            })
        };
        create.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var created = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{id}");
        get.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var order = await (await _client.SendAsync(get)).Content.ReadFromJsonAsync<OrderCustomerResponse>(JsonOptions);
        Assert.Equal(partyId, order!.CustomerPartyId);
        Assert.Equal("SGN → LAX", order.RouteCode);
        Assert.Equal(2, order.Context!.PackageCount);
    }

    [Fact]
    public async Task UpsertShipment_WithContextDocument_PersistsOnFirstSave()
    {
        var tenantId = await CreateTenantAsync("TN-UI02-SHPC", "UI02 Shipment Context");
        using var create = new HttpRequestMessage(HttpMethod.Put, "/api/shipments")
        {
            Content = JsonContent.Create(new
            {
                shipmentNo = "SHP-UAT-001",
                sourceSystem = "lcms_manual",
                externalId = "SHP-UAT-001",
                operationalStatus = "active",
                transportMode = "sea",
                originCode = "HPH",
                destinationCode = "RTM",
                applyContext = true,
                context = new { serviceType = "port_to_port", packageCount = 4 }
            })
        };
        create.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var created = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
    }

    [Fact]
    public async Task UpsertShipment_WithContext_ListsBillCountZeroUntilLinked()
    {
        var tenantId = await CreateTenantAsync("TN-UI02-SHP", "UI02 Shipment Tenant");

        using var create = new HttpRequestMessage(HttpMethod.Put, "/api/shipments")
        {
            Content = JsonContent.Create(new
            {
                shipmentNo = "SHP2609CTX",
                sourceSystem = "lcms_manual",
                externalId = "SHP2609CTX",
                operationalStatus = "active",
                transportMode = "sea",
                originCode = "HPH",
                destinationCode = "RTM",
                applyContext = true
            })
        };
        create.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(create)).StatusCode);

        using var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/shipments");
        listReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var listRes = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<List<ShipmentListResponse>>(JsonOptions);
        Assert.NotNull(list);
        var row = Assert.Single(list!);
        Assert.Equal("sea", row.TransportMode);
        Assert.Equal("HPH → RTM", row.RouteCode);
        Assert.Equal(0, row.RelatedBillCount);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(new { code, name })
        };
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record OrderCtxResponse(
        Guid Id,
        string OrderNo,
        string? TransportMode,
        string? RouteCode,
        string? Description);

    private sealed record OrderCustomerResponse(
        Guid Id,
        Guid? CustomerPartyId,
        string? RouteCode,
        ContextBody? Context);

    private sealed record ContextBody(int? PackageCount);

    private sealed record ShipmentListResponse(
        Guid Id,
        string ShipmentNo,
        string? TransportMode,
        string? RouteCode,
        int RelatedBillCount);
}
