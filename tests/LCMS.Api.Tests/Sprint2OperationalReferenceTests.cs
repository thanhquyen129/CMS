using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint2OperationalReferenceTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint2OperationalReferenceTests(LcmsApiFactory factory)
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
    public async Task GetOrder_CrossTenant_Returns404()
    {
        var tenantA = await CreateTenantAsync("TN-ORD-A", "Order Tenant A");
        var tenantB = await CreateTenantAsync("TN-ORD-B", "Order Tenant B");

        var orderId = await UpsertOrderAsync(tenantA, "ORD-1", "tms", "ext-ord-1");

        using var getAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{orderId}");
        getAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        var leaked = await _client.SendAsync(getAsB);
        Assert.Equal(HttpStatusCode.NotFound, leaked.StatusCode);

        var err = await leaked.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("not_found", err!.Code);
        Assert.DoesNotContain(orderId.ToString(), err.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertOrder_SameExternalIdentity_IsIdempotent()
    {
        var tenantId = await CreateTenantAsync("TN-ORD-IDEM", "Idempotent Tenant");

        var firstId = await UpsertOrderAsync(
            tenantId,
            "ORD-A",
            "tms",
            "ext-100",
            externalVersion: "v1",
            operationalStatus: "active");

        var secondId = await UpsertOrderAsync(
            tenantId,
            "ORD-A-RENAMED",
            "tms",
            "ext-100",
            externalVersion: "v2",
            operationalStatus: "confirmed");

        Assert.Equal(firstId, secondId);

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/orders/{firstId}");
        get.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var order = await res.Content.ReadFromJsonAsync<OrderResponse>(JsonOptions);
        Assert.Equal("ORD-A-RENAMED", order!.OrderNo);
        Assert.Equal("v2", order.ExternalVersion);
        Assert.Equal("confirmed", order.OperationalStatus);

        using var listReq = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
        listReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var listRes = await _client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var list = await listRes.Content.ReadFromJsonAsync<List<OrderResponse>>(JsonOptions);
        Assert.Single(list!);
        Assert.Equal(firstId, list![0].Id);
    }

    [Fact]
    public async Task GetBillGraph_ReturnsLinkedOrdersAndShipments()
    {
        var tenantId = await CreateTenantAsync("TN-GRAPH", "Graph Tenant");

        var billId = await CreateBillAsync(tenantId, "BL-GRAPH-1", "freight");
        var orderId = await UpsertOrderAsync(tenantId, "ORD-G1", "tms", "ext-g-ord");
        var shipmentId = await UpsertShipmentAsync(tenantId, "SHP-G1", "tms", "ext-g-shp");

        using var linkOrder = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orders/{orderId}/bills/{billId}");
        linkOrder.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(linkOrder)).StatusCode);

        using var linkShipment = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/bills/{billId}/shipments/{shipmentId}");
        linkShipment.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(linkShipment)).StatusCode);

        // Re-link is idempotent (no duplicate edge)
        using var linkOrderAgain = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/orders/{orderId}/bills/{billId}");
        linkOrderAgain.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(linkOrderAgain)).StatusCode);

        using var graphReq = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billId}/graph");
        graphReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var graphRes = await _client.SendAsync(graphReq);
        Assert.Equal(HttpStatusCode.OK, graphRes.StatusCode);

        var graph = await graphRes.Content.ReadFromJsonAsync<BillGraphResponse>(JsonOptions);
        Assert.NotNull(graph);
        Assert.Equal(billId, graph!.BillId);
        Assert.Equal("BL-GRAPH-1", graph.BillNo);
        Assert.Single(graph.Orders);
        Assert.Equal(orderId, graph.Orders[0].Id);
        Assert.Equal("ORD-G1", graph.Orders[0].OrderNo);
        Assert.Single(graph.Shipments);
        Assert.Equal(shipmentId, graph.Shipments[0].Id);
        Assert.Equal("SHP-G1", graph.Shipments[0].ShipmentNo);
    }

    [Fact]
    public async Task GetBillGraph_CrossTenant_Returns404()
    {
        var tenantA = await CreateTenantAsync("TN-G-A", "Graph A");
        var tenantB = await CreateTenantAsync("TN-G-B", "Graph B");
        var billId = await CreateBillAsync(tenantA, "BL-ISO", "freight");

        using var graphAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billId}/graph");
        graphAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(graphAsB)).StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo, string billType)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo, billType })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> UpsertOrderAsync(
        Guid tenantId,
        string orderNo,
        string sourceSystem,
        string externalId,
        string? externalVersion = null,
        string? operationalStatus = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/orders")
        {
            Content = JsonContent.Create(new
            {
                orderNo,
                sourceSystem,
                externalId,
                externalVersion,
                operationalStatus,
                isActive = true
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> UpsertShipmentAsync(
        Guid tenantId,
        string shipmentNo,
        string sourceSystem,
        string externalId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, "/api/shipments")
        {
            Content = JsonContent.Create(new
            {
                shipmentNo,
                sourceSystem,
                externalId,
                isActive = true
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record OrderResponse(
        Guid Id,
        Guid TenantId,
        string OrderNo,
        string SourceSystem,
        string ExternalId,
        string? ExternalVersion,
        string OperationalStatus,
        bool IsActive);

    private sealed record BillGraphOrderRef(
        Guid Id,
        string OrderNo,
        string SourceSystem,
        string ExternalId,
        string OperationalStatus);

    private sealed record BillGraphShipmentRef(
        Guid Id,
        string ShipmentNo,
        string SourceSystem,
        string ExternalId,
        string OperationalStatus);

    private sealed record BillGraphResponse(
        Guid BillId,
        string BillNo,
        string BillType,
        string OperationalStatus,
        List<BillGraphOrderRef> Orders,
        List<BillGraphShipmentRef> Shipments);

    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
