using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class UnlinkRelationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public UnlinkRelationTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task UnlinkOrderBill_SoftDeletes_AndRemovesFromGraph()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-UNL-1");
        var orderId = await PutId(tenantId, "/api/orders", new
        {
            orderNo = "OR-UNL-1",
            sourceSystem = "lcms_manual",
            externalId = "or-unl-1",
            isActive = true
        });

        using (var link = Tenant(HttpMethod.Post, $"/api/orders/{orderId}/bills/{billId}", tenantId))
        {
            (await _client.SendAsync(link)).EnsureSuccessStatusCode();
        }

        var graphBefore = await GetGraph(tenantId, billId);
        Assert.Contains(graphBefore.Orders, o => o.Id == orderId && o.LinkId != null);

        using (var unlink = Tenant(HttpMethod.Delete, $"/api/orders/{orderId}/bills/{billId}?reason=sai%20lien%20ket", tenantId))
        {
            var res = await _client.SendAsync(unlink);
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        }

        var graphAfter = await GetGraph(tenantId, billId);
        Assert.DoesNotContain(graphAfter.Orders, o => o.Id == orderId);

        using (var again = Tenant(HttpMethod.Delete, $"/api/orders/{orderId}/bills/{billId}", tenantId))
        {
            var res = await _client.SendAsync(again);
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        }
    }

    [Fact]
    public async Task UnlinkBillShipment_Works()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-UNL-2");
        var shipmentId = await PutId(tenantId, "/api/shipments", new
        {
            shipmentNo = "SH-UNL-2",
            sourceSystem = "lcms_manual",
            externalId = "sh-unl-2",
            isActive = true
        });

        using (var link = Tenant(HttpMethod.Post, $"/api/bills/{billId}/shipments/{shipmentId}", tenantId))
        {
            (await _client.SendAsync(link)).EnsureSuccessStatusCode();
        }

        using (var unlink = Tenant(HttpMethod.Delete, $"/api/bills/{billId}/shipments/{shipmentId}", tenantId))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(unlink)).StatusCode);
        }

        var graph = await GetGraph(tenantId, billId);
        Assert.Empty(graph.Shipments);
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tenants",
            new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Unlink" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
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

    private async Task<Guid> PutId(Guid tenantId, string url, object body)
    {
        using var req = Tenant(HttpMethod.Put, url, tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<GraphBody> GetGraph(Guid tenantId, Guid billId)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/bills/{billId}/graph", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<GraphBody>(Json))!;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record GraphBody(List<OrderRef> Orders, List<object> Shipments);
    private sealed record OrderRef(Guid Id, Guid? LinkId);
}
