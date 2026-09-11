using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

/// <summary>Pass 2 Sprint 2 FULL — transport legs/movements, bill graph depth, operational search, C-002, data scope.</summary>
[Collection("Api")]
public sealed class Sprint2FullOperationalReferenceTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint2FullOperationalReferenceTests(LcmsApiFactory factory)
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
    public async Task UpsertLegAndMovement_Idempotent_AndGraphIncludesNodes()
    {
        var tenantId = await CreateTenantAsync("TN-S2F-G", "Sprint2 Full Graph");
        var billId = await CreateBillAsync(tenantId, "BL-S2F-1", "freight", "tms", "ext-bill-1");
        var shipmentId = await UpsertShipmentAsync(tenantId, "SHP-1", "tms", "ext-shp-1");

        using var linkShp = WithTenant(
            HttpMethod.Post,
            $"/api/bills/{billId}/shipments/{shipmentId}",
            tenantId);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(linkShp)).StatusCode);

        var legId = await UpsertLegAsync(tenantId, "LEG-1", shipmentId, "tms", "ext-leg-1", "v1");
        var legId2 = await UpsertLegAsync(tenantId, "LEG-1-REN", shipmentId, "tms", "ext-leg-1", "v2");
        Assert.Equal(legId, legId2);

        var movementId = await UpsertMovementAsync(tenantId, "MOV-1", "tms", "ext-mov-1");
        var movementId2 = await UpsertMovementAsync(tenantId, "MOV-1-REN", "tms", "ext-mov-1");
        Assert.Equal(movementId, movementId2);

        using var linkLeg = WithTenant(HttpMethod.Post, $"/api/bills/{billId}/legs/{legId}", tenantId);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(linkLeg)).StatusCode);

        using var linkLegAgain = WithTenant(HttpMethod.Post, $"/api/bills/{billId}/legs/{legId}", tenantId);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(linkLegAgain)).StatusCode);

        using var linkLegMov = WithTenant(
            HttpMethod.Post,
            $"/api/transport-legs/{legId}/movements/{movementId}",
            tenantId);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(linkLegMov)).StatusCode);

        using var linkBillMov = WithTenant(
            HttpMethod.Post,
            $"/api/bills/{billId}/movements/{movementId}",
            tenantId);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(linkBillMov)).StatusCode);

        using var graphReq = WithTenant(HttpMethod.Get, $"/api/bills/{billId}/graph", tenantId);
        var graphRes = await _client.SendAsync(graphReq);
        Assert.Equal(HttpStatusCode.OK, graphRes.StatusCode);
        var graph = await graphRes.Content.ReadFromJsonAsync<BillGraphResponse>(JsonOptions);
        Assert.NotNull(graph);
        Assert.Single(graph!.Shipments);
        Assert.Single(graph.Legs);
        Assert.Equal(legId, graph.Legs[0].Id);
        Assert.Equal("LEG-1-REN", graph.Legs[0].LegNo);
        Assert.Single(graph.Movements);
        Assert.Equal(movementId, graph.Movements[0].Id);
        Assert.Equal("MOV-1-REN", graph.Movements[0].MovementNo);
    }

    [Fact]
    public async Task OperationalSearch_FindsByBillNo_ExternalId_AndOrderExternalId_TenantIsolated()
    {
        var tenantA = await CreateTenantAsync("TN-S2F-SA", "Search A");
        var tenantB = await CreateTenantAsync("TN-S2F-SB", "Search B");

        var billA = await CreateBillAsync(tenantA, "BL-SEARCH-99", "freight", "tms", "ext-bill-needle");
        var orderA = await UpsertOrderAsync(tenantA, "ORD-X", "tms", "ext-ord-needle");
        using var linkOrder = WithTenant(HttpMethod.Post, $"/api/orders/{orderA}/bills/{billA}", tenantA);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(linkOrder)).StatusCode);

        _ = await CreateBillAsync(tenantB, "BL-SEARCH-99", "freight", "tms", "ext-bill-needle");

        using var byBillNo = WithTenant(HttpMethod.Get, "/api/search/operational?q=SEARCH-99", tenantA);
        var hitsBillNo = await (await _client.SendAsync(byBillNo)).Content
            .ReadFromJsonAsync<List<SearchHit>>(JsonOptions);
        Assert.Contains(hitsBillNo!, h => h.BillId == billA && h.MatchKind == "bill_no");

        using var byExt = WithTenant(HttpMethod.Get, "/api/search/operational?q=ext-bill-needle", tenantA);
        var hitsExt = await (await _client.SendAsync(byExt)).Content
            .ReadFromJsonAsync<List<SearchHit>>(JsonOptions);
        Assert.Contains(hitsExt!, h => h.BillId == billA && h.MatchKind == "bill_external_id");

        using var byOrder = WithTenant(HttpMethod.Get, "/api/search/operational?q=ext-ord-needle", tenantA);
        var hitsOrder = await (await _client.SendAsync(byOrder)).Content
            .ReadFromJsonAsync<List<SearchHit>>(JsonOptions);
        Assert.Contains(hitsOrder!, h => h.BillId == billA && h.MatchKind == "order_external_id");

        using var listQ = WithTenant(HttpMethod.Get, "/api/bills?q=ext-ord-needle", tenantA);
        var list = await (await _client.SendAsync(listQ)).Content
            .ReadFromJsonAsync<List<BillListItem>>(JsonOptions);
        Assert.Single(list!);
        Assert.Equal(billA, list[0].Id);

        using var cross = WithTenant(HttpMethod.Get, "/api/search/operational?q=ext-ord-needle", tenantB);
        var crossHits = await (await _client.SendAsync(cross)).Content
            .ReadFromJsonAsync<List<SearchHit>>(JsonOptions);
        Assert.DoesNotContain(crossHits!, h => h.BillId == billA);
    }

    [Fact]
    public async Task Search_RespectsDataScopeOwn_AndJwt()
    {
        var tenantId = await CreateTenantAsync("TN-S2F-DS", "Search Scope");
        var alice = await CreateUserAsync(tenantId, "alice.s2f@example.com", "Alice");
        var bob = await CreateUserAsync(tenantId, "bob.s2f@example.com", "Bob");

        var ownRole = await CreateRoleWithPermissionAsync(tenantId, "OwnReader", "bill.read", "own");
        await AssignPermissionAsync(tenantId, ownRole, "bill.create", "own");
        await AssignUserRoleAsync(tenantId, alice, ownRole);
        await AssignUserRoleAsync(tenantId, bob, ownRole);

        var billAlice = await CreateBillAsUserAsync(tenantId, alice, "BL-ALICE-S2F", "ext-alice-only");
        _ = await CreateBillAsUserAsync(tenantId, bob, "BL-BOB-S2F", "ext-bob-only");

        var tokenRes = await _client.PostAsJsonAsync("/api/dev/token", new { tenantId, userId = alice });
        Assert.Equal(HttpStatusCode.OK, tokenRes.StatusCode);
        var token = (await tokenRes.Content.ReadFromJsonAsync<DevTokenResponse>(JsonOptions))!.AccessToken;

        using var search = new HttpRequestMessage(HttpMethod.Get, "/api/search/operational?q=BL-");
        search.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var hits = await (await _client.SendAsync(search)).Content
            .ReadFromJsonAsync<List<SearchHit>>(JsonOptions);
        Assert.NotNull(hits);
        Assert.Contains(hits, h => h.BillId == billAlice);
        Assert.DoesNotContain(hits, h => h.BillNo.Contains("BOB", StringComparison.OrdinalIgnoreCase));

        using var listQ = new HttpRequestMessage(HttpMethod.Get, "/api/bills?q=BL-");
        listQ.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var list = await (await _client.SendAsync(listQ)).Content
            .ReadFromJsonAsync<List<BillListItem>>(JsonOptions);
        Assert.Single(list!);
        Assert.Equal(billAlice, list[0].Id);
    }

    [Fact]
    public async Task CrossTenant_LegLink_Returns404()
    {
        var tenantA = await CreateTenantAsync("TN-S2F-XA", "Iso A");
        var tenantB = await CreateTenantAsync("TN-S2F-XB", "Iso B");
        var billA = await CreateBillAsync(tenantA, "BL-ISO", "freight");
        var shipmentA = await UpsertShipmentAsync(tenantA, "SHP-ISO", "tms", "ext-shp-iso");
        var legA = await UpsertLegAsync(tenantA, "LEG-ISO", shipmentA, "tms", "ext-leg-iso");

        using var linkAsB = WithTenant(HttpMethod.Post, $"/api/bills/{billA}/legs/{legA}", tenantB);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(linkAsB)).StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateUserAsync(Guid tenantId, string email, string displayName)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/users", tenantId);
        req.Content = JsonContent.Create(new { email, displayName, organizationId = (Guid?)null });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateRoleWithPermissionAsync(
        Guid tenantId,
        string code,
        string actionCode,
        string dataScope)
    {
        using var create = WithTenant(HttpMethod.Post, "/api/roles", tenantId);
        create.Content = JsonContent.Create(new { code, name = code });
        var roleId = (await (await _client.SendAsync(create)).Content
            .ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        await AssignPermissionAsync(tenantId, roleId, actionCode, dataScope);
        return roleId;
    }

    private async Task AssignPermissionAsync(Guid tenantId, Guid roleId, string actionCode, string dataScope)
    {
        using var req = WithTenant(HttpMethod.Post, $"/api/roles/{roleId}/permissions", tenantId);
        req.Content = JsonContent.Create(new { actionCode, dataScope });
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task AssignUserRoleAsync(Guid tenantId, Guid userId, Guid roleId)
    {
        using var req = WithTenant(HttpMethod.Post, $"/api/users/{userId}/roles/{roleId}", tenantId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> CreateBillAsync(
        Guid tenantId,
        string billNo,
        string billType,
        string? sourceSystem = null,
        string? externalId = null)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType, sourceSystem, externalId });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateBillAsUserAsync(
        Guid tenantId,
        Guid userId,
        string billNo,
        string externalId)
    {
        using var req = WithUser(HttpMethod.Post, "/api/bills", tenantId, userId);
        req.Content = JsonContent.Create(new
        {
            billNo,
            billType = "freight",
            sourceSystem = "tms",
            externalId
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> UpsertOrderAsync(
        Guid tenantId,
        string orderNo,
        string sourceSystem,
        string externalId)
    {
        using var req = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req.Content = JsonContent.Create(new
        {
            orderNo,
            sourceSystem,
            externalId,
            isActive = true
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> UpsertShipmentAsync(
        Guid tenantId,
        string shipmentNo,
        string sourceSystem,
        string externalId)
    {
        using var req = WithTenant(HttpMethod.Put, "/api/shipments", tenantId);
        req.Content = JsonContent.Create(new
        {
            shipmentNo,
            sourceSystem,
            externalId,
            isActive = true
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> UpsertLegAsync(
        Guid tenantId,
        string legNo,
        Guid shipmentId,
        string sourceSystem,
        string externalId,
        string? externalVersion = null)
    {
        using var req = WithTenant(HttpMethod.Put, "/api/transport-legs", tenantId);
        req.Content = JsonContent.Create(new
        {
            legNo,
            shipmentId,
            sourceSystem,
            externalId,
            externalVersion,
            isActive = true
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> UpsertMovementAsync(
        Guid tenantId,
        string movementNo,
        string sourceSystem,
        string externalId)
    {
        using var req = WithTenant(HttpMethod.Put, "/api/transport-movements", tenantId);
        req.Content = JsonContent.Create(new
        {
            movementNo,
            sourceSystem,
            externalId,
            isActive = true
        });
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

    private static HttpRequestMessage WithUser(HttpMethod method, string url, Guid tenantId, Guid userId)
    {
        var req = WithTenant(method, url, tenantId);
        req.Headers.Add("X-User-Id", userId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record BillListItem(Guid Id, string BillNo);
    private sealed record SearchHit(Guid BillId, string BillNo, string MatchKind, string? MatchedValue);
    private sealed record DevTokenResponse(string AccessToken);

    private sealed record BillGraphLegRef(
        Guid Id,
        string LegNo,
        Guid ShipmentId,
        string SourceSystem,
        string ExternalId,
        string OperationalStatus);

    private sealed record BillGraphMovementRef(
        Guid Id,
        string MovementNo,
        string SourceSystem,
        string ExternalId,
        string OperationalStatus);

    private sealed record BillGraphShipmentRef(Guid Id, string ShipmentNo);

    private sealed record BillGraphResponse(
        Guid BillId,
        string BillNo,
        List<BillGraphShipmentRef> Shipments,
        List<BillGraphLegRef> Legs,
        List<BillGraphMovementRef> Movements);
}
