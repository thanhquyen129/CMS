using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class RateCardRemainingAcceptanceTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public RateCardRemainingAcceptanceTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Ac04_Ac05_ThreeDirectedRoutesRoundTrip_AndUnknownRouteIsRejected()
    {
        var tenantId = await CreateTenantAsync("TN-AC04");
        var sgnHan = await CreateRouteAsync(tenantId, "SGN-HAN");
        var sgnDad = await CreateRouteAsync(tenantId, "SGN-DAD");
        var hanDad = await CreateRouteAsync(tenantId, "HAN-DAD");
        var vendorId = await CreatePartyAsync(tenantId, "NCC-04", "vendor");

        var created = await DeclareAsync(tenantId, new
        {
            code = "RC-04",
            name = "Ba tuyến",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            supplierPartyId = vendorId,
            rules = new object[]
            {
                Rule("SGN-HAN", "Cước SGN-HAN", sgnHan, 10m),
                Rule("SGN-DAD", "Cước SGN-DAD", sgnDad, 20m),
                Rule("HAN-DAD", "Cước HAN-DAD", hanDad, 30m)
            }
        });
        var rules = await GetRulesAsync(tenantId, created.RateVersionId);
        Assert.Equal(3, rules.Count);
        Assert.Contains(rules, r => r.RouteId == sgnHan && r.RouteCode == "SGN-HAN" && r.AppliesAnyRoute == false);
        Assert.Contains(rules, r => r.RouteId == sgnDad && r.RouteCode == "SGN-DAD");
        Assert.Contains(rules, r => r.RouteId == hanDad && r.RouteCode == "HAN-DAD");

        using var bad = Req(HttpMethod.Post, "/api/rate-cards", tenantId);
        bad.Content = JsonContent.Create(new
        {
            code = "RC-04-BAD",
            name = "Tuyến lạ",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            supplierPartyId = vendorId,
            rules = new[] { Rule("BAD", "Tuyến không có", Guid.NewGuid(), 1m) }
        });
        var badRes = await _client.SendAsync(bad);
        var problem = await badRes.Content.ReadFromJsonAsync<ProblemBody>(Json);
        Assert.Equal(HttpStatusCode.Conflict, badRes.StatusCode);
        Assert.Equal("invalid_route", problem!.Code);
    }

    [Fact]
    public async Task Ac07_Ac08_TwoCarriersOnOneBuyCard_SaveAndRateIndependently()
    {
        var tenantId = await CreateTenantAsync("TN-AC07");
        var routeId = await CreateRouteAsync(tenantId, "SGN-HAN");
        var vendorId = await CreatePartyAsync(tenantId, "NCC-07", "vendor");
        var carrierA = await CreatePartyAsync(tenantId, "CAR-A", "carrier");
        var carrierB = await CreatePartyAsync(tenantId, "CAR-B", "carrier");
        var created = await DeclareAsync(tenantId, new
        {
            code = "RC-07",
            name = "Hai hãng",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            supplierPartyId = vendorId,
            rules = new object[]
            {
                Rule("A", "Hãng A", routeId, 10m, carrierA),
                Rule("B", "Hãng B", routeId, 40m, carrierB)
            }
        });
        await PublishAsync(tenantId, created.RateVersionId);
        var rules = await GetRulesAsync(tenantId, created.RateVersionId);
        Assert.Equal(vendorId, (await GetCardAsync(tenantId, created.RateCardId)).SupplierPartyId);
        Assert.Contains(rules, r => r.CarrierPartyId == carrierA && r.UnitAmount == 10m);
        Assert.Contains(rules, r => r.CarrierPartyId == carrierB && r.UnitAmount == 40m);
        Assert.NotEqual(vendorId, carrierA);
        Assert.NotEqual(vendorId, carrierB);

        var billA = await CreateBillAsync(tenantId, "BL-07-A");
        var billB = await CreateBillAsync(tenantId, "BL-07-B");
        var legA = await LinkLegAsync(tenantId, billA, "L-07-A", "SH-07-A", carrierA, "SGN", "HAN");
        var legB = await LinkLegAsync(tenantId, billB, "L-07-B", "SH-07-B", carrierB, "SGN", "HAN");
        await PatchAsync(tenantId, "leg", legA, new { route_code = "SGN-HAN" });
        await PatchAsync(tenantId, "leg", legB, new { route_code = "SGN-HAN" });

        var ratingA = await GetRatingAsync(tenantId, await RateAsync(tenantId, new
        {
            billId = billA,
            rateVersionId = created.RateVersionId,
            quantity = 1m,
            contextType = "leg",
            transportLegId = legA,
            vendorPartyId = vendorId
        }));
        var ratingB = await GetRatingAsync(tenantId, await RateAsync(tenantId, new
        {
            billId = billB,
            rateVersionId = created.RateVersionId,
            quantity = 1m,
            contextType = "leg",
            transportLegId = legB,
            vendorPartyId = vendorId
        }));
        Assert.Equal(10m, ratingA.TotalAmount);
        Assert.Equal(40m, ratingB.TotalAmount);
        Assert.Equal(carrierA, ratingA.Trace("carrier_party_id"));
        Assert.Equal(carrierB, ratingB.Trace("carrier_party_id"));
    }

    [Fact]
    public async Task Ac09_Ac10_TwoMatrixGroupsKeepTheirOwnRouteAndCarrier()
    {
        var tenantId = await CreateTenantAsync("TN-AC09");
        var sgnHan = await CreateRouteAsync(tenantId, "SGN-HAN");
        var hanDad = await CreateRouteAsync(tenantId, "HAN-DAD");
        var vendorId = await CreatePartyAsync(tenantId, "NCC-09", "vendor");
        var carrierA = await CreatePartyAsync(tenantId, "CAR-09A", "carrier");
        var carrierB = await CreatePartyAsync(tenantId, "CAR-09B", "carrier");
        var created = await DeclareAsync(tenantId, new
        {
            code = "RC-09",
            name = "Hai nhóm",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "advanced",
            supplierPartyId = vendorId,
            groups = new object[]
            {
                Group("G1", sgnHan, carrierA, 11m, 21m),
                Group("G2", hanDad, carrierB, 31m, 41m)
            }
        });
        var rules = await GetRulesAsync(tenantId, created.RateVersionId);
        var first = Assert.Single(rules, r => r.MatrixGroupCode == "G1");
        var second = Assert.Single(rules, r => r.MatrixGroupCode == "G2");
        Assert.Equal(sgnHan, first.RouteId);
        Assert.Equal("SGN-HAN", first.RouteCode);
        Assert.Equal(carrierA, first.CarrierPartyId);
        Assert.Equal(hanDad, second.RouteId);
        Assert.Equal("HAN-DAD", second.RouteCode);
        Assert.Equal(carrierB, second.CarrierPartyId);
        Assert.Equal(2, first.Breaks.Count);
        Assert.Equal(2, second.Breaks.Count);
        Assert.Contains(first.Breaks, b => b.MinQuantity == 0m && b.UnitAmount == 11m);
        Assert.Contains(first.Breaks, b => b.MinQuantity == 45m && b.UnitAmount == 21m);
        Assert.Contains(second.Breaks, b => b.UnitAmount == 31m);
        Assert.Contains(second.Breaks, b => b.UnitAmount == 41m);
        Assert.All(rules, r => Assert.Equal("weight_break_pivot", r.CalcMethod));
    }

    [Fact]
    public async Task Ac24_Ac25_LegRouteAndBillServiceStayOnTheirOwnContext()
    {
        var tenantId = await CreateTenantAsync("TN-AC24");
        var sgnDad = await CreateRouteAsync(tenantId, "SGN-DAD");
        var dadHan = await CreateRouteAsync(tenantId, "DAD-HAN");
        var vendorId = await CreatePartyAsync(tenantId, "NCC-24", "vendor");
        var created = await DeclareAsync(tenantId, new
        {
            code = "RC-24",
            name = "Hai chặng",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            supplierPartyId = vendorId,
            rules = new object[]
            {
                Rule("LEG1", "SGN-DAD", sgnDad, 15m, serviceTypeCode: "LTL"),
                Rule("LEG2", "DAD-HAN", dadHan, 25m, serviceTypeCode: "FCL")
            }
        });
        await PublishAsync(tenantId, created.RateVersionId);

        var billId = await CreateBillAsync(tenantId, "BL-24");
        var leg1 = await LinkLegAsync(tenantId, billId, "L-24-1", "SH-24", null, "SGN", "DAD");
        var leg2 = await LinkLegAsync(tenantId, billId, "L-24-2", "SH-24B", null, "DAD", "HAN");
        await PatchAsync(tenantId, "leg", leg1, new { route_code = "SGN-DAD", service_type_code = "LTL" });
        await PatchAsync(tenantId, "leg", leg2, new { route_code = "DAD-HAN", service_type_code = "FCL" });
        await PatchAsync(tenantId, "bill", billId, new { route_code = "SGN-DAD", service_type_code = "LTL" });

        var first = await GetRatingAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = created.RateVersionId,
            quantity = 2m,
            contextType = "leg",
            transportLegId = leg1,
            vendorPartyId = vendorId
        }));
        var second = await GetRatingAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = created.RateVersionId,
            quantity = 2m,
            contextType = "leg",
            transportLegId = leg2,
            vendorPartyId = vendorId
        }));
        Assert.Equal(30m, first.TotalAmount);
        Assert.Equal("SGN-DAD", first.TraceString("routeCode"));
        Assert.Equal(sgnDad, first.Trace("route_id"));
        Assert.Equal(50m, second.TotalAmount);
        Assert.Equal("DAD-HAN", second.TraceString("routeCode"));
        Assert.Equal(dadHan, second.Trace("route_id"));

        var billRating = await GetRatingAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = created.RateVersionId,
            quantity = 1m,
            contextType = "bill",
            vendorPartyId = vendorId
        }));
        Assert.Equal(15m, billRating.TotalAmount);
        Assert.Equal("LTL", billRating.TraceString("serviceType"));
        Assert.Equal("SGN-DAD", billRating.TraceString("routeCode"));
        Assert.Equal(sgnDad, billRating.Trace("route_id"));
    }

    [Fact]
    public async Task Ac28_ContextJsonStoresTheMatchTrace()
    {
        var tenantId = await CreateTenantAsync("TN-AC28");
        var routeId = await CreateRouteAsync(tenantId, "SGN-HAN");
        var vendorId = await CreatePartyAsync(tenantId, "NCC-28", "vendor");
        var carrierId = await CreatePartyAsync(tenantId, "CAR-28", "carrier");
        var created = await DeclareAsync(tenantId, new
        {
            code = "RC-28",
            name = "Vết tính giá",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            supplierPartyId = vendorId,
            rules = new[]
            {
                Rule("FRT", "Cước", routeId, 8m, carrierId, weightBasis: "gross", weightMin: 0m, weightMax: 100m)
            }
        });
        await PublishAsync(tenantId, created.RateVersionId);
        var rules = await GetRulesAsync(tenantId, created.RateVersionId);
        var billId = await CreateBillAsync(tenantId, "BL-28");
        var legId = await LinkLegAsync(tenantId, billId, "L-28", "SH-28", carrierId, "SGN", "HAN");
        await PatchAsync(tenantId, "leg", legId, new { route_code = "SGN-HAN", gross_weight_kg = "40" });
        var rating = await GetRatingAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = created.RateVersionId,
            quantity = 40m,
            weight = 40m,
            grossWeightKg = 40m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId
        }));
        Assert.Equal(routeId, rating.Trace("route_id"));
        Assert.Equal(carrierId, rating.Trace("carrier_party_id"));
        Assert.Equal("gross", rating.TraceString("weight_basis"));
        Assert.Equal(40m, rating.TraceDecimal("applied_weight"));
        Assert.Equal("unit_rate", rating.TraceString("formula_type"));
        Assert.Equal(rules[0].Id, rating.Trace("matched_rule_id"));
    }

    [Fact]
    public async Task Ac29_PublishedVersionRejectsPriceEdit_UntilANewDraftExists()
    {
        var tenantId = await CreateTenantAsync("TN-AC29");
        var routeId = await CreateRouteAsync(tenantId, "SGN-HAN");
        var created = await DeclareAsync(tenantId, new
        {
            code = "RC-29",
            name = "Đã phát hành",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            routeId,
            rules = new[] { Rule("FRT", "Cước", routeId, 100m) }
        });
        await PublishAsync(tenantId, created.RateVersionId);
        var card = await GetCardAsync(tenantId, created.RateCardId);
        using var edit = Req(HttpMethod.Put, $"/api/rate-cards/{created.RateCardId}", tenantId);
        edit.Content = JsonContent.Create(new
        {
            code = "RC-29",
            name = "Sửa giá",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            routeId,
            rowVersion = card.RowVersion,
            rules = new[] { Rule("FRT", "Cước", routeId, 999m) }
        });
        var editRes = await _client.SendAsync(edit);
        var problem = await editRes.Content.ReadFromJsonAsync<ProblemBody>(Json);
        Assert.Equal(HttpStatusCode.Conflict, editRes.StatusCode);
        Assert.Contains("phiên bản mới", problem!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(100m, (await GetRulesAsync(tenantId, created.RateVersionId))[0].UnitAmount);

        using var draft = Req(HttpMethod.Post, $"/api/rate-cards/{created.RateCardId}/versions", tenantId);
        draft.Content = JsonContent.Create(new { note = "bản nháp mới" });
        var draftRes = await _client.SendAsync(draft);
        var draftText = await draftRes.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, draftRes.StatusCode);
        var draftId = JsonSerializer.Deserialize<IdBody>(draftText, Json)!.Id;
        var version = await GetVersionAsync(tenantId, draftId);
        Assert.Equal("draft", version.Status);
        Assert.Equal("published", (await GetVersionAsync(tenantId, created.RateVersionId)).Status);
    }

    [Fact]
    public async Task Ac31_RerateDoesNotChangeActualCostOrPayable()
    {
        var tenantId = await CreateTenantAsync("TN-AC31");
        var routeId = await CreateRouteAsync(tenantId, "SGN-HAN");
        var vendorId = await CreatePartyAsync(tenantId, "NCC-31", "vendor");
        var created = await DeclareAsync(tenantId, new
        {
            code = "RC-31",
            name = "Giữ thực tế",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            supplierPartyId = vendorId,
            routeId,
            rules = new[] { Rule("FRT", "Cước", routeId, 80m) }
        });
        await PublishAsync(tenantId, created.RateVersionId);
        var billId = await CreateBillAsync(tenantId, "BL-31");
        var legId = await LinkLegAsync(tenantId, billId, "L-31", "SH-31", null, "SGN", "HAN");
        await PatchAsync(tenantId, "leg", legId, new { route_code = "SGN-HAN" });
        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = created.RateVersionId,
            quantity = 1m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        var cost = Assert.Single(await ListCostsAsync(tenantId, billId));
        Assert.Equal(80m, cost.ExpectedAmount);
        await PostNoContentAsync(tenantId, $"/api/costs/{cost.Id}/confirm", new { confirmedAmount = 80m }, await CostVersionAsync(tenantId, cost.Id));
        await PostNoContentAsync(tenantId, $"/api/costs/{cost.Id}/actualize", new { actualAmount = 80m }, await CostVersionAsync(tenantId, cost.Id));
        var exposureId = await PostIdAsync(tenantId, "/api/payable-exposures", new { billId, amount = 80m, currencyCode = "USD" });
        var payableId = await PostIdAsync(tenantId, $"/api/payable-exposures/{exposureId}/recognize", new { amount = 80m });
        var before = await GetCostAsync(tenantId, cost.Id);
        var payableBefore = await GetPayableAsync(tenantId, payableId);

        using var rerate = Req(HttpMethod.Post, "/api/ratings", tenantId);
        rerate.Content = JsonContent.Create(new
        {
            billId,
            rateVersionId = created.RateVersionId,
            quantity = 9m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            supersedesRatingId = ratingId,
            seedExpectedCosts = true
        });
        var rerateRes = await _client.SendAsync(rerate);
        var problem = await rerateRes.Content.ReadFromJsonAsync<ProblemBody>(Json);
        Assert.Equal(HttpStatusCode.Conflict, rerateRes.StatusCode);
        Assert.Contains("thực tế", problem!.Message, StringComparison.OrdinalIgnoreCase);

        var after = await GetCostAsync(tenantId, cost.Id);
        var payableAfter = await GetPayableAsync(tenantId, payableId);
        Assert.Equal(80m, after.ActualAmount);
        Assert.Equal(80m, after.ConfirmedAmount);
        Assert.Equal(before.ActualAmount, after.ActualAmount);
        Assert.Equal(payableBefore, payableAfter);
        Assert.Equal("completed", (await GetRatingAsync(tenantId, ratingId)).Status);
    }

    private static object Rule(
        string code,
        string name,
        Guid routeId,
        decimal unitAmount,
        Guid? carrierPartyId = null,
        string? serviceTypeCode = null,
        string? weightBasis = null,
        decimal? weightMin = null,
        decimal? weightMax = null) => new
    {
        code,
        name,
        calcMethod = "unit_rate",
        unitAmount,
        currencyCode = "USD",
        routeId,
        appliesAnyRoute = false,
        carrierPartyId,
        serviceTypeCode,
        chargeCode = "FREIGHT",
        weightBasis,
        weightMin,
        weightMax
    };

    private static object Group(string code, Guid routeId, Guid carrierPartyId, decimal first, decimal second) => new
    {
        code,
        routeId,
        appliesAnyRoute = false,
        carrierPartyId,
        columns = new[] { new { code = "GEN", name = "Hàng thường" } },
        bands = new object[]
        {
            new { minQuantity = 0m, maxQuantity = 45m, prices = new decimal?[] { first } },
            new { minQuantity = 45m, maxQuantity = (decimal?)null, prices = new decimal?[] { second } }
        }
    };

    private async Task<DeclaredCard> DeclareAsync(Guid tenantId, object body)
    {
        using var req = Req(HttpMethod.Post, "/api/rate-cards", tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<DeclaredCard>(text, Json)!;
    }

    private async Task PublishAsync(Guid tenantId, Guid versionId)
    {
        using var req = Req(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        var res = await _client.SendAsync(req);
        Assert.True(res.StatusCode == HttpStatusCode.NoContent, await res.Content.ReadAsStringAsync());
    }

    private async Task<Guid> CreateTenantAsync(string code)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(new { code, name = code })
        };
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var tenantId = (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var charge = new HttpRequestMessage(HttpMethod.Post, "/api/economic-charge-types")
        {
            Content = JsonContent.Create(new { code = "FREIGHT", name = "Cước vận tải" })
        };
        charge.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(charge)).EnsureSuccessStatusCode();
        return tenantId;
    }

    private async Task<Guid> CreateRouteAsync(Guid tenantId, string code)
    {
        var origin = await PutIdAsync(tenantId, "/api/locations", new
        {
            code = code + "-O",
            name = code + "-O",
            locationType = "airport",
            countryCode = "VN",
            isActive = true,
            aliases = Array.Empty<object>()
        });
        var destination = await PutIdAsync(tenantId, "/api/locations", new
        {
            code = code + "-D",
            name = code + "-D",
            locationType = "airport",
            countryCode = "VN",
            isActive = true,
            aliases = Array.Empty<object>()
        });
        return await PutIdAsync(tenantId, "/api/routes", new
        {
            code,
            name = code,
            originLocationId = origin,
            destinationLocationId = destination,
            isActive = true,
            intermediateLocationIds = Array.Empty<Guid>()
        });
    }

    private async Task<Guid> CreatePartyAsync(Guid tenantId, string code, string role)
    {
        using var req = Req(HttpMethod.Post, "/api/business-parties", tenantId);
        req.Content = JsonContent.Create(new { code, name = code, roleCodes = new[] { role } });
        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<IdBody>(text, Json)!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = Req(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> LinkLegAsync(
        Guid tenantId,
        Guid billId,
        string legNo,
        string shipmentNo,
        Guid? carrierPartyId,
        string origin,
        string destination)
    {
        var shipmentId = await PutIdAsync(tenantId, "/api/shipments", new
        {
            shipmentNo,
            sourceSystem = "lcms_manual",
            externalId = shipmentNo,
            originCode = origin,
            destinationCode = destination,
            carrierPartyId,
            isActive = true
        });
        var legId = await PutIdAsync(tenantId, "/api/transport-legs", new
        {
            legNo,
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = legNo,
            originCode = origin,
            destinationCode = destination,
            isActive = true
        });
        await PostNoContentAsync(tenantId, $"/api/transport-legs/{legId}/bills/{billId}", null, null);
        return legId;
    }

    private async Task PatchAsync(Guid tenantId, string type, Guid id, object changes)
    {
        using var req = Req(HttpMethod.Patch, $"/api/operational-references/{type}/{id}", tenantId);
        req.Content = JsonContent.Create(new { changes, reason = "test" });
        var res = await _client.SendAsync(req);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        using var req = Req(HttpMethod.Post, "/api/ratings", tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<IdBody>(text, Json)!.Id;
    }

    private async Task<List<RuleBody>> GetRulesAsync(Guid tenantId, Guid versionId)
    {
        using var req = Req(HttpMethod.Get, $"/api/rate-versions/{versionId}/rules", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<RuleBody>>(Json))!;
    }

    private async Task<CardBody> GetCardAsync(Guid tenantId, Guid id)
    {
        using var req = Req(HttpMethod.Get, $"/api/rate-cards/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CardBody>(Json))!;
    }

    private async Task<VersionBody> GetVersionAsync(Guid tenantId, Guid id)
    {
        using var req = Req(HttpMethod.Get, $"/api/rate-versions/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<VersionBody>(Json))!;
    }

    private async Task<RatingBody> GetRatingAsync(Guid tenantId, Guid id)
    {
        using var req = Req(HttpMethod.Get, $"/api/ratings/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var raw = await res.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<RatingBody>(raw, Json)!;
    }

    private async Task<List<CostBody>> ListCostsAsync(Guid tenantId, Guid billId)
    {
        using var req = Req(HttpMethod.Get, $"/api/costs?billId={billId}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<CostBody>>(Json))!;
    }

    private async Task<CostBody> GetCostAsync(Guid tenantId, Guid id)
    {
        using var req = Req(HttpMethod.Get, $"/api/costs/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CostBody>(Json))!;
    }

    private async Task<string> CostVersionAsync(Guid tenantId, Guid id) => (await GetCostAsync(tenantId, id)).RowVersion!;

    private async Task<decimal> GetPayableAsync(Guid tenantId, Guid id)
    {
        using var req = Req(HttpMethod.Get, $"/api/accounts-payable/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<PayableBody>(Json);
        return body!.RecognizedAmount;
    }

    private async Task<Guid> PutIdAsync(Guid tenantId, string path, object body)
    {
        using var req = Req(HttpMethod.Put, path, tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<IdBody>(text, Json)!.Id;
    }

    private async Task<Guid> PostIdAsync(Guid tenantId, string path, object body)
    {
        using var req = Req(HttpMethod.Post, path, tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<IdBody>(text, Json)!.Id;
    }

    private async Task PostNoContentAsync(Guid tenantId, string path, object? body, string? rowVersion)
    {
        using var req = Req(HttpMethod.Post, path, tenantId);
        if (rowVersion is not null)
        {
            req.Headers.TryAddWithoutValidation("If-Match", rowVersion);
        }

        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        var res = await _client.SendAsync(req);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private static HttpRequestMessage Req(HttpMethod method, string path, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record DeclaredCard(Guid Id, Guid RateCardId, Guid RateVersionId);
    private sealed record ProblemBody(string Code, string Message);
    private sealed record CardBody(Guid Id, Guid? SupplierPartyId, string? RowVersion);
    private sealed record VersionBody(Guid Id, string Status);
    private sealed record BreakBody(decimal MinQuantity, decimal? MaxQuantity, decimal UnitAmount);
    private sealed record RuleBody(
        Guid Id,
        string Code,
        string CalcMethod,
        decimal UnitAmount,
        Guid? RouteId,
        string? RouteCode,
        bool? AppliesAnyRoute,
        Guid? CarrierPartyId,
        string? MatrixGroupCode,
        List<BreakBody>? Breaks);
    private sealed record CostBody(
        Guid Id,
        decimal ExpectedAmount,
        decimal? ConfirmedAmount,
        decimal? ActualAmount,
        string? RowVersion);
    private sealed record PayableBody(decimal RecognizedAmount);

    private sealed record RatingBody(decimal TotalAmount, string? Status, string? ContextJson)
    {
        public Guid Trace(string name)
        {
            using var doc = JsonDocument.Parse(ContextJson ?? "{}");
            var value = doc.RootElement.GetProperty(name).GetString();
            return Guid.Parse(value!);
        }

        public string? TraceString(string name)
        {
            using var doc = JsonDocument.Parse(ContextJson ?? "{}");
            return doc.RootElement.GetProperty(name).GetString();
        }

        public decimal TraceDecimal(string name)
        {
            using var doc = JsonDocument.Parse(ContextJson ?? "{}");
            return doc.RootElement.GetProperty(name).GetDecimal();
        }
    }
}
