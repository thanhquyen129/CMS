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
        Assert.Equal("ROAD", leg.TransportMode);
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

    [Fact]
    public async Task Ac25_SuggestionsListOnlyLegsLinkedToTheBill()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-25");
        var other = await CreateBillAsync(tenantId, "BL-25-X");
        var a = await LinkLegAsync(tenantId, billId, "L1", "SH-25-A", "HAN", "SGN");
        var b = await LinkLegAsync(tenantId, billId, "L2", "SH-25-B", "SGN", "DAD");
        var c = await LinkLegAsync(tenantId, billId, "L3", "SH-25-C", "DAD", "PQC");
        var outside = await LinkLegAsync(tenantId, other, "LX", "SH-25-X", "HAN", "PQC");

        using var req = Tenant(HttpMethod.Get, $"/api/bills/{billId}/rate-suggestions?contextType=leg", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<SuggestionBody>(Json);
        Assert.NotNull(body);
        Assert.Equal(3, body!.Legs.Count);
        Assert.Contains(body.Legs, l => l.Id == a && l.ShipmentNo == "SH-25-A" && l.OriginCode == "HAN");
        Assert.Contains(body.Legs, l => l.Id == b && l.ShipmentNo == "SH-25-B");
        Assert.Contains(body.Legs, l => l.Id == c && l.ShipmentNo == "SH-25-C");
        Assert.DoesNotContain(body.Legs, l => l.Id == outside);
        Assert.Empty(body.Buy.Options);
    }

    [Fact]
    public async Task Ac27_RouteMismatchIsNotSelectable_AndSeveralVendorsStayUnpicked()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-27");
        var vendorId = await CreateVendorAsync(tenantId, "NCC-27", "Vendor tuyến");
        var otherVendor = await CreateVendorAsync(tenantId, "NCC-27B", "Vendor khác");
        var legId = await LinkLegAsync(tenantId, billId, "L27", "SH-27", "HPH", "HAN");
        await PatchObjectAsync(tenantId, "leg", legId, new { route_code = "HPH-HAN", transport_mode = "road" });
        var (_, match) = await PublishBuyAsync(tenantId, "RC-MATCH", vendorId, 1m, routeCode: "HPH-HAN");
        var (_, miss) = await PublishBuyAsync(tenantId, "RC-MISS", vendorId, 1m, routeCode: "XXX");
        await PublishBuyAsync(tenantId, "RC-OTHER", otherVendor, 1m, routeCode: "HPH-HAN");

        using var open = Tenant(HttpMethod.Get, $"/api/bills/{billId}/rate-suggestions?contextType=leg&legId={legId}", tenantId);
        var openRes = await _client.SendAsync(open);
        openRes.EnsureSuccessStatusCode();
        var openBody = await openRes.Content.ReadFromJsonAsync<SuggestionBody>(Json);
        Assert.Null(openBody!.VendorPartyId);
        Assert.Empty(openBody.Buy.Options);
        Assert.Contains("vendor", openBody.Buy.Notice, StringComparison.OrdinalIgnoreCase);

        using var req = Tenant(HttpMethod.Get, $"/api/bills/{billId}/rate-suggestions?contextType=leg&legId={legId}&vendorPartyId={vendorId}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<SuggestionBody>(Json);
        Assert.NotNull(body);
        Assert.Equal(vendorId, body!.VendorPartyId);
        Assert.Equal(2, body.Vendors.Count);
        var selectable = body.Buy.Options.Where(o => o.Selectable).Select(o => o.VersionId).ToList();
        Assert.Equal(match, Assert.Single(selectable));
        Assert.DoesNotContain(body.Buy.Options, o => o.VersionId == miss && o.Selectable);
    }

    [Fact]
    public async Task Ac28_UnpublishedVersionAndMissingLegWeightCreateNoCost()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-28");
        var vendorId = await CreateVendorAsync(tenantId, "NCC-28", "Vendor 28");
        var legId = await LinkLegAsync(tenantId, billId, "L28", "SH-28", "HAN", "SGN");
        var (_, draftId) = await PublishBuyAsync(tenantId, "RC-DRAFT", vendorId, 8m, publish: false);
        var draftMessage = await ExpectConflictAsync(tenantId, new
        {
            billId,
            rateVersionId = draftId,
            quantity = 1m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        Assert.Contains("phát hành", draftMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await ListCostsAsync(tenantId, null, "shared"));
        Assert.Empty(await ListCostsAsync(tenantId, billId));

        var (_, versionId) = await PublishBuyAsync(tenantId, "RC-28", vendorId, 8m);
        var missing = await ExpectConflictAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        Assert.Contains("Không dùng trọng lượng của cả Bill", missing, StringComparison.Ordinal);
        Assert.Empty(await ListCostsAsync(tenantId, billId));
    }

    [Fact]
    public async Task Ac29_UnlinkedLegAndOtherTenantAreRejected()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var billA = await CreateBillAsync(tenantA, "BL-29-A");
        var billB = await CreateBillAsync(tenantB, "BL-29-B");
        var vendorA = await CreateVendorAsync(tenantA, "NCC-29", "Vendor 29");
        var (_, versionA) = await PublishBuyAsync(tenantA, "RC-29", vendorA, 4m);
        var linked = await LinkLegAsync(tenantA, billA, "L29", "SH-29", "HAN", "SGN");
        var shipmentId = await PutId(tenantA, "/api/shipments", new
        {
            shipmentNo = "SH-29-OUT",
            sourceSystem = "lcms_manual",
            externalId = "sh-29-out",
            isActive = true
        });
        var unlinked = await PutId(tenantA, "/api/transport-legs", new
        {
            legNo = "L29-OUT",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "l29-out",
            isActive = true
        });

        var unlinkedMessage = await ExpectConflictAsync(tenantA, new
        {
            billId = billA,
            rateVersionId = versionA,
            quantity = 1m,
            contextType = "leg",
            transportLegId = unlinked,
            vendorPartyId = vendorA,
            seedExpectedCosts = true
        });
        Assert.Contains("liên kết", unlinkedMessage, StringComparison.OrdinalIgnoreCase);

        using var foreign = Tenant(HttpMethod.Post, "/api/ratings", tenantB);
        foreign.Content = JsonContent.Create(new
        {
            billId = billB,
            rateVersionId = versionA,
            quantity = 1m,
            contextType = "leg",
            transportLegId = linked,
            vendorPartyId = vendorA,
            seedExpectedCosts = true
        });
        var foreignRes = await _client.SendAsync(foreign);
        Assert.NotEqual(HttpStatusCode.OK, foreignRes.StatusCode);
        Assert.NotEqual(HttpStatusCode.Created, foreignRes.StatusCode);
        Assert.Empty(await ListCostsAsync(tenantB, billB));
        Assert.Empty(await ListCostsAsync(tenantA, billA));

        using var seen = Tenant(HttpMethod.Get, $"/api/bills/{billB}/rate-suggestions?contextType=leg", tenantB);
        var seenRes = await _client.SendAsync(seen);
        seenRes.EnsureSuccessStatusCode();
        var suggestions = await seenRes.Content.ReadFromJsonAsync<SuggestionBody>(Json);
        Assert.DoesNotContain(suggestions!.Legs, l => l.Id == linked);
    }

    [Fact]
    public async Task Ac30_LegWeightAndServiceAreUsed_NotTheBill()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-30");
        await EditBillAsync(tenantId, billId, new { transport_mode = "air", service_type_code = "house", gross_weight_kg = "100", volume_cbm = "1" });
        var vendorId = await CreateVendorAsync(tenantId, "NCC-30", "Vendor 30");
        var (_, versionId) = await PublishBuyAsync(tenantId, "RC-30", vendorId, 10m, serviceTypeCode: "LTL");
        var legId = await LinkLegAsync(tenantId, billId, "L30", "SH-30", "HAN", "SGN");
        await PatchObjectAsync(tenantId, "leg", legId, new
        {
            transport_mode = "air",
            service_type_code = "LTL",
            gross_weight_kg = "12",
            volume_cbm = "0.2"
        });

        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        var rating = await GetRatingAsync(tenantId, ratingId);
        Assert.Equal(33.4m, rating.Quantity);
        Assert.Equal(334m, rating.TotalAmount);
        Assert.Equal(12m, rating.ContextGross);
        Assert.Equal("LTL", rating.ContextService);
        var cost = Assert.Single(await ListCostsAsync(tenantId, billId));
        Assert.Equal(334m, cost.ExpectedAmount);
        Assert.Equal(legId, cost.TransportLegId);
    }

    [Fact]
    public async Task Ac33_SharedLegShowsOnEachBillWithoutEnteringTheTotalUntilAllocated()
    {
        var tenantId = await CreateTenantAsync();
        var billA = await CreateBillAsync(tenantId, "BL-33-A");
        var billB = await CreateBillAsync(tenantId, "BL-33-B");
        var billC = await CreateBillAsync(tenantId, "BL-33-C");
        var vendorId = await CreateVendorAsync(tenantId, "NCC-33", "Vendor 33");
        var (_, versionId) = await PublishBuyAsync(tenantId, "RC-33", vendorId, 10m);
        var legId = await LinkLegAsync(tenantId, billA, "L33", "SH-33", "HAN", "SGN");
        await PostNoContent(tenantId, $"/api/transport-legs/{legId}/bills/{billB}");
        await PostNoContent(tenantId, $"/api/transport-legs/{legId}/bills/{billC}");
        await RateAsync(tenantId, new
        {
            billId = billA,
            rateVersionId = versionId,
            quantity = 3m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });

        var source = Assert.Single(await ListCostsAsync(tenantId, null, "shared"));
        Assert.Equal(30m, source.ExpectedAmount);
        Assert.Null(source.BillId);
        Assert.Empty(await ListCostsAsync(tenantId, billA));
        Assert.Equal(0m, await ProfitCostAsync(tenantId, billA));

        var before = await FinancialViewAsync(tenantId, billB);
        var sourceRow = Assert.Single(before, c => c.AttributionType == "shared");
        Assert.False(sourceRow.IncludedInBillTotal);
        Assert.Equal(30m, sourceRow.Amount);
        Assert.Equal(legId, sourceRow.TransportLegId);
        Assert.Equal(vendorId, sourceRow.VendorPartyId);
        Assert.DoesNotContain(before, c => c.AttributionType == "allocated");

        var allocationId = await AllocateEqualAsync(tenantId, source.Id, billA, billB, billC);
        await PostNoContent(tenantId, $"/api/cost-allocations/{allocationId}/finalize");

        Assert.Equal(10m, await ProfitCostAsync(tenantId, billA));
        Assert.Equal(10m, await ProfitCostAsync(tenantId, billB));
        Assert.Equal(10m, await ProfitCostAsync(tenantId, billC));
        var after = await FinancialViewAsync(tenantId, billA);
        Assert.Contains(after, c => c.AttributionType == "shared" && c.IncludedInBillTotal == false && c.Amount == 30m);
        var share = Assert.Single(after, c => c.AttributionType == "allocated");
        Assert.True(share.IncludedInBillTotal);
        Assert.Equal(10m, share.Amount);
        Assert.Equal(legId, share.TransportLegId);
        Assert.Equal(source.Id, share.SourceCostId);
    }

    [Fact]
    public async Task Ac34_RetryIsIdempotent_RerateReplacesExpected_ConfirmedBlocksRerate()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-34");
        var vendorId = await CreateVendorAsync(tenantId, "NCC-34", "Vendor 34");
        var (_, versionId) = await PublishBuyAsync(tenantId, "RC-34", vendorId, 10m);
        var legId = await LinkLegAsync(tenantId, billId, "L34", "SH-34", "HAN", "SGN");
        var body = new
        {
            billId,
            rateVersionId = versionId,
            quantity = 2m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        };
        var first = await RateAsync(tenantId, body);
        var retry = await RateAsync(tenantId, body);
        Assert.Equal(first, retry);
        Assert.Single(await ListCostsAsync(tenantId, billId));

        using var suggest = Tenant(HttpMethod.Get, $"/api/bills/{billId}/rate-suggestions?contextType=leg&legId={legId}&vendorPartyId={vendorId}&rateVersionId={versionId}", tenantId);
        var suggestRes = await _client.SendAsync(suggest);
        suggestRes.EnsureSuccessStatusCode();
        var suggestion = await suggestRes.Content.ReadFromJsonAsync<SuggestionBody>(Json);
        Assert.Equal(first, suggestion!.ActiveRatingId);

        var changed = await ExpectConflictAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 5m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        Assert.Contains("tính lại", changed, StringComparison.OrdinalIgnoreCase);

        var second = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 5m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            supersedesRatingId = first,
            seedExpectedCosts = true
        });
        Assert.NotEqual(first, second);
        Assert.Equal("superseded", (await GetRatingAsync(tenantId, first)).Status);
        var costs = await ListCostsAsync(tenantId, billId);
        Assert.Equal(50m, Assert.Single(costs, c => c.RecordStatus == "active").ExpectedAmount);
        Assert.Equal("inactive", Assert.Single(costs, c => c.ExpectedAmount == 20m).RecordStatus);

        await PostNoContent(tenantId, $"/api/costs/{costs.Single(c => c.RecordStatus == "active").Id}/confirm");
        var locked = await ExpectConflictAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 6m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            supersedesRatingId = second,
            seedExpectedCosts = true
        });
        Assert.Contains("xác nhận", locked, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ac35_SellStaysOnTheBill_BuyDoesNotCreateActualApOrPayment()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-35");
        var vendorId = await CreateVendorAsync(tenantId, "NCC-35", "Vendor 35");
        var customerCard = await PublishBuyAsync(tenantId, "RC-SELL", null, 3m, partyType: "customer");
        var legId = await LinkLegAsync(tenantId, billId, "L35", "SH-35", "HAN", "SGN");
        var sellBlocked = await ExpectConflictAsync(tenantId, new
        {
            billId,
            rateVersionId = customerCard.VersionId,
            quantity = 1m,
            contextType = "leg",
            transportLegId = legId,
            seedExpectedRevenues = true
        });
        Assert.Contains("bảng giá mua", sellBlocked, StringComparison.OrdinalIgnoreCase);

        var sellId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = customerCard.VersionId,
            quantity = 2m,
            contextType = "bill",
            seedExpectedRevenues = true
        });
        var sell = await GetRatingAsync(tenantId, sellId);
        Assert.Equal("bill", sell.ContextType);
        Assert.Equal(6m, sell.TotalAmount);
        using var revenues = Tenant(HttpMethod.Get, $"/api/revenues?billId={billId}", tenantId);
        var revenueRes = await _client.SendAsync(revenues);
        revenueRes.EnsureSuccessStatusCode();
        using var revenueDoc = JsonDocument.Parse(await revenueRes.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, revenueDoc.RootElement.ValueKind);
        Assert.Equal(1, revenueDoc.RootElement.GetArrayLength());

        var (_, buyVersion) = await PublishBuyAsync(tenantId, "RC-BUY-35", vendorId, 4m);
        await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = buyVersion,
            quantity = 2m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        var cost = Assert.Single(await ListCostsAsync(tenantId, billId));
        Assert.Equal("expected", cost.FinancialMaturity);
        Assert.Null(cost.ConfirmedAmount);
        Assert.Null(cost.ActualAmount);
        Assert.Equal(8m, cost.ExpectedAmount);

        using var ap = Tenant(HttpMethod.Get, "/api/accounts-payable", tenantId);
        var apRes = await _client.SendAsync(ap);
        apRes.EnsureSuccessStatusCode();
        using var apDoc = JsonDocument.Parse(await apRes.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, apDoc.RootElement.ValueKind);
        Assert.Equal(0, apDoc.RootElement.GetArrayLength());

        using var payments = Tenant(HttpMethod.Get, "/api/payments", tenantId);
        var payRes = await _client.SendAsync(payments);
        payRes.EnsureSuccessStatusCode();
        using var payDoc = JsonDocument.Parse(await payRes.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, payDoc.RootElement.ValueKind);
        Assert.Equal(0, payDoc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task LegRerateWithoutQuantity_UsesTheNewLegWeight()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-RERATE");
        await EditBillAsync(tenantId, billId, new { transport_mode = "air", gross_weight_kg = "100", volume_cbm = "1" });
        var vendorId = await CreateVendorAsync(tenantId, "NCC-RR", "Vendor rerate");
        var (_, versionId) = await PublishBuyAsync(tenantId, "RC-RR", vendorId, 10m);
        var legId = await LinkLegAsync(tenantId, billId, "L-RR", "SH-RR", "HAN", "SGN");
        var first = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 2m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        await PatchObjectAsync(tenantId, "leg", legId, new
        {
            transport_mode = "air",
            gross_weight_kg = "12",
            volume_cbm = "0.2"
        });

        var second = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            supersedesRatingId = first,
            seedExpectedCosts = true
        });
        var rating = await GetRatingAsync(tenantId, second);
        Assert.Equal(33.4m, rating.Quantity);
        Assert.Equal(334m, rating.TotalAmount);
        Assert.Equal("superseded", (await GetRatingAsync(tenantId, first)).Status);
        var active = Assert.Single(await ListCostsAsync(tenantId, billId), c => c.RecordStatus == "active");
        Assert.Equal(334m, active.ExpectedAmount);
    }

    [Fact]
    public async Task LegSuggestion_UsesLegCommodity_NotTheBill()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-CMD");
        var vendorId = await CreateVendorAsync(tenantId, "NCC-CMD", "Vendor hàng");
        var generalId = await PutId(tenantId, "/api/commodities", new { code = "GEN", name = "Thường" });
        var dangerousId = await PutId(tenantId, "/api/commodities", new { code = "DG", name = "Nguy hiểm" });
        await PatchObjectAsync(tenantId, "bill", billId, new { commodity_type_id = generalId });
        var legId = await LinkLegAsync(tenantId, billId, "L-CMD", "SH-CMD", "HAN", "SGN");
        await PatchObjectAsync(tenantId, "leg", legId, new { commodity_type_id = dangerousId });
        var (_, billVersion) = await PublishBuyAsync(tenantId, "RC-GEN", vendorId, 1m, commodityCode: "GEN");
        var (_, legVersion) = await PublishBuyAsync(tenantId, "RC-DG", vendorId, 5m, commodityCode: "DG");

        using var req = Tenant(HttpMethod.Get, $"/api/bills/{billId}/rate-suggestions?contextType=leg&legId={legId}&vendorPartyId={vendorId}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<SuggestionBody>(Json);
        var selectable = body!.Buy.Options.Where(o => o.Selectable).Select(o => o.VersionId).ToList();
        Assert.Equal(legVersion, Assert.Single(selectable));
        Assert.DoesNotContain(body.Buy.Options, o => o.VersionId == billVersion && o.Selectable);

        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = legVersion,
            quantity = 2m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        Assert.Equal("DG", (await GetRatingAsync(tenantId, ratingId)).ContextCommodity);
    }

    private async Task<(Guid CardId, Guid VersionId)> PublishBuyAsync(
        Guid tenantId,
        string code,
        Guid? vendorId,
        decimal unitAmount,
        string? routeCode = null,
        string? serviceTypeCode = null,
        string partyType = "vendor",
        bool publish = true,
        string? commodityCode = null)
    {
        using var cardReq = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        cardReq.Content = JsonContent.Create(new { code, name = code, partyType, currencyCode = "VND", routeCode });
        var cardRes = await _client.SendAsync(cardReq);
        cardRes.EnsureSuccessStatusCode();
        var cardId = (await cardRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        if (vendorId is Guid vendor && partyType == "vendor")
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
            currencyCode = "VND",
            serviceTypeCode,
            commodityCode
        });
        (await _client.SendAsync(rule)).EnsureSuccessStatusCode();
        if (publish)
        {
            using var publishReq = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
            (await _client.SendAsync(publishReq)).EnsureSuccessStatusCode();
        }

        return (cardId, versionId);
    }

    private async Task<Guid> LinkLegAsync(Guid tenantId, Guid billId, string legNo, string shipmentNo, string origin, string destination)
    {
        var shipmentId = await PutId(tenantId, "/api/shipments", new
        {
            shipmentNo,
            sourceSystem = "lcms_manual",
            externalId = shipmentNo,
            originCode = origin,
            destinationCode = destination,
            isActive = true
        });
        var legId = await PutId(tenantId, "/api/transport-legs", new
        {
            legNo,
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = legNo,
            originCode = origin,
            destinationCode = destination,
            isActive = true
        });
        await PostNoContent(tenantId, $"/api/transport-legs/{legId}/bills/{billId}");
        return legId;
    }

    private async Task PatchObjectAsync(Guid tenantId, string type, Guid id, object changes)
    {
        using var req = Tenant(HttpMethod.Patch, $"/api/operational-references/{type}/{id}", tenantId);
        req.Content = JsonContent.Create(new { changes, reason = "test" });
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<string> ExpectConflictAsync(Guid tenantId, object body)
    {
        using var req = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        var err = await res.Content.ReadFromJsonAsync<ErrBody>(Json);
        return err?.Message ?? "";
    }

    private async Task<List<FinCost>> FinancialViewAsync(Guid tenantId, Guid billId)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/bills/{billId}/financial-view", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<FinView>(Json);
        return body?.Costs ?? [];
    }

    private async Task<Guid> AllocateEqualAsync(Guid tenantId, Guid costId, params Guid[] bills)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/costs/{costId}/allocations", tenantId);
        req.Content = JsonContent.Create(new
        {
            allocationBasis = "equal",
            details = bills.Select(id => new { billId = id }).ToArray()
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
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
        rating.ContextGross = doc.RootElement.TryGetProperty("gross", out var gross) && gross.ValueKind == JsonValueKind.Number
            ? gross.GetDecimal()
            : null;
        rating.ContextService = doc.RootElement.TryGetProperty("serviceType", out var service) ? service.GetString() : null;
        rating.ContextCommodity = doc.RootElement.TryGetProperty("commodityCode", out var commodity) ? commodity.GetString() : null;
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
        string? ContextJson,
        decimal Quantity = 0,
        string? Status = null)
    {
        public string? ContextOrigin { get; set; }
        public decimal? ContextGross { get; set; }
        public string? ContextService { get; set; }
        public string? ContextCommodity { get; set; }
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
        string? OriginLevel,
        Guid Id = default,
        string RecordStatus = "active",
        bool IncludedInBillTotal = true);

    private sealed record ProfitBody(List<ProfitBucket> ByCurrency);
    private sealed record ProfitBucket(decimal CostAmount);
    private sealed record SuggestionBody(
        List<LegBody> Legs,
        List<VendorBody> Vendors,
        Guid? VendorPartyId,
        SideBody Buy,
        Guid? ActiveRatingId = null);
    private sealed record LegBody(Guid Id, string ShipmentNo, string? TransportMode, string? OriginCode);
    private sealed record VendorBody(Guid Id);
    private sealed record SideBody(string Notice, List<OptionBody> Options);
    private sealed record OptionBody(Guid VersionId, bool Selectable);
    private sealed record FinView(List<FinCost> Costs);
    private sealed record FinCost(
        Guid Id,
        string AttributionType,
        decimal Amount,
        bool IncludedInBillTotal,
        Guid? TransportLegId,
        string? LegNo,
        Guid? VendorPartyId,
        Guid? SourceCostId = null);
}
