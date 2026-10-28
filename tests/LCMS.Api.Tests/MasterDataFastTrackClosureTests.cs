using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.Catalog;
using LCMS.Application.Surcharges;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;

namespace LCMS.Api.Tests;

public sealed class CatalogCodeAndSurchargeBoundaryTests
{
    [Fact]
    public void DocumentType_NormalizesLower_AndTransportMode_NormalizesUpper()
    {
        Assert.Equal("invoice", CatalogCodePolicy.Normalize(MasterCatalogKinds.DocumentType, " Invoice "));
        Assert.Equal("debit_note", CatalogCodePolicy.Normalize(MasterCatalogKinds.DocumentType, "DEBIT_NOTE"));
        Assert.Equal("credit_note", CatalogCodePolicy.Normalize("document_type", "Credit_Note"));
        Assert.Equal("customer", CatalogCodePolicy.Normalize("party_role", "Customer"));
        Assert.Equal("dg", CatalogCodePolicy.Normalize("special_flag", "DG"));
        Assert.Equal("FREIGHT", CatalogCodePolicy.Normalize(MasterCatalogKinds.ServiceType, "freight"));
        Assert.Equal("AIR", CatalogCodePolicy.Normalize(MasterCatalogKinds.TransportMode, " air "));
        Assert.Equal("SGN", CatalogCodePolicy.Normalize(MasterCatalogKinds.Location, "SGN"));
        Assert.Equal("sgn", CatalogCodePolicy.Normalize(MasterCatalogKinds.Location, "sgn"));
    }

    [Fact]
    public void EmptyCatalog_RejectsRecognizedAndGarbageModes()
    {
        var empty = Array.Empty<string>();
        var air = TransportModeCatalog.Decide("AIR", empty, allowMultimodal: false);
        var garbage = TransportModeCatalog.Decide("RANDOM_MODE", empty, allowMultimodal: false);
        var xyz = TransportModeCatalog.Decide("XYZ", empty, allowMultimodal: true);

        Assert.False(air.Accepted);
        Assert.Equal("transport_mode_catalog_empty", air.ErrorCode);
        Assert.False(garbage.Accepted);
        Assert.Equal("transport_mode_not_allowed", garbage.ErrorCode);
        Assert.False(xyz.Accepted);
        Assert.Equal("transport_mode_not_allowed", xyz.ErrorCode);
    }

    [Fact]
    public void LockedModes_StayWaterExpressOceanMultimodal()
    {
        var seaOnly = new[] { "SEA" };
        var ocean = TransportModeCatalog.Decide("OCEAN", seaOnly, allowMultimodal: false);
        Assert.False(ocean.Accepted);
        Assert.Equal("transport_mode_not_in_catalog", ocean.ErrorCode);

        var oceanCatalog = new[] { "OCEAN" };
        var storedOcean = TransportModeCatalog.Decide("ocean", oceanCatalog, allowMultimodal: false);
        Assert.True(storedOcean.Accepted);
        Assert.Equal("OCEAN", storedOcean.Code);
        Assert.NotEqual("SEA", storedOcean.Code);

        var waterOnRate = TransportModeCatalog.Decide("WATER", new[] { "WATER", "SEA" }, allowMultimodal: false, forRateRule: true);
        Assert.Equal("transport_mode_water_blocked", waterOnRate.ErrorCode);
        var waterOnLeg = TransportModeCatalog.Decide("WATER", new[] { "WATER", "SEA" }, allowMultimodal: false);
        Assert.True(waterOnLeg.Accepted);
        Assert.Equal("WATER", waterOnLeg.Code);
        var express = TransportModeCatalog.Decide("express", new[] { "AIR" }, allowMultimodal: true);
        Assert.Equal("transport_mode_express_blocked", express.ErrorCode);

        var leg = TransportModeCatalog.Decide("MULTIMODAL", new[] { "MULTIMODAL" }, allowMultimodal: false);
        Assert.Equal("transport_mode_multimodal_scope", leg.ErrorCode);
        var shipment = TransportModeCatalog.Decide("MULTIMODAL", new[] { "MULTIMODAL", "ROAD" }, allowMultimodal: true);
        Assert.True(shipment.Accepted);
        Assert.Equal("MULTIMODAL", shipment.Code);

        var kept = TransportModeCatalog.Decide("  AIR  ", Array.Empty<string>(), allowMultimodal: false, currentStored: "air");
        Assert.True(kept.Accepted);
        Assert.Equal("air", kept.Code);
    }

    [Fact]
    public void SurchargeEffectiveDay_IsClosedOnTheCalendarDay()
    {
        var from = new DateTimeOffset(2026, 10, 28, 18, 30, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 10, 30, 2, 0, 0, TimeSpan.Zero);
        var before = new DateTimeOffset(2026, 10, 27, 23, 0, 0, TimeSpan.Zero);
        var sameStart = new DateTimeOffset(2026, 10, 28, 0, 30, 0, TimeSpan.Zero);
        var sameEnd = new DateTimeOffset(2026, 10, 30, 23, 0, 0, TimeSpan.Zero);
        var after = new DateTimeOffset(2026, 10, 31, 0, 5, 0, TimeSpan.Zero);

        Assert.True(RatingEngine.IsBeforeEffectiveDay(before, from));
        Assert.False(RatingEngine.IsBeforeEffectiveDay(sameStart, from));
        Assert.False(RatingEngine.IsAfterEffectiveDay(sameEnd, to));
        Assert.True(RatingEngine.IsAfterEffectiveDay(after, to));

        var candidate = new SurchargeCandidate(
            Guid.NewGuid(),
            "FSC",
            "Phụ phí xăng dầu",
            "buy",
            Guid.NewGuid(),
            1,
            from,
            to,
            Guid.NewGuid(),
            "fixed_rate",
            null,
            "VND",
            10m,
            1,
            null,
            null,
            null,
            [],
            [],
            []);

        Assert.False(SurchargeRating.IsEffective(candidate, before));
        Assert.True(SurchargeRating.IsEffective(candidate, sameStart));
        Assert.True(SurchargeRating.IsEffective(candidate, from));
        Assert.True(SurchargeRating.IsEffective(candidate, to));
        Assert.True(SurchargeRating.IsEffective(candidate, sameEnd));
        Assert.False(SurchargeRating.IsEffective(candidate, after));
    }
}

[Collection("Api")]
public sealed class TransportModeCatalogApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public TransportModeCatalogApiTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task EmptyCatalog_BlocksAirAndGarbage_OnRateCardAndBill()
    {
        var tenantId = await CreateTenantAsync();
        await DeactivateBaselineModesAsync(tenantId);
        var air = await PostCardAsync(tenantId, "RC-EMPTY-AIR", "AIR");
        Assert.Equal(HttpStatusCode.BadRequest, air.StatusCode);
        var airBody = await ReadProblemAsync(air);
        Assert.Equal("transport_mode_catalog_empty", airBody.ErrorCode);
        Assert.Contains("danh mục", airBody.Message, StringComparison.OrdinalIgnoreCase);

        var garbage = await PostCardAsync(tenantId, "RC-EMPTY-XYZ", "RANDOM_MODE");
        Assert.Equal(HttpStatusCode.BadRequest, garbage.StatusCode);
        Assert.Equal("transport_mode_not_allowed", (await ReadProblemAsync(garbage)).ErrorCode);

        var billId = await CreateBillAsync(tenantId, "BL-EMPTY-MODE");
        var patched = await PatchModeAsync(tenantId, billId, "XYZ");
        Assert.Equal(HttpStatusCode.BadRequest, patched.StatusCode);
        Assert.Equal("transport_mode_not_allowed", (await ReadProblemAsync(patched)).ErrorCode);
    }

    [Fact]
    public async Task ActiveCatalog_StoresUppercase_AndKeepsOceanDistinct()
    {
        var tenantId = await CreateTenantAsync();
        await SeedAsync(tenantId, "transport_mode", "OCEAN", "Đường biển đại dương", false);

        var rejectedOcean = await PostCardAsync(tenantId, "RC-OCEAN-OFF", "ocean");
        Assert.Equal(HttpStatusCode.BadRequest, rejectedOcean.StatusCode);
        Assert.Equal("transport_mode_not_in_catalog", (await ReadProblemAsync(rejectedOcean)).ErrorCode);

        await SeedAsync(tenantId, "transport_mode", "OCEAN", "Đường biển đại dương");
        var created = await PostCardAsync(tenantId, "RC-AIR", "air");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var cardId = (await created.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var get = Tenant(HttpMethod.Get, $"/api/rate-cards/{cardId}", tenantId);
        var card = await (await SendOk(get)).Content.ReadFromJsonAsync<CardBody>(Json);
        Assert.Equal("AIR", card!.TransportMode);

        var ocean = await PostCardAsync(tenantId, "RC-OCEAN", "ocean");
        Assert.Equal(HttpStatusCode.Created, ocean.StatusCode);
        var oceanId = (await ocean.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var getOcean = Tenant(HttpMethod.Get, $"/api/rate-cards/{oceanId}", tenantId);
        var oceanCard = await (await SendOk(getOcean)).Content.ReadFromJsonAsync<CardBody>(Json);
        Assert.Equal("OCEAN", oceanCard!.TransportMode);

        var water = await PostCardAsync(tenantId, "RC-WATER", "WATER");
        Assert.Equal("transport_mode_water_blocked", (await ReadProblemAsync(water)).ErrorCode);
        var express = await PostCardAsync(tenantId, "RC-EXPRESS", "EXPRESS");
        Assert.Equal("transport_mode_express_blocked", (await ReadProblemAsync(express)).ErrorCode);
    }

    [Fact]
    public async Task CatalogAdmin_KeepsDocumentTypeLower_AndRejectsServiceAir()
    {
        var tenantId = await CreateTenantAsync();
        var document = await SeedAsync(tenantId, "document_type", "Invoice", "Hóa đơn");
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);

        using var list = Tenant(HttpMethod.Get, "/api/master-catalog?kind=document_type", tenantId);
        var items = await (await SendOk(list)).Content.ReadFromJsonAsync<List<CatalogRow>>(Json);
        Assert.Contains(items!, row => row.Code == "invoice");
        Assert.DoesNotContain(items!, row => row.Code == "INVOICE");

        var serviceAir = await SeedAsync(tenantId, "service_type", "AIR", "Hàng không");
        Assert.Equal(HttpStatusCode.BadRequest, serviceAir.StatusCode);
        Assert.Equal("service_type_not_a_mode", (await ReadProblemAsync(serviceAir)).ErrorCode);

        var freight = await SeedAsync(tenantId, "service_type", "freight", "Vận tải hàng");
        Assert.Equal(HttpStatusCode.OK, freight.StatusCode);
        using var services = Tenant(HttpMethod.Get, "/api/master-catalog?kind=service_type", tenantId);
        var serviceRows = await (await SendOk(services)).Content.ReadFromJsonAsync<List<CatalogRow>>(Json);
        Assert.Contains(serviceRows!, row => row.Code == "FREIGHT");
    }

    private async Task<HttpResponseMessage> PostCardAsync(Guid tenantId, string code, string mode)
    {
        using var req = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        req.Content = JsonContent.Create(new
        {
            code,
            name = code,
            partyType = "vendor",
            currencyCode = "VND",
            transportMode = mode
        });
        return await _client.SendAsync(req);
    }

    private async Task<HttpResponseMessage> SeedAsync(Guid tenantId, string kind, string code, string name, bool isActive = true)
    {
        using var req = Tenant(HttpMethod.Put, "/api/master-catalog", tenantId);
        req.Content = JsonContent.Create(new { kind, code, name, isActive });
        return await _client.SendAsync(req);
    }

    private async Task DeactivateBaselineModesAsync(Guid tenantId)
    {
        foreach (var code in new[] { "AIR", "SEA", "ROAD", "RAIL", "WATER", "OCEAN", "MULTIMODAL" })
        {
            var res = await SeedAsync(tenantId, "transport_mode", code, code, false);
            res.EnsureSuccessStatusCode();
        }
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = Tenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        var res = await SendOk(req);
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<HttpResponseMessage> PatchModeAsync(Guid tenantId, Guid billId, string mode)
    {
        using var req = Tenant(HttpMethod.Patch, $"/api/operational-references/bill/{billId}", tenantId);
        req.Content = JsonContent.Create(new { changes = new Dictionary<string, string?> { ["transport_mode"] = mode } });
        return await _client.SendAsync(req);
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Gate" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<HttpResponseMessage> SendOk(HttpRequestMessage req)
    {
        var res = await _client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        }

        return res;
    }

    private static async Task<ProblemBody> ReadProblemAsync(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<ProblemBody>(Json);
        return body!;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record CardBody(string? TransportMode);
    private sealed record CatalogRow(string Code);
    private sealed record ProblemBody(
        string Code,
        [property: System.Text.Json.Serialization.JsonPropertyName("error_code")] string? ErrorCode,
        string Message);
}
