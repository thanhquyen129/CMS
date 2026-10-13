using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Domain.Entities;

namespace LCMS.Api.Tests;

public sealed class ShipmentModePolicyTests
{
    [Fact]
    public void Derive_MixedFamilies_IsMultimodal_AndDoesNotTreatWaterAsASecondMode()
    {
        Assert.Equal("multimodal", ShipmentModePolicy.Derive(["road", "air", "sea"]));
        Assert.Equal("sea", ShipmentModePolicy.Derive(["sea", "water", "ocean"]));
        Assert.Null(ShipmentModePolicy.Derive([null, "multimodal"]));
        Assert.Equal("road", ShipmentModePolicy.Derive(["road"]));
    }

    [Fact]
    public void Mismatch_KeepsSource_AndLegRatingIgnoresMultimodal()
    {
        Assert.True(ShipmentModePolicy.IsMismatch("air", "multimodal"));
        Assert.False(ShipmentModePolicy.IsMismatch("water", "sea"));
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
    public async Task SeaAndWater_AreOneFamily_AndStoredLegModesStay()
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
        Assert.Equal("sea", row.DerivedTransportMode);
        Assert.False(row.TransportModeMismatch);
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

    private static HttpRequestMessage Tenant(HttpMethod method, string path, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record ErrBody(string Message);
    private sealed record LegBody(Guid Id, string? TransportMode);
    private sealed record ShipmentBody(string? TransportMode, string? DerivedTransportMode, bool TransportModeMismatch, List<LegBody> Legs);
    private sealed record CardBody(Guid? SupplierPartyId);
}
