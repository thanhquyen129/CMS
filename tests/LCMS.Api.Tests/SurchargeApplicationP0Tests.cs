using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using LCMS.Application.Surcharges;
using LCMS.Domain.Entities;

namespace LCMS.Api.Tests;

/// <summary>Automated coverage for LCMS-CR-SURCHARGE-APPLICATION-01 (TC-01..32 mapped).</summary>
[Collection("Api")]
public sealed class SurchargeApplicationP0Tests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private readonly Dictionary<Guid, Guid> _chargeTypes = new();
    private HttpClient _client = null!;

    public SurchargeApplicationP0Tests(LcmsApiFactory factory) => _factory = factory;

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
    public void TC01_Optional_NotSelected_GoesToEligibleBucket()
    {
        var candidate = OptionalCandidate("PACK", "SGN-HAN", 300_000m);
        var ctx = Ctx(route: "SGN-HAN", selections: []);
        var result = SurchargeRating.Evaluate([candidate], ctx);
        Assert.Empty(result.Applied);
        Assert.Contains(result.EligibleNotActivated, x =>
            x.Rule.Code == "PACK" && x.ReasonCode == SurchargeReasonCodes.NotSelected);
    }

    [Fact]
    public void TC02_Optional_Selected_AppliesFixedAmount()
    {
        var candidate = OptionalCandidate("PACK", "SGN-HAN", 300_000m, basis: "per_bill");
        var ctx = Ctx(route: "SGN-HAN", selections:
        [
            new ServiceSelectionSnapshot("BILL", Guid.NewGuid(), "PACK", "BILL", 1m, true, 1, null, null)
        ]);
        var result = SurchargeRating.Evaluate([candidate], ctx);
        Assert.Single(result.Applied);
        Assert.Equal(300_000m, result.Applied[0].AmountOriginal);
    }

    [Fact]
    public void TC07_Auto_False_NotActivated()
    {
        var candidate = AutoDgCandidate(80_000m);
        var ctx = Ctx(dangerous: false);
        var result = SurchargeRating.Evaluate([candidate], ctx);
        Assert.Empty(result.Applied);
        Assert.Contains(result.EligibleNotActivated, x => x.ReasonCode == SurchargeReasonCodes.NotActivatedConditionFalse);
    }

    [Fact]
    public void TC08_Auto_True_Applied()
    {
        var candidate = AutoDgCandidate(80_000m);
        var ctx = Ctx(dangerous: true);
        var result = SurchargeRating.Evaluate([candidate], ctx);
        Assert.Single(result.Applied);
        Assert.Equal(80_000m, result.Applied[0].AmountOriginal);
    }

    [Fact]
    public void TC09_Auto_Unknown_NeedsContext()
    {
        var candidate = AutoDgCandidate(80_000m);
        var ctx = Ctx(dangerous: null);
        var result = SurchargeRating.Evaluate([candidate], ctx);
        Assert.Empty(result.Applied);
        Assert.Contains(result.EligibleNotActivated, x =>
            x.ReasonCode is SurchargeReasonCodes.NeedsContext or SurchargeReasonCodes.DataUnknown);
    }

    [Fact]
    public void TC10_Mandatory_AppliesWithoutSelection()
    {
        var candidate = MandatoryCandidate("FSC", 50_000m);
        var result = SurchargeRating.Evaluate([candidate], Ctx());
        Assert.Single(result.Applied);
        Assert.Equal(50_000m, result.Applied[0].AmountOriginal);
    }

    [Fact]
    public void TC12_BuyRule_DoesNotPriceSellSelection()
    {
        var buy = OptionalCandidate("PACK", null, 300_000m) with { Direction = SurchargeDirections.Buy };
        var ctx = Ctx(
            direction: SurchargeDirections.Sell,
            selections: [new ServiceSelectionSnapshot("BILL", Guid.NewGuid(), "PACK", "BILL", 1m, true, 1, null, null)]);
        var result = SurchargeRating.Evaluate([buy], ctx);
        Assert.Empty(result.Applied);
        Assert.Contains(result.UnpricedServices, s => s.Contains("UNPRICED", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TC15_FixedRate_PerUnit_MultipliesQuantity()
    {
        var perUnit = OptionalCandidate("PACK", null, 300_000m, basis: "per_unit");
        var perBill = OptionalCandidate("PACK2", null, 300_000m, basis: "per_bill");
        var selections = new[]
        {
            new ServiceSelectionSnapshot("BILL", Guid.NewGuid(), "PACK", "BILL", 2m, true, 1, null, null),
            new ServiceSelectionSnapshot("BILL", Guid.NewGuid(), "PACK2", "BILL", 2m, true, 1, null, null)
        };
        var unit = SurchargeRating.Evaluate([perUnit], Ctx(selections: selections));
        var bill = SurchargeRating.Evaluate([perBill], Ctx(selections: selections));
        Assert.Equal(600_000m, unit.Applied.Single().AmountOriginal);
        Assert.Equal(300_000m, bill.Applied.Single().AmountOriginal);
    }

    [Fact]
    public void TC21_Unclassified_Excluded()
    {
        var candidate = MandatoryCandidate("OLD", 10_000m) with
        {
            ActivationPolicy = SurchargeActivationPolicies.UnclassifiedLegacy,
            ApprovalRef = null
        };
        var result = SurchargeRating.Evaluate([candidate], Ctx());
        Assert.Empty(result.Applied);
        Assert.Contains(result.Excluded, x => x.ReasonCode == SurchargeReasonCodes.ExcludedUnclassified);
        Assert.NotEmpty(result.UnclassifiedWarnings);
    }

    [Fact]
    public async Task TC01_TC02_TC19_Api_OptionalSelection_AndExactVersionRead()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-OPT");
        var (_, versionId) = await PublishedCardAsync(tenantId, "RC-OPT", "vendor", "VND", 1_000_000m);
        var created = await CreateSurchargeAsync(tenantId, new
        {
            code = "DONGGOI",
            name = "Đóng gói",
            direction = "buy",
            calculationMode = "fixed_rate",
            basis = "per_bill",
            currencyCode = "VND",
            rateAmountPercent = 300_000m,
            routeCode = "SGN-HAN",
            publish = true,
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = "DONGGOI",
            selectionScope = "BILL"
        });

        var without = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 1m,
            routeCode = "SGN-HAN"
        }));
        Assert.DoesNotContain(without.Details, d => d.ComponentCode == "DONGGOI");
        Assert.Contains("NOT_SELECTED", without.ContextJson ?? "", StringComparison.OrdinalIgnoreCase);

        await PutSelectionAsync(tenantId, "BILL", billId, "DONGGOI", true, 1m);
        var with = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 1m,
            routeCode = "SGN-HAN"
        }));
        Assert.Equal(300_000m, with.Details.Single(d => d.ComponentCode == "DONGGOI").Amount);

        using var exact = Tenant(HttpMethod.Get, $"/api/surcharges/{created.SurchargeId}/versions/{created.VersionId}", tenantId);
        var exactRes = await _client.SendAsync(exact);
        exactRes.EnsureSuccessStatusCode();
        var payload = await exactRes.Content.ReadFromJsonAsync<JsonObject>(Json);
        Assert.Equal(created.VersionId.ToString(), payload!["version"]!["id"]!.GetValue<string>());
        Assert.True(payload["readOnly"]!.GetValue<bool>());
    }

    [Fact]
    public async Task TC11_PublishMandatoryWithoutApproval_Rejected()
    {
        var tenantId = await CreateTenantAsync();
        var created = await CreateSurchargeAsync(tenantId, new
        {
            code = "FUEL",
            name = "Fuel",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 10_000m,
            publish = false,
            activationPolicy = "MANDATORY_BY_POLICY",
            approvalRef = ""
        });
        using var publish = Tenant(HttpMethod.Post, $"/api/surcharges/{created.SurchargeId}/versions/{created.VersionId}/publish", tenantId);
        var res = await _client.SendAsync(publish);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task TC31_TenantIsolation_SelectionsDoNotLeak()
    {
        var a = await CreateTenantAsync();
        var b = await CreateTenantAsync();
        var billA = await CreateBillAsync(a, "BL-A");
        await PutSelectionAsync(a, "BILL", billA, "PACK", true, 1m);
        using var req = Tenant(HttpMethod.Get, $"/api/service-selections?contextType=BILL&contextId={billA}", b);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var rows = await res.Content.ReadFromJsonAsync<List<ServiceSelectionDto>>(Json);
        Assert.Empty(rows!);
    }

    private static SurchargeCandidate OptionalCandidate(string code, string? route, decimal amount, string? basis = "per_bill")
    {
        var config = SurchargeActivationConfigParser.Serialize(new ActivationConfig(code, "BILL", []));
        var conditions = string.IsNullOrWhiteSpace(route)
            ? Array.Empty<SurchargeCondition>()
            : new[]
            {
                new SurchargeCondition { Dimension = "route", Operator = "eq", ValueText = route }
            };
        return new SurchargeCandidate(
            Guid.NewGuid(), code, code, SurchargeDirections.Buy, Guid.NewGuid(), 3,
            DateTimeOffset.UtcNow.AddYears(-1), null, Guid.NewGuid(),
            SurchargeCalcModes.FixedRate, basis, "VND", amount, 0, null, null, null,
            conditions, [], [], null, null,
            SurchargeActivationPolicies.OptionalOptIn, config, null);
    }

    private static SurchargeCandidate AutoDgCandidate(decimal amount)
    {
        var config = SurchargeActivationConfigParser.Serialize(new ActivationConfig(
            null, null, [new ActivationPredicate("dangerous_goods", "eq", "true")]));
        return new SurchargeCandidate(
            Guid.NewGuid(), "DG", "DG", SurchargeDirections.Buy, Guid.NewGuid(), 1,
            DateTimeOffset.UtcNow.AddYears(-1), null, Guid.NewGuid(),
            SurchargeCalcModes.FixedRate, "per_bill", "VND", amount, 0, null, null, null,
            [], [], [], null, null,
            SurchargeActivationPolicies.AutoConditional, config, null);
    }

    private static SurchargeCandidate MandatoryCandidate(string code, decimal amount) =>
        new(
            Guid.NewGuid(), code, code, SurchargeDirections.Buy, Guid.NewGuid(), 1,
            DateTimeOffset.UtcNow.AddYears(-1), null, Guid.NewGuid(),
            SurchargeCalcModes.FixedRate, "per_bill", "VND", amount, 0, null, null, null,
            [], [], [], null, null,
            SurchargeActivationPolicies.MandatoryByPolicy, null, "POL-1");

    private static SurchargeMatchContext Ctx(
        string direction = SurchargeDirections.Buy,
        string? route = null,
        bool? dangerous = false,
        IReadOnlyList<ServiceSelectionSnapshot>? selections = null) =>
        new(
            DateTimeOffset.UtcNow,
            direction,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "road",
            null,
            route,
            null,
            null,
            null,
            dangerous,
            null,
            null,
            null,
            1m,
            1m,
            1m,
            1_000_000m,
            [],
            Guid.NewGuid(),
            null,
            null,
            null,
            selections ?? []);

    private async Task PutSelectionAsync(Guid tenantId, string contextType, Guid contextId, string serviceCode, bool selected, decimal qty)
    {
        using var req = Tenant(HttpMethod.Put, "/api/service-selections", tenantId);
        req.Content = JsonContent.Create(new
        {
            contextType,
            contextId,
            serviceCode,
            isSelected = selected,
            quantity = qty,
            scope = contextType
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }

    private async Task<(Guid CardId, Guid VersionId)> PublishedCardAsync(
        Guid tenantId, string code, string partyType, string currency, decimal unitAmount)
    {
        using var cardReq = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        cardReq.Content = JsonContent.Create(new { code, name = code, partyType, currencyCode = currency });
        var cardRes = await _client.SendAsync(cardReq);
        cardRes.EnsureSuccessStatusCode();
        var cardId = (await cardRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var verReq = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        verReq.Content = JsonContent.Create(new { note = "v", effectiveFrom = "2020-01-01T00:00:00Z" });
        var verRes = await _client.SendAsync(verReq);
        verRes.EnsureSuccessStatusCode();
        var versionId = (await verRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var ruleReq = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId);
        ruleReq.Content = JsonContent.Create(new { code = "BASE", name = "Cước chính", calcMethod = "unit_rate", unitAmount, currencyCode = currency, sortOrder = 1 });
        (await _client.SendAsync(ruleReq)).EnsureSuccessStatusCode();
        using var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        (await _client.SendAsync(publish)).EnsureSuccessStatusCode();
        return (cardId, versionId);
    }

    private async Task<CreatedSurcharge> CreateSurchargeAsync(Guid tenantId, object body)
    {
        var typeId = await EnsureChargeTypeAsync(tenantId);
        var node = JsonSerializer.SerializeToNode(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsObject();
        if (!node.ContainsKey("economicChargeTypeId"))
        {
            node["economicChargeTypeId"] = typeId.ToString();
        }

        var res = await PostAsync(tenantId, "/api/surcharges", node);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CreatedSurcharge>(Json))!;
    }

    private async Task<Guid> EnsureChargeTypeAsync(Guid tenantId)
    {
        if (_chargeTypes.TryGetValue(tenantId, out var existing))
        {
            return existing;
        }

        var created = await PostIdAsync(tenantId, "/api/economic-charge-types", new { code = "PACK", name = "Đóng gói" });
        _chargeTypes[tenantId] = created;
        return created;
    }

    private async Task<Guid> PostIdAsync(Guid tenantId, string url, object body)
    {
        var res = await PostAsync(tenantId, url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<HttpResponseMessage> PostAsync(Guid tenantId, string url, object body)
    {
        using var req = Tenant(HttpMethod.Post, url, tenantId);
        req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        var res = await PostAsync(tenantId, "/api/ratings", body);
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }

        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<RatingBody> GetAsync(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/ratings/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<RatingBody>(Json))!;
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
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "SurchargeApp" });
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
    private sealed record CreatedSurcharge(Guid SurchargeId, Guid VersionId);
    private sealed record RatingBody(Guid Id, decimal TotalAmount, string? ContextJson, List<DetailLine> Details);
    private sealed record DetailLine(string ComponentCode, decimal Amount, string? SourceType);
}
