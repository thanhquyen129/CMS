using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

/// <summary>PO standalone slice: D03 list/get, D02 catalog, global search, expected revenue seed.</summary>
[Collection("Api")]
public sealed class StandalonePoUiGapTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public StandalonePoUiGapTests(LcmsApiFactory factory)
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
    public async Task ShipmentListAndGet_IncludesRelatedBills_AndIsTenantIsolated()
    {
        var tenantA = await CreateTenantAsync("TN-UI-SH-A", "Ship A");
        var tenantB = await CreateTenantAsync("TN-UI-SH-B", "Ship B");
        var billId = await CreateBillAsync(tenantA, "BL-UI-SH", "freight");
        var shipmentId = await UpsertAsync(
            tenantA,
            "/api/shipments",
            new { shipmentNo = "SHP-UI-1", sourceSystem = "lcms_manual", externalId = "SHP-UI-1", isActive = true });

        using var link = WithTenant(HttpMethod.Post, $"/api/shipments/{shipmentId}/bills/{billId}", tenantA);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(link)).StatusCode);

        using var listA = WithTenant(HttpMethod.Get, "/api/shipments", tenantA);
        var ships = await (await _client.SendAsync(listA)).Content
            .ReadFromJsonAsync<List<ShipmentListItem>>(JsonOptions);
        Assert.Contains(ships!, s => s.Id == shipmentId && s.ShipmentNo == "SHP-UI-1");

        using var getA = WithTenant(HttpMethod.Get, $"/api/shipments/{shipmentId}", tenantA);
        var detail = await (await _client.SendAsync(getA)).Content
            .ReadFromJsonAsync<ShipmentDetail>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Contains(detail!.RelatedBills, b => b.Id == billId && b.BillNo == "BL-UI-SH");

        using var listB = WithTenant(HttpMethod.Get, "/api/shipments", tenantB);
        var shipsB = await (await _client.SendAsync(listB)).Content
            .ReadFromJsonAsync<List<ShipmentListItem>>(JsonOptions);
        Assert.DoesNotContain(shipsB!, s => s.Id == shipmentId);

        using var getB = WithTenant(HttpMethod.Get, $"/api/shipments/{shipmentId}", tenantB);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(getB)).StatusCode);
    }

    [Fact]
    public async Task CatalogUpsert_IsIdempotentAndTenantIsolated()
    {
        var tenantA = await CreateTenantAsync("TN-UI-CAT-A", "Cat A");
        var tenantB = await CreateTenantAsync("TN-UI-CAT-B", "Cat B");

        var id1 = await UpsertCatalogAsync(tenantA, "cost_type", "AIR", "Cước hàng không");
        var id2 = await UpsertCatalogAsync(tenantA, "cost_type", "AIR", "Cước hàng không (sửa)");
        Assert.Equal(id1, id2);

        using var listA = WithTenant(HttpMethod.Get, "/api/master-catalog?kind=cost_type", tenantA);
        var itemsA = await (await _client.SendAsync(listA)).Content
            .ReadFromJsonAsync<List<CatalogItem>>(JsonOptions);
        var air = Assert.Single(itemsA!, i => i.Code == "AIR");
        Assert.Equal("Cước hàng không (sửa)", air.Name);

        using var listB = WithTenant(HttpMethod.Get, "/api/master-catalog?kind=cost_type", tenantB);
        var itemsB = await (await _client.SendAsync(listB)).Content
            .ReadFromJsonAsync<List<CatalogItem>>(JsonOptions);
        Assert.DoesNotContain(itemsB!, i => i.Code == "AIR");
    }

    [Fact]
    public async Task GlobalSearch_FindsOrderAndIgnoresOtherTenant()
    {
        var tenantA = await CreateTenantAsync("TN-UI-SR-A", "Search A");
        var tenantB = await CreateTenantAsync("TN-UI-SR-B", "Search B");
        var orderId = await UpsertAsync(
            tenantA,
            "/api/orders",
            new { orderNo = "ORD-NEEDLE-9", sourceSystem = "lcms_manual", externalId = "ORD-NEEDLE-9", isActive = true });
        _ = await UpsertAsync(
            tenantB,
            "/api/orders",
            new { orderNo = "ORD-NEEDLE-9", sourceSystem = "lcms_manual", externalId = "ORD-NEEDLE-9", isActive = true });

        using var searchA = WithTenant(HttpMethod.Get, "/api/search?q=NEEDLE-9", tenantA);
        var hitsA = await (await _client.SendAsync(searchA)).Content
            .ReadFromJsonAsync<List<GlobalHit>>(JsonOptions);
        Assert.Contains(hitsA!, h => h.EntityType == "order" && h.Id == orderId);

        using var searchB = WithTenant(HttpMethod.Get, "/api/search?q=NEEDLE-9", tenantB);
        var hitsB = await (await _client.SendAsync(searchB)).Content
            .ReadFromJsonAsync<List<GlobalHit>>(JsonOptions);
        Assert.DoesNotContain(hitsB!, h => h.Id == orderId);
    }

    [Fact]
    public async Task BillSearch_FindsExternalId_WhenBillNoDiffers()
    {
        var tenantId = await CreateTenantAsync("TN-UI-HAWB", "HAWB search");
        using var create = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        create.Content = JsonContent.Create(new
        {
            billNo = "VOL-HIDE-1",
            billType = "house",
            externalId = "HAWB-UAT-001"
        });
        var created = await _client.SendAsync(create);
        created.EnsureSuccessStatusCode();
        var billId = (await created.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var list = WithTenant(HttpMethod.Get, "/api/bills?q=HAWB-UAT-001", tenantId);
        var rows = await (await _client.SendAsync(list)).Content.ReadFromJsonAsync<List<BillListRow>>(JsonOptions);
        var row = Assert.Single(rows!);
        Assert.Equal(billId, row.Id);
        Assert.Equal("VOL-HIDE-1", row.BillNo);
        Assert.Equal("HAWB-UAT-001", row.ExternalId);

        using var search = WithTenant(HttpMethod.Get, "/api/search?q=HAWB-UAT-001", tenantId);
        var hits = await (await _client.SendAsync(search)).Content.ReadFromJsonAsync<List<GlobalHit>>(JsonOptions);
        Assert.Contains(hits!, h => h.EntityType == "bill" && h.Id == billId && h.Code == "HAWB-UAT-001");
    }

    [Fact]
    public async Task SeedExpectedRevenues_FromRatingComponent_IsIdempotent()
    {
        var tenantId = await CreateTenantAsync("TN-UI-REV", "Seed Rev");
        var billId = await CreateBillAsync(tenantId, "BL-UI-REV", "freight");
        var cardId = await UpsertAsync(
            tenantId,
            "/api/rate-cards",
            new { code = "RC-REV", name = "Card rev", partyType = "customer", currencyCode = "VND" },
            HttpMethod.Post);
        var versionId = await UpsertAsync(
            tenantId,
            $"/api/rate-cards/{cardId}/versions",
            new { note = "draft" },
            HttpMethod.Post);
        var ruleId = await UpsertAsync(
            tenantId,
            $"/api/rate-versions/{versionId}/rules",
            new
            {
                code = "SELL",
                name = "Cước bán",
                calcMethod = "fixed",
                unitAmount = 2000,
                currencyCode = "VND",
                sortOrder = 1
            },
            HttpMethod.Post);

        using var addComp = WithTenant(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/components", tenantId);
        addComp.Content = JsonContent.Create(new
        {
            code = "SELL-LINE",
            name = "Doanh thu cước",
            financialNature = "revenue",
            revenueTypeCode = "FREIGHT-REV",
            amount = 2000,
            currencyCode = "VND",
            sortOrder = 1
        });
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(addComp)).StatusCode);

        using var publish = WithTenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(publish)).StatusCode);

        var ratingId = await UpsertAsync(
            tenantId,
            "/api/ratings",
            new { billId, rateVersionId = versionId, quantity = 1 },
            HttpMethod.Post);

        using var seed = WithTenant(HttpMethod.Post, $"/api/ratings/{ratingId}/seed-expected-revenues", tenantId);
        var seedRes = await _client.SendAsync(seed);
        Assert.Equal(HttpStatusCode.OK, seedRes.StatusCode);
        var first = await seedRes.Content.ReadFromJsonAsync<SeedResult>(JsonOptions);
        Assert.Equal(1, first!.CreatedCount);

        using var list = WithTenant(HttpMethod.Get, $"/api/revenues?billId={billId}", tenantId);
        var revenues = await (await _client.SendAsync(list)).Content
            .ReadFromJsonAsync<List<RevenueListItem>>(JsonOptions);
        Assert.Single(revenues!);
        Assert.Equal(2000m, revenues[0].Amount);
        Assert.Equal("expected", revenues[0].FinancialMaturity);

        using var seedAgain = WithTenant(HttpMethod.Post, $"/api/ratings/{ratingId}/seed-expected-revenues", tenantId);
        var again = await (await _client.SendAsync(seedAgain)).Content
            .ReadFromJsonAsync<SeedResult>(JsonOptions);
        Assert.Equal(0, again!.CreatedCount);
        Assert.Equal(1, again.ExistingCount);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo, string billType)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> UpsertAsync(Guid tenantId, string url, object body, HttpMethod? method = null)
    {
        using var req = WithTenant(method ?? HttpMethod.Put, url, tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> UpsertCatalogAsync(Guid tenantId, string kind, string code, string name)
    {
        using var req = WithTenant(HttpMethod.Put, "/api/master-catalog", tenantId);
        req.Content = JsonContent.Create(new { kind, code, name, isActive = true });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private static HttpRequestMessage WithTenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record ShipmentListItem(Guid Id, string ShipmentNo);
    private sealed record RelatedBill(Guid Id, string BillNo);
    private sealed record ShipmentDetail(Guid Id, string ShipmentNo, List<RelatedBill> RelatedBills);
    private sealed record CatalogItem(string Code, string Name);
    private sealed record GlobalHit(string EntityType, Guid Id, string Code);
    private sealed record BillListRow(Guid Id, string BillNo, string? ExternalId);
    private sealed record SeedResult(int CreatedCount, int ExistingCount);
    private sealed record RevenueListItem(decimal Amount, string FinancialMaturity);
}
