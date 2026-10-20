using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.Bills.Queries;
using LCMS.Application.Costs.Queries;
using LCMS.Application.EconomicCharges;
using LCMS.Application.OperationalReferences;
using LCMS.Application.Shipments.Queries;
using LCMS.Application.TransportLegs.Queries;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;

namespace LCMS.Api.Tests;

/// <summary>
/// Integration tests verifying the 6 TO-BE input optimization packages (OPT-01 -> OPT-06):
/// - OPT-01: Order -> Bill smart prefill & cargo rollup
/// - OPT-02: Bill -> Shipment preview rollup & route inheritance
/// - OPT-03: Shipment -> Leg -> Movement context linkage & daisy chaining
/// - OPT-04: Rate Card -> Surcharge direction & currency validation + charge mapping
/// - OPT-05: 1-Click quick actions & carrier/partner context inheritance
/// - OPT-06: Shared cost allocation basis suggestions & eligible bill loading
/// </summary>
[Collection("Api")]
public sealed class InputOptimizationInheritanceTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public InputOptimizationInheritanceTests(LcmsApiFactory factory)
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

    private static HttpRequestMessage WithTenant(HttpMethod method, string uri, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, uri);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var res = await _client.PostAsJsonAsync("/api/tenants", new { Code = code, Name = name });
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return doc.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateCustomerPartyAsync(Guid tenantId, string code, string name)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        req.Content = JsonContent.Create(new
        {
            code = code,
            name = name,
            legalName = name,
            roleCodes = new[] { "customer" }
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return doc.GetProperty("id").GetGuid();
    }


    [Fact]
    public async Task OPT01_PrefillBillFromOrders_SingleOrder_MapsAll16Fields_Successfully()
    {
        var tenantId = await CreateTenantAsync("TN-OPT01-S", "OPT-01 Single Order");
        var customerId = await CreateCustomerPartyAsync(tenantId, "CUST-OPT1-01", "Khách hàng OPT 1");

        // 1. Create Order with cargo context
        var ctx = new OperationalContextDocument
        {
            ServiceType = "EXP",
            Incoterm = "FOB",
            PackageCount = 10,
            GrossWeightKg = 150.5m,
            VolumeCbm = 1.25m,
            ChargeableWeightKg = 208.33m
        };

        using var upsertOrder = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        upsertOrder.Content = JsonContent.Create(new
        {
            OrderNo = "ORD-OPT01-001",
            SourceSystem = "tms",
            ExternalId = "ext-ord-001",
            OperationalStatus = "active",
            CustomerPartyId = customerId,
            TransportMode = "air",
            OriginCode = "SGN",
            DestinationCode = "HAN",
            RouteCode = "SGN-HAN",
            CustomerReference = "REF-PO-01",
            Context = ctx,
            ApplyContext = true
        });
        var orderRes = await _client.SendAsync(upsertOrder);
        Assert.Equal(HttpStatusCode.OK, orderRes.StatusCode);
        var orderDoc = await orderRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var orderId = orderDoc.GetProperty("id").GetGuid();

        // 2. Call Prefill Bill API
        using var prefillReq = WithTenant(HttpMethod.Post, "/api/bills/prefill-from-orders", tenantId);
        prefillReq.Content = JsonContent.Create(new { OrderIds = new[] { orderId } });
        var prefillRes = await _client.SendAsync(prefillReq);
        if (prefillRes.StatusCode != HttpStatusCode.OK) { var err = await prefillRes.Content.ReadAsStringAsync(); throw new Exception($"HTTP {prefillRes.StatusCode}: {err}"); }

        var prefill = await prefillRes.Content.ReadFromJsonAsync<BillPrefillResultDto>(JsonOptions);
        Assert.NotNull(prefill);
        Assert.Equal(customerId, prefill.CustomerPartyId);
        Assert.Equal(customerId, prefill.PayerPartyId);
        Assert.Equal(customerId, prefill.BillToPartyId);
        Assert.Equal("air", prefill.TransportMode);
        Assert.Equal("SGN", prefill.OriginCode);
        Assert.Equal("HAN", prefill.DestinationCode);
        Assert.Equal("SGN-HAN", prefill.RouteCode);
        Assert.Equal("REF-PO-01", prefill.CustomerReference);
        Assert.Equal("EXP", prefill.ServiceTypeCode);
        Assert.Equal("FOB", prefill.IncotermCode);
        Assert.Equal(10, prefill.PackageCount);
        Assert.Equal(150.5m, prefill.GrossWeightKg);
        Assert.Equal(1.25m, prefill.VolumeCbm);
        Assert.Equal(208.33m, prefill.ChargeableWeightKg);
        Assert.Empty(prefill.Conflicts);
        Assert.Contains("ORD-OPT01-001", prefill.NoticeMessage);
    }

    [Fact]
    public async Task OPT01_PrefillBillFromOrders_MultipleOrders_SumsCargoAndDetectsConflicts()
    {
        var tenantId = await CreateTenantAsync("TN-OPT01-M", "OPT-01 Multi Order");
        var customer1 = await CreateCustomerPartyAsync(tenantId, "CUST-M-01", "Khách hàng M1");
        var customer2 = await CreateCustomerPartyAsync(tenantId, "CUST-M-02", "Khách hàng M2");

        // Order 1 (SGN -> HAN)
        using var req1 = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req1.Content = JsonContent.Create(new
        {
            OrderNo = "ORD-M-001",
            SourceSystem = "tms",
            ExternalId = "ext-m-001",
            CustomerPartyId = customer1,
            TransportMode = "air",
            OriginCode = "SGN",
            DestinationCode = "HAN",
            Context = new OperationalContextDocument { PackageCount = 5, GrossWeightKg = 50m, VolumeCbm = 0.5m, ChargeableWeightKg = 83.33m },
            ApplyContext = true
        });
        var res1 = await _client.SendAsync(req1);
        var id1 = (await res1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // Order 2 (DAD -> HAN - Conflict Origin & Customer)
        using var req2 = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req2.Content = JsonContent.Create(new
        {
            OrderNo = "ORD-M-002",
            SourceSystem = "tms",
            ExternalId = "ext-m-002",
            CustomerPartyId = customer2,
            TransportMode = "air",
            OriginCode = "DAD",
            DestinationCode = "HAN",
            Context = new OperationalContextDocument { PackageCount = 10, GrossWeightKg = 120m, VolumeCbm = 1.0m, ChargeableWeightKg = 166.67m },
            ApplyContext = true
        });
        var res2 = await _client.SendAsync(req2);
        var id2 = (await res2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // Call Prefill with 2 orders
        using var prefillReq = WithTenant(HttpMethod.Post, "/api/bills/prefill-from-orders", tenantId);
        prefillReq.Content = JsonContent.Create(new { OrderIds = new[] { id1, id2 } });
        var prefillRes = await _client.SendAsync(prefillReq);
        if (prefillRes.StatusCode != HttpStatusCode.OK) { var err = await prefillRes.Content.ReadAsStringAsync(); throw new Exception($"HTTP {prefillRes.StatusCode}: {err}"); }

        var prefill = await prefillRes.Content.ReadFromJsonAsync<BillPrefillResultDto>(JsonOptions);
        Assert.NotNull(prefill);
        // SUM of cargo
        Assert.Equal(15, prefill.PackageCount);
        Assert.Equal(170m, prefill.GrossWeightKg);
        Assert.Equal(1.5m, prefill.VolumeCbm);
        Assert.Equal(250.0m, prefill.ChargeableWeightKg);
        // Conflicts detected
        Assert.NotEmpty(prefill.Conflicts);
        Assert.Contains(prefill.Conflicts, c => c.Contains("Khách hàng"));
        Assert.Contains(prefill.Conflicts, c => c.Contains("Cảng/Điểm đi"));
    }

    [Fact]
    public async Task OPT02_PreviewShipmentRollup_MultipleBills_CalculatesCorrectTotalsAndInheritsRoute()
    {
        var tenantId = await CreateTenantAsync("TN-OPT02-R", "OPT-02 Rollup");

        // Create Bill 1
        using var b1Req = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        b1Req.Content = JsonContent.Create(new
        {
            BillNo = "BL-ROLL-01",
            BillType = "house",
            SourceSystem = "ops",
            ExternalId = "ext-b-01"
        });
        var b1Res = await _client.SendAsync(b1Req);
        var b1Id = (await b1Res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var patch1 = WithTenant(HttpMethod.Patch, $"/api/bills/{b1Id}/context", tenantId);
        patch1.Content = JsonContent.Create(new
        {
            TransportMode = "sea",
            OriginCode = "VNSGN",
            DestinationCode = "USLAX",
            RouteCode = "SGN-LAX",
            Context = new OperationalContextDocument { PackageCount = 100, GrossWeightKg = 2500m, VolumeCbm = 12.5m, ChargeableWeightKg = 2500m }
        });
        await _client.SendAsync(patch1);

        // Create Bill 2
        using var b2Req = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        b2Req.Content = JsonContent.Create(new
        {
            BillNo = "BL-ROLL-02",
            BillType = "house",
            SourceSystem = "ops",
            ExternalId = "ext-b-02"
        });
        var b2Res = await _client.SendAsync(b2Req);
        var b2Id = (await b2Res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var patch2 = WithTenant(HttpMethod.Patch, $"/api/bills/{b2Id}/context", tenantId);
        patch2.Content = JsonContent.Create(new
        {
            TransportMode = "sea",
            OriginCode = "VNSGN",
            DestinationCode = "USLAX",
            RouteCode = "SGN-LAX",
            Context = new OperationalContextDocument { PackageCount = 50, GrossWeightKg = 1500m, VolumeCbm = 8.0m, ChargeableWeightKg = 1500m }
        });
        await _client.SendAsync(patch2);

        // Preview Rollup
        using var rollupReq = WithTenant(HttpMethod.Post, "/api/shipments/preview-rollup", tenantId);
        rollupReq.Content = JsonContent.Create(new { BillIds = new[] { b1Id, b2Id } });
        var rollupRes = await _client.SendAsync(rollupReq);
        if (rollupRes.StatusCode != HttpStatusCode.OK) { var err = await rollupRes.Content.ReadAsStringAsync(); throw new Exception($"HTTP {rollupRes.StatusCode}: {err}"); }

        var rollup = await rollupRes.Content.ReadFromJsonAsync<ShipmentRollupPreviewDto>(JsonOptions);
        Assert.NotNull(rollup);
        Assert.Equal(150, rollup.TotalPackageCount);
        Assert.Equal(4000m, rollup.TotalGrossWeightKg);
        Assert.Equal(20.5m, rollup.TotalVolumeCbm);
        Assert.Equal(4000m, rollup.TotalChargeableWeightKg);
        Assert.Equal("sea", rollup.TransportMode);
        Assert.Equal("VNSGN", rollup.OriginCode);
        Assert.Equal("USLAX", rollup.DestinationCode);
        Assert.Equal("SGN-LAX", rollup.RouteCode);
        Assert.Empty(rollup.Conflicts);
    }

    [Fact]
    public async Task OPT03_NextLegContext_FirstAndSubsequentLegs_AppliesDaisyChaining()
    {
        var tenantId = await CreateTenantAsync("TN-OPT03-D", "OPT-03 Daisy Chain");

        // 1. Create Shipment
        using var shpReq = WithTenant(HttpMethod.Put, "/api/shipments", tenantId);
        shpReq.Content = JsonContent.Create(new
        {
            ShipmentNo = "SHP-DAISY-01",
            SourceSystem = "ops",
            ExternalId = "ext-shp-d1",
            TransportMode = "air",
            OriginCode = "SGN",
            DestinationCode = "JFK",
            RouteCode = "SGN-JFK"
        });
        var shpRes = await _client.SendAsync(shpReq);
        var shpId = (await shpRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // 2. Query Next Leg Context before any legs exist
        using var leg1CtxReq = WithTenant(HttpMethod.Get, $"/api/shipments/{shpId}/next-leg-context", tenantId);
        var leg1CtxRes = await _client.SendAsync(leg1CtxReq);
        Assert.Equal(HttpStatusCode.OK, leg1CtxRes.StatusCode);
        var leg1Ctx = await leg1CtxRes.Content.ReadFromJsonAsync<NextLegContextDto>(JsonOptions);
        Assert.NotNull(leg1Ctx);
        Assert.Equal(1, leg1Ctx.NextSequenceNo);
        Assert.Equal("SGN", leg1Ctx.SuggestedOriginCode);
        Assert.Equal("JFK", leg1Ctx.SuggestedDestinationCode);

        // 3. Create Leg 1 (SGN -> NRT transit)
        using var leg1Upsert = WithTenant(HttpMethod.Put, "/api/transport-legs", tenantId);
        leg1Upsert.Content = JsonContent.Create(new
        {
            LegNo = "LEG-01",
            ShipmentId = shpId,
            SourceSystem = "ops",
            ExternalId = "ext-leg-1",
            SequenceNo = 1,
            OriginCode = "SGN",
            DestinationCode = "NRT",
            TransportMode = "air"
        });
        await _client.SendAsync(leg1Upsert);

        // 4. Query Next Leg Context after Leg 1 exists (Daisy Chaining!)
        using var leg2CtxReq = WithTenant(HttpMethod.Get, $"/api/shipments/{shpId}/next-leg-context", tenantId);
        var leg2CtxRes = await _client.SendAsync(leg2CtxReq);
        Assert.Equal(HttpStatusCode.OK, leg2CtxRes.StatusCode);
        var leg2Ctx = await leg2CtxRes.Content.ReadFromJsonAsync<NextLegContextDto>(JsonOptions);
        Assert.NotNull(leg2Ctx);
        Assert.Equal(2, leg2Ctx.NextSequenceNo);
        // Destination of Leg 1 becomes Origin of Leg 2!
        Assert.Equal("NRT", leg2Ctx.SuggestedOriginCode);
        Assert.Equal("JFK", leg2Ctx.SuggestedDestinationCode);
        Assert.Contains("Daisy-chaining", leg2Ctx.NoticeMessage);
    }

    [Fact]
    public async Task OPT04_Surcharge_AttachedToRateCard_ValidatesDirectionAndCurrency()
    {
        var tenantId = await CreateTenantAsync("TN-OPT04-V", "OPT-04 Surcharge Validation");

        // 1. Create Economic Charge Type & BUY RateCard (vendor, VND)
        using var ecReq = WithTenant(HttpMethod.Post, "/api/economic-charge-types", tenantId);
        ecReq.Content = JsonContent.Create(new { Code = "SUR_CHG", Name = "Phụ phí nhiên liệu" });
        var ecRes = await _client.SendAsync(ecReq);
        var ecId = (await ecRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var rcReq = WithTenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        rcReq.Content = JsonContent.Create(new
        {
            Code = "RC-BUY-VND",
            Name = "Bảng giá mua VND",
            PartyType = "vendor",
            CurrencyCode = "VND",
            TransportMode = "air"
        });
        var rcRes = await _client.SendAsync(rcReq);
        var rcId = (await rcRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // 2. Try creating Surcharge with SELL direction attached to BUY RateCard -> Must be rejected!
        using var invalidDirReq = WithTenant(HttpMethod.Post, "/api/surcharges", tenantId);
        invalidDirReq.Content = JsonContent.Create(new
        {
            Code = "SUR-SELL-ERR",
            Name = "Phụ phí sai chiều",
            Direction = "sell",
            CalculationMode = "fixed_rate",
            CurrencyCode = "VND",
            RateAmountPercent = 50000m,
            RateCardId = rcId,
            Publish = false
        });
        var invalidDirRes = await _client.SendAsync(invalidDirReq);
        Assert.Equal(HttpStatusCode.Conflict, invalidDirRes.StatusCode);

        // 3. Try creating Surcharge with USD currency attached to VND RateCard -> Must be rejected!
        using var invalidCurReq = WithTenant(HttpMethod.Post, "/api/surcharges", tenantId);
        invalidCurReq.Content = JsonContent.Create(new
        {
            Code = "SUR-CUR-ERR",
            Name = "Phụ phí sai tiền tệ",
            Direction = "buy",
            CalculationMode = "fixed_rate",
            CurrencyCode = "USD",
            RateAmountPercent = 20m,
            RateCardId = rcId,
            Publish = false
        });
        var invalidCurRes = await _client.SendAsync(invalidCurReq);
        Assert.Equal(HttpStatusCode.Conflict, invalidCurRes.StatusCode);

        // 4. Valid Surcharge matching BUY and VND -> Must succeed!
        using var validReq = WithTenant(HttpMethod.Post, "/api/surcharges", tenantId);
        validReq.Content = JsonContent.Create(new
        {
            Code = "SUR-BUY-OK",
            Name = "Phụ phí xăng dầu chuẩn",
            Direction = "buy",
            CalculationMode = "fixed_rate",
            CurrencyCode = "VND",
            RateAmountPercent = 100000m,
            RateCardId = rcId,
            Publish = false,
            EconomicChargeTypeId = ecId
        });
        var validRes = await _client.SendAsync(validReq);
        Assert.Equal(HttpStatusCode.Created, validRes.StatusCode);
    }

    [Fact]
    public async Task OPT04_EconomicCharge_AccountingMapping_ReturnsCostAndRevenueCodes()
    {
        var tenantId = await CreateTenantAsync("TN-OPT04-E", "OPT-04 Economic Mapping");

        // 1. Create Economic Charge Type
        using var ecReq = WithTenant(HttpMethod.Post, "/api/economic-charge-types", tenantId);
        ecReq.Content = JsonContent.Create(new { Code = "FUEL_SUR", Name = "Phụ phí nhiên liệu" });
        var ecRes = await _client.SendAsync(ecReq);
        var ecId = (await ecRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // 2. Map cost_type
        using var mapCost = WithTenant(HttpMethod.Post, "/api/economic-charge-types/mappings", tenantId);
        mapCost.Content = JsonContent.Create(new { SourceKind = "cost_type", SourceCode = "641_FUEL", EconomicChargeTypeId = ecId });
        await _client.SendAsync(mapCost);

        // 3. Map revenue_type
        using var mapRev = WithTenant(HttpMethod.Post, "/api/economic-charge-types/mappings", tenantId);
        mapRev.Content = JsonContent.Create(new { SourceKind = "revenue_type", SourceCode = "511_FUEL", EconomicChargeTypeId = ecId });
        await _client.SendAsync(mapRev);

        // 4. Query Accounting Mapping
        using var qReq = WithTenant(HttpMethod.Get, $"/api/economic-charge-types/{ecId}/accounting-mapping", tenantId);
        var qRes = await _client.SendAsync(qReq);
        Assert.Equal(HttpStatusCode.OK, qRes.StatusCode);

        var mapping = await qRes.Content.ReadFromJsonAsync<EconomicChargeAccountingMappingDto>(JsonOptions);
        Assert.NotNull(mapping);
        Assert.Equal("641_FUEL", mapping.CostTypeCode);
        Assert.Equal("511_FUEL", mapping.RevenueTypeCode);
        Assert.Contains("641_FUEL", mapping.NoticeMessage);
    }

    [Fact]
    public async Task OPT06_SharedCostAllocation_SuggestsCorrectBasisAndLoadsEligibleBills()
    {
        var tenantId = await CreateTenantAsync("TN-OPT06-S", "OPT-06 Shared Cost");

        // 1. Create Shipment (Air mode)
        using var shpReq = WithTenant(HttpMethod.Put, "/api/shipments", tenantId);
        shpReq.Content = JsonContent.Create(new
        {
            ShipmentNo = "SHP-AIR-01",
            SourceSystem = "ops",
            ExternalId = "ext-shp-air",
            TransportMode = "air",
            OriginCode = "SGN",
            DestinationCode = "HAN"
        });
        var shpRes = await _client.SendAsync(shpReq);
        var shpId = (await shpRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // 2. Create Bill and link to Shipment
        using var bReq = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        bReq.Content = JsonContent.Create(new
        {
            BillNo = "BL-AIR-01",
            BillType = "house",
            SourceSystem = "ops",
            ExternalId = "ext-b-air"
        });
        var bRes = await _client.SendAsync(bReq);
        var bId = (await bRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var patch = WithTenant(HttpMethod.Patch, $"/api/bills/{bId}/context", tenantId);
        patch.Content = JsonContent.Create(new
        {
            TransportMode = "air",
            Context = new OperationalContextDocument { ChargeableWeightKg = 500m, GrossWeightKg = 300m }
        });
        await _client.SendAsync(patch);

        using var link = WithTenant(HttpMethod.Post, $"/api/bills/{bId}/shipments/{shpId}", tenantId);
        await _client.SendAsync(link);

        // 3. Query Shared Cost Allocation Context
        using var allocReq = WithTenant(HttpMethod.Get, $"/api/costs/shared/allocation-context?shipmentId={shpId}", tenantId);
        var allocRes = await _client.SendAsync(allocReq);
        Assert.Equal(HttpStatusCode.OK, allocRes.StatusCode);

        var allocCtx = await allocRes.Content.ReadFromJsonAsync<SharedCostAllocationContextDto>(JsonOptions);
        Assert.NotNull(allocCtx);
        Assert.Single(allocCtx.EligibleBills);
        Assert.Equal(bId, allocCtx.EligibleBills[0].BillId);
        // For Air -> suggestedBasis is chargeable_weight!
        Assert.Equal("chargeable_weight", allocCtx.SuggestedBasis);
        Assert.Contains("Hàng không", allocCtx.BasisReason);
    }
}
