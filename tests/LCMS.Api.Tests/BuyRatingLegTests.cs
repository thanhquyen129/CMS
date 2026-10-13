using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

/// <summary>COST-LEG v1.1 — buy rating by linked leg reuses the existing engine (AC-24..AC-35).</summary>
[Collection("Api")]
public sealed class BuyRatingLegTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public BuyRatingLegTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task LegBuy_StoresTrace_DoesNotUseBillWeight_AndRetryDoesNotDuplicate()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-LEG-BUY");
        await EditBillAsync(tenantId, billId, new { transport_mode = "air", gross_weight_kg = "100", volume_cbm = "1" });
        var vendorId = await CreateVendorAsync(tenantId, "NCC-1", "Vendor một");
        var (cardId, versionId) = await PublishBuyAsync(tenantId, "RC-LEG", vendorId, 10m);
        var shipmentId = await PutId(tenantId, "/api/shipments", new
        {
            shipmentNo = "SH-1",
            sourceSystem = "lcms_manual",
            externalId = "sh-1",
            transportMode = "road",
            originCode = "HPH",
            destinationCode = "HAN",
            isActive = true
        });
        var legId = await PutId(tenantId, "/api/transport-legs", new
        {
            legNo = "LEG-01",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "leg-01",
            originCode = "HPH",
            destinationCode = "HAN",
            isActive = true
        });
        await PostNoContent(tenantId, $"/api/transport-legs/{legId}/bills/{billId}");

        using var missing = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        missing.Content = JsonContent.Create(new
        {
            billId,
            rateVersionId = versionId,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        var blocked = await _client.SendAsync(missing);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var blockedErr = await blocked.Content.ReadFromJsonAsync<ErrBody>(Json);
        Assert.Contains("Không dùng trọng lượng của cả Bill", blockedErr!.Message);

        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 4m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        var again = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 4m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        Assert.Equal(ratingId, again);

        var rating = await GetRatingAsync(tenantId, ratingId);
        Assert.Equal(40m, rating.TotalAmount);
        Assert.Equal("leg", rating.ContextType);
        Assert.Equal(legId, rating.TransportLegId);
        Assert.Equal(shipmentId, rating.ShipmentId);
        Assert.Equal(vendorId, rating.VendorPartyId);
        Assert.Equal(cardId, rating.RateCardId);
        Assert.Equal("HPH", rating.ContextOrigin);

        var costs = await ListCostsAsync(tenantId, billId);
        var cost = Assert.Single(costs);
        Assert.Equal("direct", cost.AttributionType);
        Assert.Equal("expected", cost.FinancialMaturity);
        Assert.Equal(40m, cost.ExpectedAmount);
        Assert.Equal(legId, cost.TransportLegId);
        Assert.Equal(vendorId, cost.VendorPartyId);
        Assert.Equal(cardId, cost.RateCardId);
        Assert.Equal(versionId, cost.RateVersionId);
        Assert.Equal("leg", cost.OriginLevel);
        Assert.Null(cost.ConfirmedAmount);
        Assert.Null(cost.ActualAmount);
    }

    [Fact]
    public async Task SharedLeg_IsOneSourceCost_AndIsNotCopiedOntoEachBill()
    {
        var tenantId = await CreateTenantAsync();
        var billA = await CreateBillAsync(tenantId, "BL-A");
        var billB = await CreateBillAsync(tenantId, "BL-B");
        var billC = await CreateBillAsync(tenantId, "BL-C");
        var vendor1 = await CreateVendorAsync(tenantId, "NCC-A", "Vendor A");
        var vendor2 = await CreateVendorAsync(tenantId, "NCC-B", "Vendor B");
        var (_, version1) = await PublishBuyAsync(tenantId, "RC-A", vendor1, 25m);
        var (_, version2) = await PublishBuyAsync(tenantId, "RC-B", vendor2, 7m);
        var shipmentId = await PutId(tenantId, "/api/shipments", new
        {
            shipmentNo = "SH-SHARE",
            sourceSystem = "lcms_manual",
            externalId = "sh-share",
            transportMode = "road",
            isActive = true
        });
        var legId = await PutId(tenantId, "/api/transport-legs", new
        {
            legNo = "LEG-SHARE",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "leg-share",
            originCode = "SGN",
            destinationCode = "DAD",
            isActive = true
        });
        await PostNoContent(tenantId, $"/api/transport-legs/{legId}/bills/{billA}");
        await PostNoContent(tenantId, $"/api/transport-legs/{legId}/bills/{billB}");
        await PostNoContent(tenantId, $"/api/transport-legs/{legId}/bills/{billC}");

        var outsider = await CreateBillAsync(tenantId, "BL-OUT");
        using var foreign = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        foreign.Content = JsonContent.Create(new
        {
            billId = outsider,
            rateVersionId = version1,
            quantity = 2m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendor1,
            seedExpectedCosts = true
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(foreign)).StatusCode);

        var first = await RateAsync(tenantId, new
        {
            billId = billA,
            rateVersionId = version1,
            quantity = 2m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendor1,
            seedExpectedCosts = true
        });
        var secondBill = await RateAsync(tenantId, new
        {
            billId = billB,
            rateVersionId = version1,
            quantity = 2m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendor1,
            seedExpectedCosts = true
        });
        Assert.Equal(first, secondBill);
        await RateAsync(tenantId, new
        {
            billId = billA,
            rateVersionId = version2,
            quantity = 2m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendor2,
            seedExpectedCosts = true
        });

        var shared = await ListCostsAsync(tenantId, null, "shared");
        Assert.Equal(2, shared.Count);
        Assert.All(shared, c =>
        {
            Assert.Null(c.BillId);
            Assert.Equal(legId, c.TransportLegId);
            Assert.Equal("expected", c.FinancialMaturity);
        });
        Assert.Equal(2, shared.Select(c => c.VendorPartyId).Distinct().Count());
        Assert.Empty(await ListCostsAsync(tenantId, billA));
        Assert.Empty(await ListCostsAsync(tenantId, billB));
        Assert.Equal(0m, await ProfitCostAsync(tenantId, billA));
        Assert.Equal(0m, await ProfitCostAsync(tenantId, billC));

        using var changed = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        changed.Content = JsonContent.Create(new
        {
            billId = billA,
            rateVersionId = version1,
            quantity = 9m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendor1,
            seedExpectedCosts = true
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(changed)).StatusCode);
    }

    [Fact]
    public async Task Suggestions_ListOnlyLinkedLegs_AndDoNotPickAmongVendors()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-SUG");
        var other = await CreateBillAsync(tenantId, "BL-SUG-2");
        var vendor1 = await CreateVendorAsync(tenantId, "NCC-1", "Vendor 1");
        var vendor2 = await CreateVendorAsync(tenantId, "NCC-2", "Vendor 2");
        await PublishBuyAsync(tenantId, "RC-1", vendor1, 1m);
        await PublishBuyAsync(tenantId, "RC-2", vendor2, 1m);
        var shipmentId = await PutId(tenantId, "/api/shipments", new
        {
            shipmentNo = "SH-SUG",
            sourceSystem = "lcms_manual",
            externalId = "sh-sug",
            transportMode = "road",
            isActive = true
        });
        var linked = await PutId(tenantId, "/api/transport-legs", new
        {
            legNo = "LEG-IN",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "leg-in",
            originCode = "HAN",
            destinationCode = "SGN",
            isActive = true
        });
        var outside = await PutId(tenantId, "/api/transport-legs", new
        {
            legNo = "LEG-OUT",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "leg-out",
            originCode = "DAD",
            destinationCode = "PQC",
            isActive = true
        });
        await PostNoContent(tenantId, $"/api/transport-legs/{linked}/bills/{billId}");
        await PostNoContent(tenantId, $"/api/transport-legs/{outside}/bills/{other}");

        using var req = Tenant(HttpMethod.Get, $"/api/bills/{billId}/rate-suggestions?contextType=leg&legId={linked}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<SuggestionBody>(Json);
        Assert.NotNull(body);
        var leg = Assert.Single(body!.Legs);
        Assert.Equal(linked, leg.Id);
        Assert.Equal("SH-SUG", leg.ShipmentNo);
        Assert.Equal("road", leg.TransportMode);
        Assert.Equal("HAN", leg.OriginCode);
        Assert.Equal(2, body.Vendors.Count);
        Assert.Null(body.VendorPartyId);
        Assert.Empty(body.Buy.Options);
        Assert.Contains("vendor", body.Buy.Notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BillContext_StillRatesWhenTheBillHasNoLeg()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-ONLY");
        var (_, versionId) = await PublishBuyAsync(tenantId, "RC-BILL", null, 3m);
        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 2m,
            contextType = "bill",
            seedExpectedCosts = true
        });
        var rating = await GetRatingAsync(tenantId, ratingId);
        Assert.Equal("bill", rating.ContextType);
        Assert.Null(rating.TransportLegId);
        Assert.Equal(6m, rating.TotalAmount);
        var cost = Assert.Single(await ListCostsAsync(tenantId, billId));
        Assert.Equal("direct", cost.AttributionType);
        Assert.Equal(billId, cost.BillId);
        Assert.Equal("expected", cost.FinancialMaturity);
    }

    private async Task<(Guid CardId, Guid VersionId)> PublishBuyAsync(Guid tenantId, string code, Guid? vendorId, decimal unitAmount)
    {
        using var cardReq = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        cardReq.Content = JsonContent.Create(new { code, name = code, partyType = "vendor", currencyCode = "VND" });
        var cardRes = await _client.SendAsync(cardReq);
        cardRes.EnsureSuccessStatusCode();
        var cardId = (await cardRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        if (vendorId is Guid vendor)
        {
            using var partner = Tenant(HttpMethod.Put, $"/api/rate-cards/{cardId}/partner", tenantId);
            partner.Content = JsonContent.Create(new { supplierPartyId = vendor });
            (await _client.SendAsync(partner)).EnsureSuccessStatusCode();
        }

        using var versionReq = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        versionReq.Content = JsonContent.Create(new { note = code });
        var versionRes = await _client.SendAsync(versionReq);
        versionRes.EnsureSuccessStatusCode();
        var versionId = (await versionRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var rule = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId);
        rule.Content = JsonContent.Create(new
        {
            code = "FRT",
            name = "Cước",
            calcMethod = "unit_rate",
            unitAmount,
            currencyCode = "VND"
        });
        (await _client.SendAsync(rule)).EnsureSuccessStatusCode();
        using var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        (await _client.SendAsync(publish)).EnsureSuccessStatusCode();
        return (cardId, versionId);
    }

    private async Task<Guid> CreateVendorAsync(Guid tenantId, string code, string name)
    {
        using var req = Tenant(HttpMethod.Post, "/api/business-parties", tenantId);
        req.Content = JsonContent.Create(new { code, name, roleCodes = new[] { "vendor" } });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task EditBillAsync(Guid tenantId, Guid billId, object changes)
    {
        using var req = Tenant(HttpMethod.Patch, $"/api/operational-references/bill/{billId}", tenantId);
        req.Content = JsonContent.Create(new { changes, reason = "test" });
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<decimal> ProfitCostAsync(Guid tenantId, Guid billId)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/bills/{billId}/profitability?view=expected", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<ProfitBody>(Json);
        return body?.ByCurrency.Sum(b => b.CostAmount) ?? 0m;
    }

    private async Task<List<CostBody>> ListCostsAsync(Guid tenantId, Guid? billId, string? attribution = null)
    {
        var query = new List<string>();
        if (billId is Guid id) query.Add($"billId={id}");
        if (attribution is not null) query.Add($"attributionType={attribution}");
        var path = "/api/costs" + (query.Count == 0 ? "" : "?" + string.Join("&", query));
        using var req = Tenant(HttpMethod.Get, path, tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<CostBody>>(Json))!;
    }

    private async Task<RatingBody> GetRatingAsync(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/ratings/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadAsStringAsync();
        var rating = JsonSerializer.Deserialize<RatingBody>(raw, Json)!;
        using var doc = JsonDocument.Parse(rating.ContextJson ?? "{}");
        rating.ContextOrigin = doc.RootElement.TryGetProperty("originCode", out var origin) ? origin.GetString() : null;
        return rating;
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        using var req = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
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

    private async Task<Guid> PutId(Guid tenantId, string url, object body)
    {
        using var req = Tenant(HttpMethod.Put, url, tenantId);
        req.Content = JsonContent.Create(body);
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
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Leg rate" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record ErrBody(string Message);
    private sealed record RatingBody(
        decimal TotalAmount,
        string? ContextType,
        Guid? TransportLegId,
        Guid? ShipmentId,
        Guid? VendorPartyId,
        Guid? RateCardId,
        string? ContextJson)
    {
        public string? ContextOrigin { get; set; }
    }

    private sealed record CostBody(
        Guid? BillId,
        string AttributionType,
        string FinancialMaturity,
        decimal ExpectedAmount,
        decimal? ConfirmedAmount,
        decimal? ActualAmount,
        Guid? TransportLegId,
        Guid? VendorPartyId,
        Guid? RateCardId,
        Guid? RateVersionId,
        string? OriginLevel);

    private sealed record ProfitBody(List<ProfitBucket> ByCurrency);
    private sealed record ProfitBucket(decimal CostAmount);
    private sealed record SuggestionBody(List<LegBody> Legs, List<VendorBody> Vendors, Guid? VendorPartyId, SideBody Buy);
    private sealed record LegBody(Guid Id, string ShipmentNo, string? TransportMode, string? OriginCode);
    private sealed record VendorBody(Guid Id);
    private sealed record SideBody(string Notice, List<OptionBody> Options);
    private sealed record OptionBody(Guid VersionId);
}
