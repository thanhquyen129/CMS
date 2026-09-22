using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class AllocationBasisTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public AllocationBasisTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task GrossKg_UsesMeasurements_ZeroBasisDoesNotSplitEvenly_RemainderIsRecorded()
    {
        var tenantId = await CreateTenantAsync();
        var billA = await CreateBillAsync(tenantId, "BL-KG-A");
        var billB = await CreateBillAsync(tenantId, "BL-KG-B");
        var billC = await CreateBillAsync(tenantId, "BL-KG-C");
        await PatchMeasure(tenantId, billA, 80m);
        await PatchMeasure(tenantId, billB, 20m);
        var sharedId = await CreateSharedAsync(tenantId, 100m);
        var emptyId = await CreateSharedAsync(tenantId, 100m);
        using var zero = Tenant(HttpMethod.Post, $"/api/costs/{emptyId}/allocations", tenantId);
        zero.Content = JsonContent.Create(new
        {
            allocationBasis = "chargeable",
            details = new[] { new { billId = billA }, new { billId = billB } }
        });
        var blocked = await _client.SendAsync(zero);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<Err>(Json);
        Assert.Contains("ZERO_ALLOCATION_BASIS", err!.Message);

        var kgId = await Allocate(tenantId, sharedId, "gross_kg", new[] { billA, billB });
        await PostNoContent(tenantId, $"/api/cost-allocations/{kgId}/calculate");
        var calculated = await GetCost(tenantId, sharedId);
        var session = Assert.Single(calculated.Allocations, a => a.Id == kgId);
        Assert.Equal("calculated", session.AllocationStatus);
        Assert.Equal(80m, session.Details.Single(d => d.BillId == billA).AllocatedAmount);
        Assert.Equal(20m, session.Details.Single(d => d.BillId == billB).AllocatedAmount);

        await PostNoContent(tenantId, $"/api/cost-allocations/{kgId}/cancel");
        var third = await CreateSharedAsync(tenantId, 100m);
        var equalId = await Allocate(tenantId, third, "equal", new[] { billA, billB, billC });
        await PostNoContent(tenantId, $"/api/cost-allocations/{equalId}/finalize");
        var finalized = await GetCost(tenantId, third);
        var lines = Assert.Single(finalized.Allocations).Details;
        Assert.Equal(100m, lines.Sum(d => d.AllocatedAmount));
        Assert.Contains(lines, d => d.RoundingAdjustment != 0m);
    }

    [Fact]
    public async Task LegScope_RejectsBillOutsideTheLeg_AndPercentMustSumTo100()
    {
        var tenantId = await CreateTenantAsync();
        var onLeg = await CreateBillAsync(tenantId, "BL-LEG-1");
        var also = await CreateBillAsync(tenantId, "BL-LEG-2");
        var outsider = await CreateBillAsync(tenantId, "BL-LEG-X");
        var shipmentId = await PutId(tenantId, "/api/shipments", new
        {
            shipmentNo = "SH-D",
            sourceSystem = "lcms_manual",
            externalId = "sh-d",
            isActive = true
        });
        var legId = await PutId(tenantId, "/api/transport-legs", new
        {
            legNo = "LEG-D",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "leg-d",
            isActive = true
        });
        await PostNoContent(tenantId, $"/api/transport-legs/{legId}/bills/{onLeg}");
        await PostNoContent(tenantId, $"/api/transport-legs/{legId}/bills/{also}");

        var sharedId = await CreateSharedAsync(tenantId, 100m);
        using var wrong = Tenant(HttpMethod.Post, $"/api/costs/{sharedId}/allocations", tenantId);
        wrong.Content = JsonContent.Create(new
        {
            allocationBasis = "equal",
            applicabilityMode = "leg",
            scopeId = legId,
            details = new[] { new { billId = onLeg }, new { billId = outsider } }
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(wrong)).StatusCode);

        using var percent = Tenant(HttpMethod.Post, $"/api/costs/{sharedId}/allocations", tenantId);
        percent.Content = JsonContent.Create(new
        {
            allocationBasis = "manual_percent",
            details = new[]
            {
                new { billId = onLeg, basisValue = 40m },
                new { billId = also, basisValue = 50m }
            }
        });
        var percentRes = await _client.SendAsync(percent);
        percentRes.EnsureSuccessStatusCode();
        var percentId = (await percentRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var fin = Tenant(HttpMethod.Post, $"/api/cost-allocations/{percentId}/finalize", tenantId);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(fin)).StatusCode);
    }

    private async Task PatchMeasure(Guid tenantId, Guid billId, decimal kg)
    {
        using var req = Tenant(HttpMethod.Patch, $"/api/bills/{billId}/context", tenantId);
        req.Content = JsonContent.Create(new { context = new { grossWeightKg = kg } });
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> Allocate(Guid tenantId, Guid costId, string basis, Guid[] bills)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/costs/{costId}/allocations", tenantId);
        req.Content = JsonContent.Create(new
        {
            allocationBasis = basis,
            details = bills.Select(id => new { billId = id, basisValue = (decimal?)null }).ToArray()
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateSharedAsync(Guid tenantId, decimal amount)
    {
        using var req = Tenant(HttpMethod.Post, "/api/costs", tenantId);
        req.Content = JsonContent.Create(new { attributionType = "shared", amount, currencyCode = "VND", costTypeCode = "SHARED" });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = Tenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Alloc" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> PutId(Guid tenantId, string url, object body)
    {
        using var req = Tenant(HttpMethod.Put, url, tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task PostNoContent(Guid tenantId, string url)
    {
        using var req = Tenant(HttpMethod.Post, url, tenantId);
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<CostBody> GetCost(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/costs/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CostBody>(Json))!;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record Err(string Message);
    private sealed record CostBody(List<AllocBody> Allocations);
    private sealed record AllocBody(Guid Id, string AllocationStatus, List<LineBody> Details);
    private sealed record LineBody(Guid BillId, decimal AllocatedAmount, decimal RoundingAdjustment);
}
