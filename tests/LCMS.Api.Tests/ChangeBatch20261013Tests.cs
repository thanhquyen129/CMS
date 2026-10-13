using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.OperationalReferences.Edit;
using LCMS.Domain.Entities;

namespace LCMS.Api.Tests;

public sealed class ShipmentModePolicyTests
{
    [Fact]
    public void Derive_MixedFamilies_IsMultimodal_AndDoesNotTreatWaterAsASecondMode()
    {
        Assert.Equal("multimodal", ShipmentModePolicy.Derive(["road", "air", "sea"]));
        Assert.Equal("multimodal", ShipmentModePolicy.Derive(["sea", "water"]));
        Assert.Equal("sea", ShipmentModePolicy.Derive(["sea", "ocean"]));
        Assert.Equal("water", ShipmentModePolicy.Derive(["water"]));
        Assert.Null(ShipmentModePolicy.Derive([null, "multimodal"]));
        Assert.Equal("road", ShipmentModePolicy.Derive(["road"]));
    }

    [Fact]
    public void Water_DoesNotUseSeaChargeableWeight()
    {
        Assert.False(ChargeableWeightPolicy.IsSea("water"));
        Assert.True(ChargeableWeightPolicy.IsSea("sea"));
        Assert.True(ChargeableWeightPolicy.IsSea("ocean"));
        Assert.Equal("water", ShipmentModePolicy.PhysicalFamily("water"));
        Assert.Equal("sea", ShipmentModePolicy.PhysicalFamily("ocean"));

        var water = ChargeableWeightPolicy.Compute("WATER", 1000m, 2m);
        Assert.Null(water.Value);
        Assert.NotEqual(ChargeableWeightPolicy.SeaRuleCode, water.RuleCode);
        Assert.NotEqual("W/M", water.Uom);

        var sea = ChargeableWeightPolicy.Compute("sea", 1000m, 2m);
        Assert.Equal(ChargeableWeightPolicy.SeaRuleCode, sea.RuleCode);
        Assert.Equal("W/M", sea.Uom);
        Assert.NotNull(sea.Value);
    }

    [Fact]
    public void Mismatch_KeepsSource_AndLegRatingIgnoresMultimodal()
    {
        Assert.True(ShipmentModePolicy.IsMismatch("air", "multimodal"));
        Assert.True(ShipmentModePolicy.IsMismatch("water", "sea"));
        Assert.False(ShipmentModePolicy.IsMismatch("ocean", "sea"));
        Assert.False(ShipmentModePolicy.IsMismatch(null, "road"));
        Assert.Equal("road", ShipmentModePolicy.LegRatingMode("road", "multimodal", "multimodal"));
        Assert.Equal("air", ShipmentModePolicy.LegRatingMode(null, "air", null));
        Assert.Null(ShipmentModePolicy.LegRatingMode(null, "multimodal", null));
    }
}

/// <summary>LCMS-CR-20261013-02 — multimodal classification and rate-card partner role.</summary>
[Collection("Api")]
public sealed class ChangeBatch20261013Tests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public ChangeBatch20261013Tests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task ShipmentClassification_FollowsLegs_WithoutOverwritingSourceOrLegModes()
    {
        var tenantId = await CreateTenantAsync("TN-MM-01", "Multimodal");
        var shipmentId = await PutId(tenantId, "/api/shipments", new
        {
            shipmentNo = "SH-MM",
            sourceSystem = "tms",
            externalId = "sh-mm",
            transportMode = "air",
            isActive = true,
            applyContext = true
        });
        var roadId = await PutId(tenantId, "/api/transport-legs", new
        {
            legNo = "L-ROAD",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "l-road",
            isActive = true
        });
        var airId = await PutId(tenantId, "/api/transport-legs", new
        {
            legNo = "L-AIR",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "l-air",
            isActive = true
        });
        await PatchMode(tenantId, "leg", roadId, "road");
        await PatchMode(tenantId, "leg", airId, "air");

        var mixed = await GetShipment(tenantId, shipmentId);
        Assert.Equal("air", mixed.TransportMode);
        Assert.Equal("multimodal", mixed.DerivedTransportMode);
        Assert.True(mixed.TransportModeMismatch);
        Assert.Equal("road", mixed.Legs.Single(l => l.Id == roadId).TransportMode);
        Assert.Equal("air", mixed.Legs.Single(l => l.Id == airId).TransportMode);

        var rejected = await PatchMode(tenantId, "leg", roadId, "multimodal", expectOk: false);
        Assert.Equal(HttpStatusCode.BadRequest, rejected);
        var stillRoad = await GetShipment(tenantId, shipmentId);
        Assert.Equal("road", stillRoad.Legs.Single(l => l.Id == roadId).TransportMode);
        Assert.Equal("air", stillRoad.TransportMode);

        await PatchMode(tenantId, "leg", airId, "road");
        var oneMode = await GetShipment(tenantId, shipmentId);
        Assert.Equal("road", oneMode.DerivedTransportMode);
        Assert.True(oneMode.TransportModeMismatch);
        Assert.Equal("air", oneMode.TransportMode);
    }

    [Fact]
    public async Task SeaAndWater_StayDistinct_AndStoredLegModesStay()
    {
        var tenantId = await CreateTenantAsync("TN-MM-SEA", "Sea water");
        var shipmentId = await PutId(tenantId, "/api/shipments", new
        {
            shipmentNo = "SH-SEA",
            sourceSystem = "lcms_manual",
            externalId = "sh-sea",
            transportMode = "water",
            isActive = true,
            applyContext = true
        });
        var seaId = await PutId(tenantId, "/api/transport-legs", new
        {
            legNo = "L-SEA",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "l-sea",
            isActive = true
        });
        var waterId = await PutId(tenantId, "/api/transport-legs", new
        {
            legNo = "L-WATER",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "l-water",
            isActive = true
        });
        await PatchMode(tenantId, "leg", seaId, "sea");
        await PatchMode(tenantId, "leg", waterId, "water");

        var row = await GetShipment(tenantId, shipmentId);
        Assert.Equal("water", row.TransportMode);
        Assert.Equal("multimodal", row.DerivedTransportMode);
        Assert.True(row.TransportModeMismatch);
        Assert.Equal("sea", row.Legs.Single(l => l.Id == seaId).TransportMode);
        Assert.Equal("water", row.Legs.Single(l => l.Id == waterId).TransportMode);
    }

    [Fact]
    public async Task RateCardPartner_RequiresRole_AndLeavesHistoricalPartner()
    {
        var tenantId = await CreateTenantAsync("TN-RP-01", "Rate partner");
        var vendorId = await CreateParty(tenantId, "NCC-1", "vendor");
        var customerId = await CreateParty(tenantId, "KH-1", "customer");
        var bothId = await CreateParty(tenantId, "BOTH-1", "vendor", "customer");
        var carrierId = await CreateParty(tenantId, "CAR-1", "carrier");

        var buyId = await CreateCard(tenantId, "BUY-1", "vendor", vendorId, null);
        Assert.Equal(HttpStatusCode.Conflict, await CreateCardStatus(tenantId, "BUY-CUS", "vendor", customerId, null));
        Assert.Equal(HttpStatusCode.Conflict, await CreateCardStatus(tenantId, "BUY-CAR", "vendor", carrierId, null));
        Assert.Equal(HttpStatusCode.Created, await CreateCardStatus(tenantId, "BUY-OPEN", "vendor", null, null));
        Assert.Equal(HttpStatusCode.Created, await CreateCardStatus(tenantId, "SELL-1", "customer", null, customerId));
        Assert.Equal(HttpStatusCode.Created, await CreateCardStatus(tenantId, "BUY-BOTH", "vendor", bothId, null));
        Assert.Equal(HttpStatusCode.Created, await CreateCardStatus(tenantId, "SELL-BOTH", "customer", null, bothId));

        using (var revoke = Tenant(HttpMethod.Delete, $"/api/business-parties/{vendorId}/roles/vendor", tenantId))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(revoke)).StatusCode);
        }

        using (var keep = Tenant(HttpMethod.Put, $"/api/rate-cards/{buyId}/partner", tenantId))
        {
            keep.Content = JsonContent.Create(new { supplierPartyId = vendorId });
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(keep)).StatusCode);
        }

        var kept = await GetCard(tenantId, buyId);
        Assert.Equal(vendorId, kept.SupplierPartyId);

        using (var wrong = Tenant(HttpMethod.Put, $"/api/rate-cards/{buyId}/partner", tenantId))
        {
            wrong.Content = JsonContent.Create(new { supplierPartyId = customerId });
            var res = await _client.SendAsync(wrong);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrBody>(Json);
            Assert.Contains("Nhà cung cấp", err!.Message);
        }

        var unchanged = await GetCard(tenantId, buyId);
        Assert.Equal(vendorId, unchanged.SupplierPartyId);
    }

    [Fact]
    public async Task QuickCreate_RequiresShipmentCreate_AndAuditsTheSameExternalId()
    {
        var tenantId = await CreateTenantAsync("TN-QC-06", "Quick create");
        var viewer = await CreateUserAsync(tenantId, "viewer-qc@example.com");
        await AssignRoleAsync(tenantId, viewer, "Viewer");
        var ops = await CreateUserAsync(tenantId, "ops-qc@example.com");
        await AssignRoleAsync(tenantId, ops, "Ops");

        using (var denied = Actor(HttpMethod.Put, "/api/shipments", tenantId, viewer))
        {
            denied.Content = JsonContent.Create(QuickBody("SH-DENY", "ext-deny"));
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(denied)).StatusCode);
        }

        using var created = Actor(HttpMethod.Put, "/api/shipments", tenantId, ops);
        created.Content = JsonContent.Create(QuickBody("SH-QC", "ext-qc"));
        var first = await _client.SendAsync(created);
        first.EnsureSuccessStatusCode();
        var shipmentId = (await first.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        using var retry = Actor(HttpMethod.Put, "/api/shipments", tenantId, ops);
        retry.Content = JsonContent.Create(QuickBody("SH-QC", "ext-qc"));
        var second = await _client.SendAsync(retry);
        second.EnsureSuccessStatusCode();
        Assert.Equal(shipmentId, (await second.Content.ReadFromJsonAsync<IdBody>(Json))!.Id);

        using var listed = Tenant(HttpMethod.Get, "/api/shipments?q=ext-qc", tenantId);
        var rows = await (await _client.SendAsync(listed)).Content.ReadFromJsonAsync<List<ShipmentListItem>>(Json);
        Assert.Single(rows!);

        using var auditReq = Tenant(HttpMethod.Get, $"/api/audit-events?objectType=shipment&objectId={shipmentId}", tenantId);
        var audits = await (await _client.SendAsync(auditReq)).Content.ReadFromJsonAsync<List<AuditBody>>(Json);
        Assert.NotNull(audits);
        Assert.Contains(audits!, a =>
            a.Action == "shipment.create"
            && a.ActorId == ops
            && a.OccurredAt > DateTimeOffset.UtcNow.AddMinutes(-5)
            && a.AfterJson != null
            && a.AfterJson.Contains("quick_create_modal")
            && a.AfterJson.Contains("ext-qc"));

        using var plain = Tenant(HttpMethod.Put, "/api/shipments", tenantId);
        plain.Content = JsonContent.Create(new
        {
            shipmentNo = "SH-PLAIN",
            sourceSystem = "lcms_manual",
            externalId = "ext-plain",
            isActive = true
        });
        var plainRes = await _client.SendAsync(plain);
        plainRes.EnsureSuccessStatusCode();
        var plainId = (await plainRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var plainAudit = Tenant(HttpMethod.Get, $"/api/audit-events?objectType=shipment&objectId={plainId}", tenantId);
        var plainEvents = await (await _client.SendAsync(plainAudit)).Content.ReadFromJsonAsync<List<AuditBody>>(Json);
        Assert.Empty(plainEvents!);
    }

    [Fact]
    public async Task UpdateRateCard_RejectsDirectionChange_UntilPartnerIsCleared()
    {
        var tenantId = await CreateTenantAsync("TN-RP-DIR", "Rate direction");
        var vendorId = await CreateParty(tenantId, "NCC-DIR", "vendor");
        var customerId = await CreateParty(tenantId, "KH-DIR", "customer");
        var buyId = await CreateCard(tenantId, "BUY-DIR", "vendor", vendorId, null);
        var sellId = await CreateCard(tenantId, "SELL-DIR", "customer", null, customerId);

        using (var flipBuy = Tenant(HttpMethod.Put, $"/api/rate-cards/{buyId}", tenantId))
        {
            flipBuy.Content = JsonContent.Create(CardUpdate("BUY-DIR", "customer"));
            var res = await _client.SendAsync(flipBuy);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var detail = doc.RootElement.GetProperty("errors").GetProperty("partyType")[0].GetString();
            Assert.Contains("chiều mua", detail);
        }

        var buy = await GetCard(tenantId, buyId);
        Assert.Equal("vendor", buy.PartyType);
        Assert.Equal(vendorId, buy.SupplierPartyId);

        using (var same = Tenant(HttpMethod.Put, $"/api/rate-cards/{buyId}", tenantId))
        {
            same.Content = JsonContent.Create(CardUpdate("BUY-DIR-2", "vendor"));
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(same)).StatusCode);
        }

        var kept = await GetCard(tenantId, buyId);
        Assert.Equal("vendor", kept.PartyType);
        Assert.Equal(vendorId, kept.SupplierPartyId);

        using var flipSell = Tenant(HttpMethod.Put, $"/api/rate-cards/{sellId}", tenantId);
        flipSell.Content = JsonContent.Create(CardUpdate("SELL-DIR", "vendor"));
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(flipSell)).StatusCode);
        var sell = await GetCard(tenantId, sellId);
        Assert.Equal("customer", sell.PartyType);
        Assert.Equal(customerId, sell.CustomerPartyId);
    }

    [Fact]
    public async Task ImportMultimodal_RoundTripsInListAndReport()
    {
        var tenantId = await CreateTenantAsync("TN-MM-IMP", "Import multimodal");
        using (var commit = Tenant(HttpMethod.Post, "/api/operational-import/commit", tenantId))
        {
            commit.Content = JsonContent.Create(new
            {
                sourceSystem = "tms-mm",
                rows = new object[]
                {
                    new
                    {
                        objectType = "shipment",
                        businessNo = "SH-MM-IMP",
                        externalId = "sh-mm-imp",
                        transportMode = "MULTIMODAL",
                        originCode = "SGN",
                        destinationCode = "HAN"
                    },
                    new
                    {
                        objectType = "shipment",
                        businessNo = "SH-OLD",
                        externalId = "sh-old",
                        originCode = "SGN",
                        destinationCode = "DAD"
                    },
                    new
                    {
                        objectType = "leg",
                        businessNo = "L-ROAD",
                        externalId = "l-road-imp",
                        parentExternalId = "sh-mm-imp",
                        transportMode = "road",
                        sequenceNo = 1
                    }
                }
            });
            var res = await _client.SendAsync(commit);
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<CommitBody>(Json);
            Assert.Equal(3, body!.Committed);
        }

        using var listed = Tenant(HttpMethod.Get, "/api/shipments?transportMode=multimodal", tenantId);
        var listRes = await _client.SendAsync(listed);
        listRes.EnsureSuccessStatusCode();
        var rows = await listRes.Content.ReadFromJsonAsync<List<ShipmentListItem>>(Json);
        var imported = Assert.Single(rows!);
        Assert.Equal("multimodal", imported.TransportMode);

        using var roadFilter = Tenant(HttpMethod.Get, "/api/shipments?transportMode=road", tenantId);
        var roadRes = await _client.SendAsync(roadFilter);
        roadRes.EnsureSuccessStatusCode();
        Assert.Empty((await roadRes.Content.ReadFromJsonAsync<List<ShipmentListItem>>(Json))!);

        using var report = Tenant(HttpMethod.Get, "/api/profitability/groups?groupBy=mode", tenantId);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(report)).StatusCode);

        using var preview = Tenant(HttpMethod.Post, "/api/operational-import/preview", tenantId);
        preview.Content = JsonContent.Create(new
        {
            sourceSystem = "tms-mm",
            rows = new object[]
            {
                new
                {
                    objectType = "leg",
                    businessNo = "L-BAD",
                    externalId = "l-bad",
                    parentExternalId = "sh-mm-imp",
                    transportMode = "multimodal"
                }
            }
        });
        var previewRes = await _client.SendAsync(preview);
        previewRes.EnsureSuccessStatusCode();
        var previewBody = await previewRes.Content.ReadFromJsonAsync<PreviewBody>(Json);
        Assert.False(previewBody!.CanCommit);
        Assert.Contains(previewBody.Issues, i => i.Field == "transportMode");

        using var badCommit = Tenant(HttpMethod.Post, "/api/operational-import/commit", tenantId);
        badCommit.Content = JsonContent.Create(new
        {
            sourceSystem = "tms-mm",
            rows = new object[]
            {
                new
                {
                    objectType = "leg",
                    businessNo = "L-BAD",
                    externalId = "l-bad",
                    parentExternalId = "sh-mm-imp",
                    transportMode = "multimodal"
                }
            }
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(badCommit)).StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(new { code, name })
        };
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> PutId(Guid tenantId, string path, object body)
    {
        using var req = Tenant(HttpMethod.Put, path, tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<HttpStatusCode> PatchMode(Guid tenantId, string type, Guid id, string mode, bool expectOk = true)
    {
        using var req = Tenant(HttpMethod.Patch, $"/api/operational-references/{type}/{id}", tenantId);
        req.Content = JsonContent.Create(new
        {
            changes = new Dictionary<string, string?> { ["transport_mode"] = mode },
            reason = "test"
        });
        var res = await _client.SendAsync(req);
        if (expectOk)
        {
            res.EnsureSuccessStatusCode();
        }

        return res.StatusCode;
    }

    private async Task<ShipmentBody> GetShipment(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/shipments/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ShipmentBody>(Json))!;
    }

    private async Task<Guid> CreateParty(Guid tenantId, string code, params string[] roles)
    {
        using var req = Tenant(HttpMethod.Post, "/api/business-parties", tenantId);
        req.Content = JsonContent.Create(new { code, name = code, roleCodes = roles });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateCard(Guid tenantId, string code, string partyType, Guid? supplierId, Guid? customerId)
    {
        var res = await SendCard(tenantId, code, partyType, supplierId, customerId);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<HttpStatusCode> CreateCardStatus(Guid tenantId, string code, string partyType, Guid? supplierId, Guid? customerId)
    {
        var res = await SendCard(tenantId, code, partyType, supplierId, customerId);
        return res.StatusCode;
    }

    private async Task<HttpResponseMessage> SendCard(Guid tenantId, string code, string partyType, Guid? supplierId, Guid? customerId)
    {
        using var req = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        req.Content = JsonContent.Create(new
        {
            code,
            name = code,
            partyType,
            currencyCode = "VND",
            supplierPartyId = supplierId,
            customerPartyId = customerId
        });
        return await _client.SendAsync(req);
    }

    private async Task<CardBody> GetCard(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/rate-cards/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CardBody>(Json))!;
    }

    private async Task<Guid> CreateUserAsync(Guid tenantId, string email)
    {
        using var req = Tenant(HttpMethod.Post, "/api/users", tenantId);
        req.Content = JsonContent.Create(new { email, displayName = email });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task AssignRoleAsync(Guid tenantId, Guid userId, string roleCode)
    {
        using var rolesReq = Tenant(HttpMethod.Get, "/api/roles", tenantId);
        var roles = await (await _client.SendAsync(rolesReq)).Content.ReadFromJsonAsync<List<RoleBody>>(Json);
        var roleId = roles!.Single(r => r.Code == roleCode).Id;
        using var req = Tenant(HttpMethod.Post, $"/api/users/{userId}/roles/{roleId}", tenantId);
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private static object QuickBody(string shipmentNo, string externalId) => new
    {
        shipmentNo,
        sourceSystem = "lcms_manual",
        externalId,
        operationalStatus = "active",
        isActive = true,
        applyContext = true,
        transportMode = "air",
        originCode = "SGN",
        destinationCode = "HAN",
        entryChannel = "quick_create_modal"
    };

    private static object CardUpdate(string name, string partyType) => new
    {
        name,
        partyType,
        currencyCode = "VND",
        isActive = true
    };

    private static HttpRequestMessage Tenant(HttpMethod method, string path, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private static HttpRequestMessage Actor(HttpMethod method, string path, Guid tenantId, Guid userId)
    {
        var req = Tenant(method, path, tenantId);
        req.Headers.Add("X-User-Id", userId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record ErrBody(string Message);
    private sealed record LegBody(Guid Id, string? TransportMode);
    private sealed record ShipmentBody(string? TransportMode, string? DerivedTransportMode, bool TransportModeMismatch, List<LegBody> Legs);
    private sealed record ShipmentListItem(Guid Id, string? TransportMode);
    private sealed record CardBody(string PartyType, Guid? SupplierPartyId, Guid? CustomerPartyId);
    private sealed record RoleBody(Guid Id, string Code);
    private sealed record AuditBody(Guid? ActorId, string Action, string? AfterJson, DateTimeOffset OccurredAt);
    private sealed record CommitBody(int Committed);
    private sealed record PreviewBody(bool CanCommit, List<IssueBody> Issues);
    private sealed record IssueBody(string Field);
}
