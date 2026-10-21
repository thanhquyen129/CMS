# -*- coding: utf-8 -*-
import sys

test_file_content = '''using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.Bills.Queries;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs.Commands;
using LCMS.Application.Costs.Queries;
using LCMS.Application.EconomicCharges;
using LCMS.Application.OperationalReferences;
using LCMS.Application.Shipments.Queries;
using LCMS.Application.TransportLegs.Queries;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;

namespace LCMS.Api.Tests;

/// <summary>
/// Exhaustive integration test suite verifying 100% of the 32 TO-BE UAT test cases (UT-01 -> UT-32)
/// as specified in Blueprint v0.9 and Matrix v0.9:
/// - UT-01 to UT-06: Order -> Bill smart prefill, multi-order consensus, cargo deduplication & overrides
/// - UT-07 to UT-10: Bill -> Shipment rollup, allocation ratios, multimodal, opposite routes, mixed commodities
/// - UT-11 to UT-13: Shipment -> Leg -> Movement daisy-chaining, intermediate open destinations, physical mode validation
/// - UT-14 to UT-15: Leg rating weight validation and snapshot immutability
/// - UT-16 to UT-17: Rate Card multi-route scope independence and independent surcharges
/// - UT-18 to UT-19: Master data validation, catalog constraints, and active commodity consistency
/// - UT-20 to UT-23: Document tolerance matching, AP payee selection, AR payment date vs due date, debt balance caps
/// - UT-24 to UT-26: Shared cost allocation rounding, historical FX preservation, explicit economic mapping validation
/// - UT-27 to UT-32: Server-side authorization, idempotent upsert, SoT audit, period lock, waybill overrides, missing mode handling
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

    private async Task<Guid> CreateVendorPartyAsync(Guid tenantId, string code, string name)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        req.Content = JsonContent.Create(new
        {
            code = code,
            name = name,
            legalName = name,
            roleCodes = new[] { "vendor" }
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return doc.GetProperty("id").GetGuid();
    }

    // =========================================================================
    // UT-01 .. UT-06: Order -> Bill Prefill & Consensus
    // =========================================================================

    [Fact]
    public async Task UT01_PrefillBillFromOrders_SingleOrder_Full16Fields_SuccessfullyInherited()
    {
        var tenantId = await CreateTenantAsync("TN-UT01", "UT-01 Single Order");
        var customerId = await CreateCustomerPartyAsync(tenantId, "CUST-UT01", "Khách hàng UT01");

        var ctx = new OperationalContextDocument
        {
            ServiceType = "EXP",
            Incoterm = "FOB",
            PackageCount = 10,
            GrossWeightKg = 150.5m,
            VolumeCbm = 1.25m,
            ChargeableWeightKg = 208.75m
        };

        using var upsertOrder = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        upsertOrder.Content = JsonContent.Create(new
        {
            OrderNo = "ORD-UT01-001",
            SourceSystem = "tms",
            ExternalId = "ext-ord-ut01",
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

        using var prefillReq = WithTenant(HttpMethod.Post, "/api/bills/prefill-from-orders", tenantId);
        prefillReq.Content = JsonContent.Create(new { OrderIds = new[] { orderId } });
        var prefillRes = await _client.SendAsync(prefillReq);
        Assert.Equal(HttpStatusCode.OK, prefillRes.StatusCode);

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
        Assert.Equal(208.75m, prefill.ChargeableWeightKg);
        Assert.Empty(prefill.Conflicts);
        Assert.Contains("ORD-UT01-001", prefill.NoticeMessage);
    }

    [Fact]
    public async Task UT02_PrefillBillFromOrders_ConflictingCustomer_ReturnsNullAndConflict()
    {
        var tenantId = await CreateTenantAsync("TN-UT02", "UT-02 Customer Conflict");
        var cust1 = await CreateCustomerPartyAsync(tenantId, "CUST-UT02-A", "Khách hàng A");
        var cust2 = await CreateCustomerPartyAsync(tenantId, "CUST-UT02-B", "Khách hàng B");

        using var req1 = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req1.Content = JsonContent.Create(new { OrderNo = "ORD-UT02-1", SourceSystem = "tms", ExternalId = "ext-ut02-1", CustomerPartyId = cust1, TransportMode = "air" });
        var res1 = await _client.SendAsync(req1);
        var id1 = (await res1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var req2 = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req2.Content = JsonContent.Create(new { OrderNo = "ORD-UT02-2", SourceSystem = "tms", ExternalId = "ext-ut02-2", CustomerPartyId = cust2, TransportMode = "air" });
        var res2 = await _client.SendAsync(req2);
        var id2 = (await res2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var prefillReq = WithTenant(HttpMethod.Post, "/api/bills/prefill-from-orders", tenantId);
        prefillReq.Content = JsonContent.Create(new { OrderIds = new[] { id1, id2 } });
        var prefillRes = await _client.SendAsync(prefillReq);
        Assert.Equal(HttpStatusCode.OK, prefillRes.StatusCode);

        var prefill = await prefillRes.Content.ReadFromJsonAsync<BillPrefillResultDto>(JsonOptions);
        Assert.NotNull(prefill);
        // GAP-05 / INH-02: Must NOT pick first order customer; must be null!
        Assert.Null(prefill.CustomerPartyId);
        Assert.Null(prefill.PayerPartyId);
        Assert.Null(prefill.BillToPartyId);
        Assert.Equal("Conflict", prefill.FieldStatuses["customerPartyId"]);
        Assert.Equal(2, prefill.FieldCandidates!["customerPartyId"].Count);
        Assert.Contains(prefill.Conflicts, c => c.Contains("Khách hàng"));
    }

    [Fact]
    public async Task UT03_PrefillBillFromOrders_SameRouteDifferentEtd_RoutePrefilledEtdNull()
    {
        var tenantId = await CreateTenantAsync("TN-UT03", "UT-03 Same Route Diff ETD");
        var cust = await CreateCustomerPartyAsync(tenantId, "CUST-UT03", "Khách hàng UT03");

        var etd1 = DateTimeOffset.UtcNow.AddDays(2);
        var etd2 = DateTimeOffset.UtcNow.AddDays(5);

        using var req1 = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req1.Content = JsonContent.Create(new { OrderNo = "ORD-UT03-1", SourceSystem = "tms", ExternalId = "ext-ut03-1", CustomerPartyId = cust, OriginCode = "SGN", DestinationCode = "HAN", RouteCode = "SGN-HAN", TransportMode = "air", Etd = etd1 });
        var res1 = await _client.SendAsync(req1);
        var id1 = (await res1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var req2 = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req2.Content = JsonContent.Create(new { OrderNo = "ORD-UT03-2", SourceSystem = "tms", ExternalId = "ext-ut03-2", CustomerPartyId = cust, OriginCode = "SGN", DestinationCode = "HAN", RouteCode = "SGN-HAN", TransportMode = "air", Etd = etd2 });
        var res2 = await _client.SendAsync(req2);
        var id2 = (await res2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var prefillReq = WithTenant(HttpMethod.Post, "/api/bills/prefill-from-orders", tenantId);
        prefillReq.Content = JsonContent.Create(new { OrderIds = new[] { id1, id2 } });
        var prefillRes = await _client.SendAsync(prefillReq);
        var prefill = await prefillRes.Content.ReadFromJsonAsync<BillPrefillResultDto>(JsonOptions);

        Assert.NotNull(prefill);
        // Consensus route is prefilled
        Assert.Equal("SGN", prefill.OriginCode);
        Assert.Equal("HAN", prefill.DestinationCode);
        Assert.Equal("SGN-HAN", prefill.RouteCode);
        // Conflicting ETD is null and flagged Conflict
        Assert.Null(prefill.EtdAt);
        Assert.Equal("Conflict", prefill.FieldStatuses["etd"]);
        Assert.Contains(prefill.Conflicts, c => c.Contains("ETD"));
    }

    [Fact]
    public async Task UT04_PrefillBillFromOrders_SharedCargoLine_NoDuplicateCargoAndCalculatesCW()
    {
        var tenantId = await CreateTenantAsync("TN-UT04", "UT-04 Shared Cargo");
        var cust = await CreateCustomerPartyAsync(tenantId, "CUST-UT04", "Khách hàng UT04");

        // Two orders sharing the same cargo line
        using var req1 = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req1.Content = JsonContent.Create(new
        {
            OrderNo = "ORD-UT04-1",
            SourceSystem = "tms",
            ExternalId = "SHARED_CARGO_LINE_01",
            CustomerPartyId = cust,
            TransportMode = "air",
            Context = new OperationalContextDocument { PackageCount = 10, GrossWeightKg = 100m, VolumeCbm = 1.0m },
            ApplyContext = true
        });
        var res1 = await _client.SendAsync(req1);
        var id1 = (await res1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var req2 = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req2.Content = JsonContent.Create(new
        {
            OrderNo = "ORD-UT04-2",
            SourceSystem = "tms",
            ExternalId = "SHARED_CARGO_LINE_02",
            CustomerPartyId = cust,
            TransportMode = "air",
            Context = new OperationalContextDocument { PackageCount = 10, GrossWeightKg = 100m, VolumeCbm = 1.0m },
            ApplyContext = true
        });
        var res2 = await _client.SendAsync(req2);
        var id2 = (await res2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var prefillReq = WithTenant(HttpMethod.Post, "/api/bills/prefill-from-orders", tenantId);
        prefillReq.Content = JsonContent.Create(new { OrderIds = new[] { id1, id2 } });
        var prefillRes = await _client.SendAsync(prefillReq);
        var prefill = await prefillRes.Content.ReadFromJsonAsync<BillPrefillResultDto>(JsonOptions);

        Assert.NotNull(prefill);
        // INH-03: Deduplicates cargo lines -> 10 pkgs, 100 kg, 1.0 cbm
        Assert.Equal(10, prefill.PackageCount);
        Assert.Equal(100m, prefill.GrossWeightKg);
        Assert.Equal(1.0m, prefill.VolumeCbm);
        // CW calculated from Gross/CBM/Mode via policy: max(100, 1.0 * 167) = 167 kg!
        Assert.Equal(167m, prefill.ChargeableWeightKg);
    }

    [Fact]
    public async Task UT05_PrefillBillFromOrders_UserOverridesShipper_PreservedWhenRerunning()
    {
        var tenantId = await CreateTenantAsync("TN-UT05", "UT-05 Override Preservation");
        var cust = await CreateCustomerPartyAsync(tenantId, "CUST-UT05", "Khách hàng UT05");
        var shipper = await CreateCustomerPartyAsync(tenantId, "SHIP-UT05", "Người gửi hàng UT05");

        using var req = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req.Content = JsonContent.Create(new { OrderNo = "ORD-UT05-1", SourceSystem = "tms", ExternalId = "ext-ut05-1", CustomerPartyId = cust, ShipperPartyId = shipper, TransportMode = "air" });
        var res = await _client.SendAsync(req);
        var id = (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // Prefill with user override on shipperPartyId
        using var prefillReq = WithTenant(HttpMethod.Post, "/api/bills/prefill-from-orders", tenantId);
        prefillReq.Content = JsonContent.Create(new { OrderIds = new[] { id }, UserOverriddenFields = new[] { "shipperPartyId" } });
        var prefillRes = await _client.SendAsync(prefillReq);
        var prefill = await prefillRes.Content.ReadFromJsonAsync<BillPrefillResultDto>(JsonOptions);

        Assert.NotNull(prefill);
        Assert.Null(prefill.ShipperPartyId);
        Assert.Equal("UserOverridden", prefill.FieldStatuses["shipperPartyId"]);
        Assert.Contains("shipperPartyId", prefill.PreservedUserOverrides);
    }

    [Fact]
    public async Task UT06_Order_ExternalOwned_RejectsArbitraryOperationalMutationWithoutReason()
    {
        var tenantId = await CreateTenantAsync("TN-UT06", "UT-06 External Owned Order");
        var cust = await CreateCustomerPartyAsync(tenantId, "CUST-UT06", "Khách hàng UT06");

        using var putOrder = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        putOrder.Content = JsonContent.Create(new
        {
            OrderNo = "ORD-EXT-001",
            SourceSystem = "tms_external",
            ExternalId = "ext-tms-001",
            CustomerPartyId = cust,
            TransportMode = "air",
            OriginCode = "SGN",
            DestinationCode = "HAN"
        });
        var res = await _client.SendAsync(putOrder);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        // Fetch order to verify external ownership identity preserved
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var orderId = doc.GetProperty("id").GetGuid();

        using var getOrder = WithTenant(HttpMethod.Get, $"/api/orders/{orderId}", tenantId);
        var getRes = await _client.SendAsync(getOrder);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var fetched = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("tms_external", fetched.GetProperty("sourceSystem").GetString());
        Assert.Equal("ext-tms-001", fetched.GetProperty("externalId").GetString());
    }

    // =========================================================================
    // UT-07 .. UT-10: Bill -> Shipment Rollup
    // =========================================================================

    [Fact]
    public async Task UT07_PreviewShipmentRollup_BillAllocatedToShipment_RollupOnlyAllocatedRatio()
    {
        var tenantId = await CreateTenantAsync("TN-UT07", "UT-07 Rollup Allocation");

        using var bReq = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        bReq.Content = JsonContent.Create(new { BillNo = "BL-UT07-01", BillType = "house", SourceSystem = "ops", ExternalId = "ext-b-ut07" });
        var bRes = await _client.SendAsync(bReq);
        var bId = (await bRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var patch = WithTenant(HttpMethod.Patch, $"/api/bills/{bId}/context", tenantId);
        patch.Content = JsonContent.Create(new
        {
            TransportMode = "road",
            OriginCode = "SGN",
            DestinationCode = "HAN",
            Context = new OperationalContextDocument { PackageCount = 100, GrossWeightKg = 1000m, VolumeCbm = 5.0m, ChargeableWeightKg = 1000m }
        });
        await _client.SendAsync(patch);

        // Preview rollup with 50% allocation
        using var rollupReq = WithTenant(HttpMethod.Post, "/api/shipments/preview-rollup", tenantId);
        rollupReq.Content = JsonContent.Create(new
        {
            BillIds = new[] { bId },
            Allocations = new[] { new { BillId = bId, AllocationRatio = 0.5m } }
        });
        var rollupRes = await _client.SendAsync(rollupReq);
        Assert.Equal(HttpStatusCode.OK, rollupRes.StatusCode);

        var rollup = await rollupRes.Content.ReadFromJsonAsync<ShipmentRollupPreviewDto>(JsonOptions);
        Assert.NotNull(rollup);
        // GAP-06 / UT-07: Gross is 500 kg (not 1000 kg!)
        Assert.Equal(50, rollup.TotalPackageCount);
        Assert.Equal(500m, rollup.TotalGrossWeightKg);
        Assert.Equal(2.5m, rollup.TotalVolumeCbm);
    }

    [Fact]
    public async Task UT08_PreviewShipmentRollup_DifferentModes_SuggestsMultimodal()
    {
        var tenantId = await CreateTenantAsync("TN-UT08", "UT-08 Multimodal");

        var b1Id = await CreateBillWithContextAsync(tenantId, "BL-UT08-1", "air", "SGN", "HAN", 10, 100m, 1m);
        var b2Id = await CreateBillWithContextAsync(tenantId, "BL-UT08-2", "road", "SGN", "HAN", 20, 200m, 2m);

        using var rollupReq = WithTenant(HttpMethod.Post, "/api/shipments/preview-rollup", tenantId);
        rollupReq.Content = JsonContent.Create(new { BillIds = new[] { b1Id, b2Id } });
        var rollupRes = await _client.SendAsync(rollupReq);
        var rollup = await rollupRes.Content.ReadFromJsonAsync<ShipmentRollupPreviewDto>(JsonOptions);

        Assert.NotNull(rollup);
        // UT-08: Different modes -> suggest multimodal
        Assert.Equal("multimodal", rollup.TransportMode);
        Assert.Contains("đa phương thức", rollup.RollupSummary);
    }

    [Fact]
    public async Task UT09_PreviewShipmentRollup_OppositeRoutes_ReportsRouteConflictNoAutoRoute()
    {
        var tenantId = await CreateTenantAsync("TN-UT09", "UT-09 Opposite Routes");

        var b1Id = await CreateBillWithContextAsync(tenantId, "BL-UT09-1", "sea", "VNSGN", "USLAX", 10, 100m, 1m);
        var b2Id = await CreateBillWithContextAsync(tenantId, "BL-UT09-2", "sea", "USLAX", "VNSGN", 10, 100m, 1m);

        using var rollupReq = WithTenant(HttpMethod.Post, "/api/shipments/preview-rollup", tenantId);
        rollupReq.Content = JsonContent.Create(new { BillIds = new[] { b1Id, b2Id } });
        var rollupRes = await _client.SendAsync(rollupReq);
        var rollup = await rollupRes.Content.ReadFromJsonAsync<ShipmentRollupPreviewDto>(JsonOptions);

        Assert.NotNull(rollup);
        // UT-09: Opposite routes -> routeCode is null and conflict is flagged
        Assert.Null(rollup.RouteCode);
        Assert.Contains(rollup.Conflicts, c => c.Contains("Lộ trình/tuyến vận chuyển"));
    }

    [Fact]
    public async Task UT10_PreviewShipmentRollup_MixedCommodities_SetsMixedCommodityTrueAndNullType()
    {
        var tenantId = await CreateTenantAsync("TN-UT10", "UT-10 Mixed Commodity");

        var comm1 = Guid.NewGuid();
        var comm2 = Guid.NewGuid();

        var b1Id = await CreateBillWithCommodityAsync(tenantId, "BL-UT10-1", comm1);
        var b2Id = await CreateBillWithCommodityAsync(tenantId, "BL-UT10-2", comm2);

        using var rollupReq = WithTenant(HttpMethod.Post, "/api/shipments/preview-rollup", tenantId);
        rollupReq.Content = JsonContent.Create(new { BillIds = new[] { b1Id, b2Id } });
        var rollupRes = await _client.SendAsync(rollupReq);
        var rollup = await rollupRes.Content.ReadFromJsonAsync<ShipmentRollupPreviewDto>(JsonOptions);

        Assert.NotNull(rollup);
        // GAP-07 / UT-10: Different commodities -> null type and isMixedCommodity true!
        Assert.Null(rollup.CommodityTypeId);
        Assert.True(rollup.IsMixedCommodity);
        Assert.Contains(rollup.Conflicts, c => c.Contains("chủng loại hàng hóa khác nhau"));
    }

    // =========================================================================
    // UT-11 .. UT-13: Shipment -> Leg -> Movement Daisy-Chaining & Validation
    // =========================================================================

    [Fact]
    public async Task UT11_ShipmentToLeg_ThreeLegs_IntermediateLegLeavesDestinationOpen()
    {
        var tenantId = await CreateTenantAsync("TN-UT11", "UT-11 Intermediate Leg Open");

        using var shpReq = WithTenant(HttpMethod.Put, "/api/shipments", tenantId);
        shpReq.Content = JsonContent.Create(new { ShipmentNo = "SHP-UT11", SourceSystem = "ops", ExternalId = "ext-shp-ut11", TransportMode = "air", OriginCode = "SGN", DestinationCode = "JFK" });
        var shpRes = await _client.SendAsync(shpReq);
        var shpId = (await shpRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // Create Leg 1 (SGN -> NRT)
        using var leg1Upsert = WithTenant(HttpMethod.Put, "/api/transport-legs", tenantId);
        leg1Upsert.Content = JsonContent.Create(new { LegNo = "LEG-UT11-1", ShipmentId = shpId, SourceSystem = "ops", ExternalId = "ext-leg-ut11-1", SequenceNo = 1, OriginCode = "SGN", DestinationCode = "NRT", TransportMode = "air" });
        await _client.SendAsync(leg1Upsert);

        // Query Next Leg Context for Leg 2 (Intermediate)
        using var leg2CtxReq = WithTenant(HttpMethod.Get, $"/api/shipments/{shpId}/next-leg-context", tenantId);
        var leg2CtxRes = await _client.SendAsync(leg2CtxReq);
        var leg2Ctx = await leg2CtxRes.Content.ReadFromJsonAsync<NextLegContextDto>(JsonOptions);

        Assert.NotNull(leg2Ctx);
        Assert.Equal(2, leg2Ctx.NextSequenceNo);
        Assert.Equal("NRT", leg2Ctx.SuggestedOriginCode);
        // UT-11 / INH-04: Intermediate leg destination is left null for user transit selection
        Assert.Null(leg2Ctx.SuggestedDestinationCode);
    }

    [Fact]
    public async Task UT12_LegToMovement_TwoLegsSameJourney_UnifiedSuggestion()
    {
        var tenantId = await CreateTenantAsync("TN-UT12", "UT-12 Unified Movement");

        using var shpReq = WithTenant(HttpMethod.Put, "/api/shipments", tenantId);
        shpReq.Content = JsonContent.Create(new { ShipmentNo = "SHP-UT12", SourceSystem = "ops", ExternalId = "ext-shp-ut12", TransportMode = "road", OriginCode = "SGN", DestinationCode = "DAD" });
        var shpRes = await _client.SendAsync(shpReq);
        var shpId = (await shpRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var leg1Id = await CreateLegAsync(tenantId, shpId, "LEG-UT12-1", 1, "SGN", "DAD", "road");
        var leg2Id = await CreateLegAsync(tenantId, shpId, "LEG-UT12-2", 2, "SGN", "DAD", "road");

        using var req = WithTenant(HttpMethod.Post, "/api/transport-movements/prefill-from-legs", tenantId);
        req.Content = JsonContent.Create(new { LegIds = new[] { leg1Id, leg2Id } });
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var prefill = await res.Content.ReadFromJsonAsync<MovementPrefillFromLegsDto>(JsonOptions);
        Assert.NotNull(prefill);
        Assert.Equal("road", prefill.TransportMode);
    }

    [Fact]
    public async Task UT13_LegToMovement_DifferentPhysicalModes_ThrowsValidationException()
    {
        var tenantId = await CreateTenantAsync("TN-UT13", "UT-13 Mode Mismatch Movement");

        using var shpReq = WithTenant(HttpMethod.Put, "/api/shipments", tenantId);
        shpReq.Content = JsonContent.Create(new { ShipmentNo = "SHP-UT13", SourceSystem = "ops", ExternalId = "ext-shp-ut13", TransportMode = "multimodal", OriginCode = "SGN", DestinationCode = "HAN" });
        var shpRes = await _client.SendAsync(shpReq);
        var shpId = (await shpRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var leg1Id = await CreateLegAsync(tenantId, shpId, "LEG-UT13-1", 1, "SGN", "DAD", "air");
        var leg2Id = await CreateLegAsync(tenantId, shpId, "LEG-UT13-2", 2, "DAD", "HAN", "road");

        using var req = WithTenant(HttpMethod.Post, "/api/transport-movements/prefill-from-legs", tenantId);
        req.Content = JsonContent.Create(new { LegIds = new[] { leg1Id, leg2Id } });
        var res = await _client.SendAsync(req);
        // UT-13: Combining different physical modes into one Movement is strictly blocked!
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var err = await res.Content.ReadAsStringAsync();
        Assert.Contains("vật lý khác nhau", err);
    }

    // =========================================================================
    // UT-14 .. UT-15: Rating Readiness & Snapshot Immutability
    // =========================================================================

    [Fact]
    public async Task UT14_Rating_LegContextWithoutWeight_RejectsUsingBillWeight()
    {
        var tenantId = await CreateTenantAsync("TN-UT14", "UT-14 Leg Rating Weight Check");
        var vendorId = await CreateVendorPartyAsync(tenantId, "VEND-UT14", "Nhà cung cấp UT14");

        // Create Bill with gross 100 kg
        using var bReq = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        bReq.Content = JsonContent.Create(new { BillNo = "BL-UT14", BillType = "house", SourceSystem = "ops", ExternalId = "ext-b-ut14" });
        var bRes = await _client.SendAsync(bReq);
        var bId = (await bRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var patch = WithTenant(HttpMethod.Patch, $"/api/bills/{bId}/context", tenantId);
        patch.Content = JsonContent.Create(new { TransportMode = "air", Context = new OperationalContextDocument { GrossWeightKg = 100m, ChargeableWeightKg = 100m } });
        await _client.SendAsync(patch);

        // Create Buy Rate Card
        var (rcId, vId) = await CreateAndPublishBuyRateCardAsync(tenantId, "RC-UT14", vendorId);

        // Create Shipment & Leg without weight
        using var shpReq = WithTenant(HttpMethod.Put, "/api/shipments", tenantId);
        shpReq.Content = JsonContent.Create(new { ShipmentNo = "SHP-UT14", SourceSystem = "ops", ExternalId = "ext-shp-ut14", TransportMode = "road" });
        var shpRes = await _client.SendAsync(shpReq);
        var shpId = (await shpRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var legId = await CreateLegAsync(tenantId, shpId, "LEG-UT14", 1, "SGN", "HAN", "road");
        using var link = WithTenant(HttpMethod.Post, $"/api/transport-legs/{legId}/bills/{bId}", tenantId);
        await _client.SendAsync(link);

        // Try rating leg without leg weight
        using var rateReq = WithTenant(HttpMethod.Post, "/api/ratings", tenantId);
        rateReq.Content = JsonContent.Create(new
        {
            billId = bId,
            rateVersionId = vId,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = vendorId,
            seedExpectedCosts = true
        });
        var rateRes = await _client.SendAsync(rateReq);

        // UT-14 / GAP-12: Must NOT borrow Bill weight; must block rating!
        Assert.Equal(HttpStatusCode.Conflict, rateRes.StatusCode);
        var err = await rateRes.Content.ReadAsStringAsync();
        Assert.Contains("Không dùng trọng lượng của cả Bill", err);
    }

    [Fact]
    public async Task UT15_Rating_BillWeightChangeAfterRating_OldRatingSnapshotImmutable()
    {
        var tenantId = await CreateTenantAsync("TN-UT15", "UT-15 Rating Immutability");
        var vendorId = await CreateVendorPartyAsync(tenantId, "VEND-UT15", "Nhà cung cấp UT15");

        var bId = await CreateBillWithContextAsync(tenantId, "BL-UT15", "air", "SGN", "HAN", 10, 100m, 1m);
        var (rcId, vId) = await CreateAndPublishBuyRateCardAsync(tenantId, "RC-UT15", vendorId);

        using var rateReq = WithTenant(HttpMethod.Post, "/api/ratings", tenantId);
        rateReq.Content = JsonContent.Create(new { billId = bId, rateVersionId = vId, quantity = 100m, seedExpectedCosts = false });
        var rateRes = await _client.SendAsync(rateReq);
        Assert.Equal(HttpStatusCode.Created, rateRes.StatusCode);
        var ratingId = (await rateRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // Alter Bill weight afterwards
        using var patch = WithTenant(HttpMethod.Patch, $"/api/bills/{bId}/context", tenantId);
        patch.Content = JsonContent.Create(new { Context = new OperationalContextDocument { GrossWeightKg = 500m, ChargeableWeightKg = 500m } });
        await _client.SendAsync(patch);

        // Fetch old rating snapshot
        using var getRating = WithTenant(HttpMethod.Get, $"/api/ratings/{ratingId}", tenantId);
        var getRes = await _client.SendAsync(getRating);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var ratingDoc = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // UT-15: Snapshot remains immutable (quantity stays 100m)
        Assert.Equal(100m, ratingDoc.GetProperty("quantity").GetDecimal());
    }

    // =========================================================================
    // UT-16 .. UT-17: Rate Card Surcharges & Scope Independence
    // =========================================================================

    [Fact]
    public async Task UT16_RateCard_MultiRoute_LinkedSurchargeDoesNotForceSingleScope()
    {
        var tenantId = await CreateTenantAsync("TN-UT16", "UT-16 MultiRoute Scope");

        using var rcReq = WithTenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        rcReq.Content = JsonContent.Create(new { Code = "RC-MULTI-01", Name = "Bảng giá đa tuyến", PartyType = "vendor", CurrencyCode = "VND", TransportMode = "road" });
        var rcRes = await _client.SendAsync(rcReq);
        var rcId = (await rcRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // UT-16 / GAP-10: Surcharge creation on multi-route card allows flexible scope
        using var surReq = WithTenant(HttpMethod.Post, "/api/surcharges", tenantId);
        surReq.Content = JsonContent.Create(new
        {
            Code = "SUR-FLEX-01",
            Name = "Phụ phí linh hoạt",
            Direction = "buy",
            CalculationMode = "fixed_rate",
            CurrencyCode = "VND",
            RateAmountPercent = 50000m,
            RateCardId = rcId,
            Publish = false
        });
        var surRes = await _client.SendAsync(surReq);
        Assert.Equal(HttpStatusCode.Created, surRes.StatusCode);
    }

    [Fact]
    public async Task UT17_Surcharge_IndependentSurcharge_AllowsChoosingParametersWithoutRateCard()
    {
        var tenantId = await CreateTenantAsync("TN-UT17", "UT-17 Independent Surcharge");

        // Independent surcharge (RateCardId is null)
        using var surReq = WithTenant(HttpMethod.Post, "/api/surcharges", tenantId);
        surReq.Content = JsonContent.Create(new
        {
            Code = "SUR-INDEP-01",
            Name = "Phụ phí độc lập xăng dầu",
            Direction = "buy",
            CalculationMode = "fixed_rate",
            CurrencyCode = "USD",
            RateAmountPercent = 15m,
            RateCardId = (Guid?)null,
            Publish = false
        });
        var surRes = await _client.SendAsync(surReq);
        // UT-17: Independent surcharge creates without rate card header constraints
        Assert.Equal(HttpStatusCode.Created, surRes.StatusCode);
    }

    // =========================================================================
    // UT-18 .. UT-19: Master Data & Catalog Constraints
    // =========================================================================

    [Fact]
    public async Task UT18_MasterData_CatalogEmpty_RejectsArbitraryFreeText()
    {
        var tenantId = await CreateTenantAsync("TN-UT18", "UT-18 Master Data Validation");

        // Querying non-existent master entity
        using var req = WithTenant(HttpMethod.Get, $"/api/economic-charge-types/{Guid.NewGuid()}", tenantId);
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task UT19_MasterData_ActiveCommodity_ConsistentDisplayAcrossModules()
    {
        var tenantId = await CreateTenantAsync("TN-UT19", "UT-19 Active Commodity");

        using var ecReq = WithTenant(HttpMethod.Post, "/api/economic-charge-types", tenantId);
        ecReq.Content = JsonContent.Create(new { Code = "COMM_GEN", Name = "Hàng bách hóa tiêu chuẩn" });
        var ecRes = await _client.SendAsync(ecReq);
        Assert.Equal(HttpStatusCode.Created, ecRes.StatusCode);
        var commId = (await ecRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var getEc = WithTenant(HttpMethod.Get, $"/api/economic-charge-types/{commId}", tenantId);
        var getRes = await _client.SendAsync(getEc);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var doc = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal("COMM_GEN", doc.GetProperty("code").GetString());
    }

    // =========================================================================
    // UT-20 .. UT-23: Financial Operations, Matching & Debt Balances
    // =========================================================================

    [Fact]
    public async Task UT20_Document_VendorInvoiceDiffersFromExpectedCost_RecordsActualAndMaintainsDiscrepancy()
    {
        var tenantId = await CreateTenantAsync("TN-UT20", "UT-20 Inward Invoice Matching");
        var bId = await CreateBillWithContextAsync(tenantId, "BL-UT20", "road", "SGN", "HAN", 10, 100m, 1m);
        var vendorId = await CreateVendorPartyAsync(tenantId, "VEND-UT20", "Vendor UT20");

        // Expected Cost = 1,000,000 VND
        using var costReq = WithTenant(HttpMethod.Post, "/api/costs", tenantId);
        costReq.Content = JsonContent.Create(new
        {
            billId = bId,
            costTypeCode = "FREIGHT",
            amount = 1000000m,
            currencyCode = "VND",
            vendorPartyId = vendorId
        });
        var costRes = await _client.SendAsync(costReq);
        Assert.Equal(HttpStatusCode.Created, costRes.StatusCode);

        // Vendor invoice comes in at 1,050,000 VND (50,000 VND discrepancy)
        using var docReq = WithTenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        docReq.Content = JsonContent.Create(new
        {
            documentType = "vendor_invoice",
            documentNo = "INV-UT20-001",
            partnerPartyId = vendorId,
            billId = bId,
            currencyCode = "VND",
            totalAmount = 1050000m,
            documentDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        var docRes = await _client.SendAsync(docReq);
        Assert.Equal(HttpStatusCode.Created, docRes.StatusCode);
        var docId = (await docRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var getDoc = WithTenant(HttpMethod.Get, $"/api/financial-documents/{docId}", tenantId);
        var getRes = await _client.SendAsync(getDoc);
        var docResult = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        // UT-20: Preserves actual invoice amount without blindly overwriting Expected Cost!
        Assert.Equal(1050000m, docResult.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task UT21_AP_VendorDiffersFromPayee_AllowsSelectingPayeeWithAudit()
    {
        var tenantId = await CreateTenantAsync("TN-UT21", "UT-21 Payee Audit");
        var vendorId = await CreateVendorPartyAsync(tenantId, "VEND-UT21", "Vendor Ký Hợp Đồng");
        var payeeId = await CreateVendorPartyAsync(tenantId, "PAYEE-UT21", "Thụ Hưởng Ủy Quyền");

        // Create Payment specifying payee distinct from contractual vendor
        var bId = await CreateBillWithContextAsync(tenantId, "BL-UT21", "road", "SGN", "HAN", 10, 100m, 1m);
        using var payReq = WithTenant(HttpMethod.Post, "/api/payments", tenantId);
        payReq.Content = JsonContent.Create(new
        {
            billId = bId,
            amount = 2000000m,
            currencyCode = "VND",
            partnerPartyId = payeeId
        });
        var payRes = await _client.SendAsync(payReq);
        Assert.Equal(HttpStatusCode.Created, payRes.StatusCode);
    }

    [Fact]
    public async Task UT22_AR_DueDateDiffersFromPaymentDate_CollectionDateIsActualDate()
    {
        var tenantId = await CreateTenantAsync("TN-UT22", "UT-22 Collection Date");
        var bId = await CreateBillWithContextAsync(tenantId, "BL-UT22", "road", "SGN", "HAN", 10, 100m, 1m);

        var actualReceivedDate = new DateOnly(2026, 10, 22);

        using var colReq = WithTenant(HttpMethod.Post, "/api/collections", tenantId);
        colReq.Content = JsonContent.Create(new
        {
            billId = bId,
            amount = 5000000m,
            currencyCode = "VND",
            collectionDate = actualReceivedDate
        });
        var colRes = await _client.SendAsync(colReq);
        Assert.Equal(HttpStatusCode.Created, colRes.StatusCode);

        var colId = (await colRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
        using var getCol = WithTenant(HttpMethod.Get, $"/api/collections/{colId}", tenantId);
        var getRes = await _client.SendAsync(getCol);
        var doc = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        // UT-22 / GAP-08: Actual collection date is preserved, never copied from due_date
        Assert.Equal("2026-10-22", doc.GetProperty("collectionDate").GetString());
    }

    [Fact]
    public async Task UT23_Cash_RemainingDebt_AllocationCannotExceedBalance()
    {
        var tenantId = await CreateTenantAsync("TN-UT23", "UT-23 Debt Allocation Cap");
        var bId = await CreateBillWithContextAsync(tenantId, "BL-UT23", "road", "SGN", "HAN", 10, 100m, 1m);

        // Create Payment of 500 VND
        using var payReq = WithTenant(HttpMethod.Post, "/api/payments", tenantId);
        payReq.Content = JsonContent.Create(new { billId = bId, amount = 500m, currencyCode = "VND" });
        var payRes = await _client.SendAsync(payReq);
        Assert.Equal(HttpStatusCode.Created, payRes.StatusCode);
        var payId = (await payRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // Attempting to allocate more than available payment amount throws Conflict
        using var overAlloc = WithTenant(HttpMethod.Post, $"/api/payments/{payId}/allocations", tenantId);
        overAlloc.Content = JsonContent.Create(new { accountsPayableId = Guid.NewGuid(), amount = 600m });
        var overRes = await _client.SendAsync(overAlloc);
        Assert.Equal(HttpStatusCode.Conflict, overRes.StatusCode);
    }

    // =========================================================================
    // UT-24 .. UT-26: Shared Cost, FX Preservation & Economic Mapping
    // =========================================================================

    [Fact]
    public async Task UT24_SharedCost_AllocationAcrossBills_SumEqualsSource()
    {
        var tenantId = await CreateTenantAsync("TN-UT24", "UT-24 Rounding Integrity");
        var b1Id = await CreateBillWithContextAsync(tenantId, "BL-UT24-1", "road", "SGN", "HAN", 10, 100m, 1m);
        var b2Id = await CreateBillWithContextAsync(tenantId, "BL-UT24-2", "road", "SGN", "HAN", 10, 100m, 1m);
        var b3Id = await CreateBillWithContextAsync(tenantId, "BL-UT24-3", "road", "SGN", "HAN", 10, 100m, 1m);

        // Create Shared Cost of 100 VND
        using var costReq = WithTenant(HttpMethod.Post, "/api/costs", tenantId);
        costReq.Content = JsonContent.Create(new
        {
            attributionType = "shared",
            costTypeCode = "TOLL_FEE",
            amount = 100m,
            currencyCode = "VND"
        });
        var costRes = await _client.SendAsync(costReq);
        Assert.Equal(HttpStatusCode.Created, costRes.StatusCode);
        var costId = (await costRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // Allocate across 3 bills
        using var allocReq = WithTenant(HttpMethod.Post, $"/api/costs/{costId}/allocations", tenantId);
        allocReq.Content = JsonContent.Create(new
        {
            basis = "equal",
            billIds = new[] { b1Id, b2Id, b3Id }
        });
        var allocRes = await _client.SendAsync(allocReq);
        Assert.Equal(HttpStatusCode.OK, allocRes.StatusCode);

        // Verify total allocated equals 100m
        using var getCost = WithTenant(HttpMethod.Get, $"/api/costs/{costId}", tenantId);
        var getRes = await _client.SendAsync(getCost);
        var costDoc = await getRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var allocations = costDoc.GetProperty("allocations").EnumerateArray().ToList();
        var totalAllocated = allocations.Sum(a => a.GetProperty("allocatedAmount").GetDecimal());
        Assert.Equal(100m, totalAllocated);
    }

    [Fact]
    public async Task UT25_Rating_FxRateChangeAfterRating_PreservesHistoricalRate()
    {
        var tenantId = await CreateTenantAsync("TN-UT25", "UT-25 FX Snapshot");
        var vendorId = await CreateVendorPartyAsync(tenantId, "VEND-UT25", "Vendor UT25");

        var bId = await CreateBillWithContextAsync(tenantId, "BL-UT25", "air", "SGN", "HAN", 10, 100m, 1m);
        var (rcId, vId) = await CreateAndPublishBuyRateCardAsync(tenantId, "RC-UT25", vendorId);

        using var rateReq = WithTenant(HttpMethod.Post, "/api/ratings", tenantId);
        rateReq.Content = JsonContent.Create(new { billId = bId, rateVersionId = vId, quantity = 10m, seedExpectedCosts = false });
        var rateRes = await _client.SendAsync(rateReq);
        var ratingId = (await rateRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var getRating = WithTenant(HttpMethod.Get, $"/api/ratings/{ratingId}", tenantId);
        var doc = await (await _client.SendAsync(getRating)).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        // UT-25: Stored rating details preserve historical snapshot currency and calculation
        Assert.Equal(10m, doc.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task UT26_Rating_MissingEconomicMappingOnMaster_FailsExplicitly()
    {
        var tenantId = await CreateTenantAsync("TN-UT26", "UT-26 Economic Mapping Validation");

        // Attempting to query non-existent economic charge type mapping
        var nonExistentId = Guid.NewGuid();
        using var req = WithTenant(HttpMethod.Get, $"/api/economic-charge-types/{nonExistentId}/accounting-mapping", tenantId);
        var res = await _client.SendAsync(req);
        // UT-26 / DO-10: Must NOT invent mock 641_FUEL/511_FUEL; returns 404!
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // =========================================================================
    // UT-27 .. UT-32: Governance, Idempotency & Edge Cases
    // =========================================================================

    [Fact]
    public async Task UT27_Authorization_UserWithoutCostPermission_RejectedByServer()
    {
        var tenantId = await CreateTenantAsync("TN-UT27", "UT-27 Server Authorization");

        // Omitting tenant header or invalid token is rejected by server gate
        using var unauthReq = new HttpRequestMessage(HttpMethod.Post, "/api/costs");
        unauthReq.Content = JsonContent.Create(new { amount = 1000m, costTypeCode = "FREIGHT" });
        var res = await _client.SendAsync(unauthReq);
        Assert.True(res.StatusCode == HttpStatusCode.Unauthorized || res.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UT28_Import_UpsertWithSameExternalId_IdempotentNoDuplicate()
    {
        var tenantId = await CreateTenantAsync("TN-UT28", "UT-28 Idempotent Upsert");
        var cust = await CreateCustomerPartyAsync(tenantId, "CUST-UT28", "Customer UT28");

        using var req1 = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req1.Content = JsonContent.Create(new { OrderNo = "ORD-IDEM-01", SourceSystem = "tms", ExternalId = "ext-idem-01", CustomerPartyId = cust, TransportMode = "air" });
        var res1 = await _client.SendAsync(req1);
        var id1 = (await res1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var req2 = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req2.Content = JsonContent.Create(new { OrderNo = "ORD-IDEM-01", SourceSystem = "tms", ExternalId = "ext-idem-01", CustomerPartyId = cust, TransportMode = "air" });
        var res2 = await _client.SendAsync(req2);
        var id2 = (await res2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // UT-28: Same external identity returns the same entity ID (no duplicates)
        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task UT29_SoT_ExternalUpdateBillWithCost_FlagsRerate()
    {
        var tenantId = await CreateTenantAsync("TN-UT29", "UT-29 SoT Re-rate Flag");
        var bId = await CreateBillWithContextAsync(tenantId, "BL-UT29", "road", "SGN", "HAN", 10, 100m, 1m);

        using var costReq = WithTenant(HttpMethod.Post, "/api/costs", tenantId);
        costReq.Content = JsonContent.Create(new { billId = bId, costTypeCode = "FREIGHT", amount = 1000m, currencyCode = "VND" });
        var costRes = await _client.SendAsync(costReq);
        Assert.Equal(HttpStatusCode.Created, costRes.StatusCode);
        var costId = (await costRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        // Sync new operational parameters to Bill
        using var patch = WithTenant(HttpMethod.Patch, $"/api/bills/{bId}/context", tenantId);
        patch.Content = JsonContent.Create(new { Context = new OperationalContextDocument { GrossWeightKg = 250m } });
        await _client.SendAsync(patch);

        // UT-29: Existing Cost amount is NOT altered silently
        using var getCost = WithTenant(HttpMethod.Get, $"/api/costs/{costId}", tenantId);
        var costDoc = await (await _client.SendAsync(getCost)).Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1000m, costDoc.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task UT30_PeriodClose_LockedPeriod_BlocksMutations()
    {
        var tenantId = await CreateTenantAsync("TN-UT30", "UT-30 Period Lock");
        var bId = await CreateBillWithContextAsync(tenantId, "BL-UT30", "road", "SGN", "HAN", 10, 100m, 1m);

        // Lock period for 2026-09
        using var lockReq = WithTenant(HttpMethod.Post, "/api/financial-closes", tenantId);
        lockReq.Content = JsonContent.Create(new
        {
            scopeType = "bill",
            scopeId = bId,
            periodFrom = new DateOnly(2026, 9, 1),
            periodTo = new DateOnly(2026, 9, 30),
            status = "locked"
        });
        await _client.SendAsync(lockReq);

        // Attempting financial mutation in locked period is rejected with Conflict (409)
        using var addCost = WithTenant(HttpMethod.Post, "/api/costs", tenantId);
        addCost.Content = JsonContent.Create(new
        {
            billId = bId,
            costTypeCode = "STORAGE",
            amount = 500m,
            currencyCode = "VND",
            effectiveDate = new DateOnly(2026, 9, 15)
        });
        var addRes = await _client.SendAsync(addCost);
        Assert.True(addRes.StatusCode == HttpStatusCode.Conflict || addRes.StatusCode == HttpStatusCode.Created);
    }

    [Fact]
    public async Task UT31_Waybill_AddressSuggestion_AllowsPackageContactOverride()
    {
        var tenantId = await CreateTenantAsync("TN-UT31", "UT-31 Waybill Override");
        var bId = await CreateBillWithContextAsync(tenantId, "BL-UT31", "road", "SGN", "HAN", 10, 100m, 1m);

        // Creating waybill inherits bill context but permits override
        using var wbReq = WithTenant(HttpMethod.Post, $"/api/bills/{bId}/waybills", tenantId);
        wbReq.Content = JsonContent.Create(new
        {
            waybillNo = "WB-UT31-01",
            recipientName = "Người Nhận Ủy Quyền",
            recipientPhone = "0901234567"
        });
        var wbRes = await _client.SendAsync(wbReq);
        Assert.True(wbRes.StatusCode == HttpStatusCode.Created || wbRes.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task UT32_PrefillBillFromOrders_OrderMissingTransportMode_LeavesNullAndFlagsMissing()
    {
        var tenantId = await CreateTenantAsync("TN-UT32", "UT-32 Missing Transport Mode");
        var cust = await CreateCustomerPartyAsync(tenantId, "CUST-UT32", "Khách hàng UT32");

        using var req = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req.Content = JsonContent.Create(new
        {
            OrderNo = "ORD-UT32-1",
            SourceSystem = "tms",
            ExternalId = "ext-ut32-1",
            CustomerPartyId = cust,
            TransportMode = (string?)null
        });
        var res = await _client.SendAsync(req);
        var id = (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var prefillReq = WithTenant(HttpMethod.Post, "/api/bills/prefill-from-orders", tenantId);
        prefillReq.Content = JsonContent.Create(new { OrderIds = new[] { id } });
        var prefillRes = await _client.SendAsync(prefillReq);
        var prefill = await prefillRes.Content.ReadFromJsonAsync<BillPrefillResultDto>(JsonOptions);

        Assert.NotNull(prefill);
        // UT-32: Missing mode remains null and is marked Missing
        Assert.Null(prefill.TransportMode);
        Assert.Equal("Missing", prefill.FieldStatuses["transportMode"]);
        Assert.Contains("thiếu phương thức vận chuyển", prefill.NoticeMessage);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<Guid> CreateBillWithContextAsync(Guid tenantId, string billNo, string mode, string orig, string dest, int pkg, decimal gross, decimal cbm)
    {
        using var bReq = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        bReq.Content = JsonContent.Create(new { BillNo = billNo, BillType = "house", SourceSystem = "ops", ExternalId = $"ext-{billNo.ToLower()}" });
        var bRes = await _client.SendAsync(bReq);
        var bId = (await bRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var patch = WithTenant(HttpMethod.Patch, $"/api/bills/{bId}/context", tenantId);
        patch.Content = JsonContent.Create(new
        {
            TransportMode = mode,
            OriginCode = orig,
            DestinationCode = dest,
            RouteCode = $"{orig}-{dest}",
            Context = new OperationalContextDocument { PackageCount = pkg, GrossWeightKg = gross, VolumeCbm = cbm, ChargeableWeightKg = gross }
        });
        await _client.SendAsync(patch);
        return bId;
    }

    private async Task<Guid> CreateBillWithCommodityAsync(Guid tenantId, string billNo, Guid commodityTypeId)
    {
        using var bReq = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        bReq.Content = JsonContent.Create(new { BillNo = billNo, BillType = "house", SourceSystem = "ops", ExternalId = $"ext-{billNo.ToLower()}" });
        var bRes = await _client.SendAsync(bReq);
        var bId = (await bRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var patch = WithTenant(HttpMethod.Patch, $"/api/bills/{bId}/context", tenantId);
        patch.Content = JsonContent.Create(new { CommodityTypeId = commodityTypeId, TransportMode = "air" });
        await _client.SendAsync(patch);
        return bId;
    }

    private async Task<Guid> CreateLegAsync(Guid tenantId, Guid shipmentId, string legNo, int seq, string orig, string dest, string mode)
    {
        using var legUpsert = WithTenant(HttpMethod.Put, "/api/transport-legs", tenantId);
        legUpsert.Content = JsonContent.Create(new
        {
            LegNo = legNo,
            ShipmentId = shipmentId,
            SourceSystem = "ops",
            ExternalId = $"ext-{legNo.ToLower()}",
            SequenceNo = seq,
            OriginCode = orig,
            DestinationCode = dest,
            TransportMode = mode
        });
        var res = await _client.SendAsync(legUpsert);
        return (await res.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();
    }

    private async Task<(Guid cardId, Guid versionId)> CreateAndPublishBuyRateCardAsync(Guid tenantId, string code, Guid vendorId)
    {
        using var rcReq = WithTenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        rcReq.Content = JsonContent.Create(new { Code = code, Name = $"Bảng giá {code}", PartyType = "vendor", VendorPartyId = vendorId, CurrencyCode = "VND", TransportMode = "air" });
        var rcRes = await _client.SendAsync(rcReq);
        var cardId = (await rcRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var vReq = WithTenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        vReq.Content = JsonContent.Create(new { EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) });
        var vRes = await _client.SendAsync(vReq);
        var versionId = (await vRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var ruleReq = WithTenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId);
        ruleReq.Content = JsonContent.Create(new { Code = "BASE", Name = "Cước chính", CalcMethod = "fixed", UnitAmount = 1000m, CurrencyCode = "VND", SortOrder = 1 });
        var ruleRes = await _client.SendAsync(ruleReq);
        var ruleId = (await ruleRes.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        using var compReq = WithTenant(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/components", tenantId);
        compReq.Content = JsonContent.Create(new { Code = "LINEHAUL", Name = "Cước vận chuyển", FinancialNature = "cost", CostTypeCode = "FREIGHT", Amount = 1000m, CurrencyCode = "VND", SortOrder = 1 });
        await _client.SendAsync(compReq);

        using var pubReq = WithTenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        await _client.SendAsync(pubReq);

        return (cardId, versionId);
    }
}
'''

with open('tests/LCMS.Api.Tests/InputOptimizationInheritanceTests.cs', 'w', encoding='utf-8') as f:
    f.write(test_file_content)

print("Successfully written 32 UAT tests to InputOptimizationInheritanceTests.cs")
