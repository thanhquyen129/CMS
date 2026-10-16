using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class RateRatingBaselineApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public RateRatingBaselineApiTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Uat03_BasicAndAdvanced_RateEachRouteOnTheSameCardShape()
    {
        var tenantId = await CreateTenantAsync();
        var supplierId = await CreatePartyAsync(tenantId, "NCC-03", "vendor");
        var han = await CreateRouteAsync(tenantId, "SGN-HAN");
        var dad = await CreateRouteAsync(tenantId, "SGN-DAD");

        var basic = await PostCreatedAsync(tenantId, "/api/rate-cards", new
        {
            code = "RC-UAT03-B",
            name = "Hai tuyến cơ bản",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            explicitChargeGroups = true,
            supplierPartyId = supplierId,
            rules = new object[]
            {
                Rule("SGN-HAN", han, 11m),
                Rule("SGN-DAD", dad, 22m)
            }
        });
        await PublishAsync(tenantId, basic.RateVersionId);
        var billHan = await CreateBillAsync(tenantId, "BL-HAN");
        var billDad = await CreateBillAsync(tenantId, "BL-DAD");
        await PatchRouteAsync(tenantId, billHan, "SGN-HAN");
        await PatchRouteAsync(tenantId, billDad, "SGN-DAD");
        Assert.Equal(11m, await RateAmountAsync(tenantId, billHan, basic.RateVersionId));
        Assert.Equal(22m, await RateAmountAsync(tenantId, billDad, basic.RateVersionId));

        var advanced = await PostCreatedAsync(tenantId, "/api/rate-cards", new
        {
            code = "RC-UAT03-A",
            name = "Hai tuyến nâng cao",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "advanced",
            explicitChargeGroups = true,
            supplierPartyId = supplierId,
            groups = new object[]
            {
                Group("G1", han, 5m),
                Group("G2", dad, 9m)
            }
        });
        await PublishAsync(tenantId, advanced.RateVersionId);
        var commodityId = await PutIdAsync(tenantId, "/api/commodities", new { code = "GENERAL", name = "Hàng thường", isActive = true });
        await PatchAsync(tenantId, $"/api/operational-references/bill/{billHan}", new { changes = new { commodity_type_id = commodityId }, reason = "test" });
        await PatchAsync(tenantId, $"/api/operational-references/bill/{billDad}", new { changes = new { commodity_type_id = commodityId }, reason = "test" });
        Assert.Equal(5m, await RateAmountAsync(tenantId, billHan, advanced.RateVersionId));
        Assert.Equal(9m, await RateAmountAsync(tenantId, billDad, advanced.RateVersionId));
    }

    [Fact]
    public async Task Uat05_CarrierIsNotTheBuySupplier_AndLegRequiresThatSupplier()
    {
        var tenantId = await CreateTenantAsync();
        var supplierId = await CreatePartyAsync(tenantId, "NCC-05", "vendor");
        var otherSupplier = await CreatePartyAsync(tenantId, "NCC-05B", "vendor");
        var carrierId = await CreatePartyAsync(tenantId, "HANG-05", "carrier");
        var routeId = await CreateRouteAsync(tenantId, "HPH-HAN");
        var card = await PostCreatedAsync(tenantId, "/api/rate-cards", new
        {
            code = "RC-UAT05",
            name = "Tách hãng và nhà cung cấp",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            explicitChargeGroups = true,
            supplierPartyId = supplierId,
            rules = new[]
            {
                new
                {
                    code = "QT-LEG",
                    name = "Cước chặng",
                    calcMethod = "unit_rate",
                    unitAmount = 10m,
                    currencyCode = "USD",
                    routeId,
                    appliesAnyRoute = false,
                    chargeCode = "FREIGHT",
                    lineRole = "base_rate",
                    costTypeCode = "FREIGHT",
                    carrierPartyId = carrierId
                }
            }
        });
        await PublishAsync(tenantId, card.RateVersionId);
        var saved = await GetRulesAsync(tenantId, card.RateVersionId);
        Assert.Equal(carrierId, Assert.Single(saved).CarrierPartyId);
        Assert.NotEqual(supplierId, saved[0].CarrierPartyId);

        var billId = await CreateBillAsync(tenantId, "BL-UAT05");
        var shipmentId = await PutIdAsync(tenantId, "/api/shipments", new
        {
            shipmentNo = "SH-05",
            sourceSystem = "lcms_manual",
            externalId = "sh-05",
            transportMode = "road",
            carrierPartyId = carrierId,
            originCode = "HPH",
            destinationCode = "HAN",
            isActive = true
        });
        var legId = await PutIdAsync(tenantId, "/api/transport-legs", new
        {
            legNo = "LEG-05",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "leg-05",
            originCode = "HPH",
            destinationCode = "HAN",
            isActive = true
        });
        await PostEmptyAsync(tenantId, $"/api/transport-legs/{legId}/bills/{billId}");
        await PatchAsync(tenantId, $"/api/operational-references/leg/{legId}", new { changes = new { route_code = "HPH-HAN" }, reason = "test" });

        var wrong = await PostRawAsync(tenantId, "/api/ratings", new
        {
            billId,
            rateVersionId = card.RateVersionId,
            quantity = 4m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = otherSupplier
        });
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);

        var amount = await RateAmountAsync(tenantId, billId, card.RateVersionId, new
        {
            billId,
            rateVersionId = card.RateVersionId,
            quantity = 4m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = supplierId
        });
        Assert.Equal(40m, amount);
    }

    [Fact]
    public async Task Uat14_LegBuyUsesLegQuantity_AndRetryDoesNotAddCost()
    {
        var tenantId = await CreateTenantAsync();
        var supplierId = await CreatePartyAsync(tenantId, "NCC-14", "vendor");
        var card = await PostCreatedAsync(tenantId, "/api/rate-cards", new
        {
            code = "RC-UAT14",
            name = "Theo chặng",
            partyType = "vendor",
            currencyCode = "USD",
            declarationMode = "basic",
            explicitChargeGroups = true,
            supplierPartyId = supplierId,
            rules = new[]
            {
                new
                {
                    code = "QT-LEG",
                    name = "Cước chặng",
                    calcMethod = "unit_rate",
                    unitAmount = 10m,
                    currencyCode = "USD",
                    appliesAnyRoute = true,
                    chargeCode = "FREIGHT",
                    lineRole = "base_rate",
                    costTypeCode = "FREIGHT"
                }
            }
        });
        await PublishAsync(tenantId, card.RateVersionId);
        var billId = await CreateBillAsync(tenantId, "BL-UAT14");
        await PatchAsync(tenantId, $"/api/operational-references/bill/{billId}", new { changes = new { gross_weight_kg = "100" }, reason = "test" });
        var shipmentId = await PutIdAsync(tenantId, "/api/shipments", new
        {
            shipmentNo = "SH-14",
            sourceSystem = "lcms_manual",
            externalId = "sh-14",
            isActive = true
        });
        var legId = await PutIdAsync(tenantId, "/api/transport-legs", new
        {
            legNo = "LEG-14",
            shipmentId,
            sourceSystem = "lcms_manual",
            externalId = "leg-14",
            isActive = true
        });
        await PostEmptyAsync(tenantId, $"/api/transport-legs/{legId}/bills/{billId}");
        var body = new
        {
            billId,
            rateVersionId = card.RateVersionId,
            quantity = 4m,
            contextType = "leg",
            transportLegId = legId,
            vendorPartyId = supplierId,
            seedExpectedCosts = true
        };
        var first = await PostCreatedAsync(tenantId, "/api/ratings", body);
        var second = await PostCreatedAsync(tenantId, "/api/ratings", body);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(40m, await RatingAmountAsync(tenantId, first.Id));
        using var costs = Tenant(HttpMethod.Get, $"/api/costs?billId={billId}", tenantId);
        var listed = await _client.SendAsync(costs);
        listed.EnsureSuccessStatusCode();
        var rows = await listed.Content.ReadFromJsonAsync<List<CostRow>>(Json);
        Assert.Single(rows!);
        Assert.Equal(40m, rows![0].ExpectedAmount);
    }

    [Fact]
    public async Task Uat17_BuyWriteAndSellWriteStaySeparate()
    {
        var tenantId = await CreateTenantAsync();
        var costUser = await UserInRoleAsync(tenantId, "cost-uat17@example.com", "CostAccountant");
        var revenueUser = await UserInRoleAsync(tenantId, "rev-uat17@example.com", "RevenueAccountant");

        Assert.Equal(HttpStatusCode.Created, (await SendCardAsync(tenantId, costUser, "vendor", "RC-BUY-17")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendCardAsync(tenantId, costUser, "customer", "RC-SELL-DENY")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendCardAsync(tenantId, revenueUser, "customer", "RC-SELL-17")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendCardAsync(tenantId, revenueUser, "vendor", "RC-BUY-DENY")).StatusCode);
    }

    [Fact]
    public async Task Val12_ImportRejectsServiceAir_Water_AndBlankChargeCode()
    {
        var tenantId = await CreateTenantAsync();
        using var preview = Tenant(HttpMethod.Post, "/api/rate-imports/preview", tenantId);
        preview.Content = JsonContent.Create(new
        {
            cards = new object[]
            {
                new
                {
                    code = "RC-IMP-AIR",
                    name = "Nhập",
                    partyType = "vendor",
                    currencyCode = "USD",
                    rules = new object[]
                    {
                        new { code = "R1", name = "Dịch vụ AIR", calcMethod = "fixed", unitAmount = 1m, chargeCode = "FREIGHT", serviceTypeCode = "AIR" }
                    }
                },
                new
                {
                    code = "RC-IMP-WATER",
                    name = "Nhập thủy",
                    partyType = "vendor",
                    currencyCode = "USD",
                    transportMode = "WATER",
                    rules = new object[]
                    {
                        new { code = "R1", name = "Cước", calcMethod = "fixed", unitAmount = 1m, chargeCode = "FREIGHT" }
                    }
                },
                new
                {
                    code = "RC-IMP-BLANK",
                    name = "Thiếu nhóm",
                    partyType = "vendor",
                    currencyCode = "USD",
                    rules = new object[]
                    {
                        new { code = "R1", name = "Cước", calcMethod = "fixed", unitAmount = 1m }
                    }
                }
            }
        });
        var res = await _client.SendAsync(preview);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<PreviewBody>(Json);
        Assert.False(body!.CanCommit);
        Assert.Contains(body.Issues, issue => issue.Message.Contains("AIR", StringComparison.Ordinal));
        Assert.Contains(body.Issues, issue => issue.Message.Contains("WATER", StringComparison.Ordinal));
        Assert.Contains(body.Issues, issue => issue.Field == "chargeCode");
    }

    private static object Rule(string routeCode, Guid routeId, decimal amount, string calcMethod = "fixed") => new
    {
        code = "QT-" + routeCode,
        name = "Cước " + routeCode,
        calcMethod,
        unitAmount = amount,
        currencyCode = "USD",
        routeId,
        appliesAnyRoute = false,
        chargeCode = "FREIGHT",
        lineRole = "base_rate",
        costTypeCode = "FREIGHT"
    };

    private static object Group(string code, Guid routeId, decimal price) => new
    {
        code,
        routeId,
        appliesAnyRoute = false,
        costTypeCode = "FREIGHT",
        columns = new[] { new { code = "GENERAL", name = "Hàng thường" } },
        bands = new[] { new { minQuantity = 0m, maxQuantity = 100m, prices = new decimal?[] { price } } }
    };

    private async Task<decimal> RateAmountAsync(Guid tenantId, Guid billId, Guid versionId, object? extra = null)
    {
        var payload = extra ?? new { billId, rateVersionId = versionId, quantity = 1m };
        if (extra is not null)
        {
            var created = await PostCreatedAsync(tenantId, "/api/ratings", extra);
            return await RatingAmountAsync(tenantId, created.Id);
        }

        var id = await PostCreatedAsync(tenantId, "/api/ratings", new { billId, rateVersionId = versionId, quantity = 1m });
        return await RatingAmountAsync(tenantId, id.Id);
    }

    private async Task<decimal> RatingAmountAsync(Guid tenantId, Guid ratingId)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/ratings/{ratingId}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<RatingRow>(Json);
        return body!.TotalAmount;
    }

    private async Task<List<RuleRow>> GetRulesAsync(Guid tenantId, Guid versionId)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/rate-versions/{versionId}/rules", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<RuleRow>>(Json))!;
    }

    private async Task PublishAsync(Guid tenantId, Guid versionId)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
    }

    private async Task PatchRouteAsync(Guid tenantId, Guid billId, string routeCode)
    {
        await PatchAsync(tenantId, $"/api/bills/{billId}/context", new { routeCode });
    }

    private async Task PatchAsync(Guid tenantId, string path, object body)
    {
        using var req = Tenant(HttpMethod.Patch, path, tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
    }

    private async Task PostEmptyAsync(Guid tenantId, string path)
    {
        using var req = Tenant(HttpMethod.Post, path, tenantId);
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> PostRawAsync(Guid tenantId, string path, object body)
    {
        using var req = Tenant(HttpMethod.Post, path, tenantId);
        req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    private async Task<Created> PostCreatedAsync(Guid tenantId, string path, object body)
    {
        var res = await PostRawAsync(tenantId, path, body);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<Created>(text, Json)!;
    }

    private async Task<Guid> PutIdAsync(Guid tenantId, string path, object body)
    {
        using var req = Tenant(HttpMethod.Put, path, tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<Created>(text, Json)!.Id;
    }

    private async Task<HttpResponseMessage> SendCardAsync(Guid tenantId, Guid userId, string partyType, string code)
    {
        using var req = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Content = JsonContent.Create(new
        {
            code,
            name = code,
            partyType,
            currencyCode = "USD"
        });
        return await _client.SendAsync(req);
    }

    private async Task<Guid> UserInRoleAsync(Guid tenantId, string email, string roleCode)
    {
        var userId = await PostCreatedAsync(tenantId, "/api/users", new { email, displayName = email });
        using var rolesReq = Tenant(HttpMethod.Get, "/api/roles", tenantId);
        var roles = await (await _client.SendAsync(rolesReq)).Content.ReadFromJsonAsync<List<RoleRow>>(Json);
        var roleId = roles!.Single(role => role.Code == roleCode).Id;
        await PostEmptyAsync(tenantId, $"/api/users/{userId.Id}/roles/{roleId}");
        return userId.Id;
    }

    private async Task<Guid> CreatePartyAsync(Guid tenantId, string code, string role)
    {
        var created = await PostCreatedAsync(tenantId, "/api/business-parties", new { code, name = code, roleCodes = new[] { role } });
        return created.Id;
    }

    private async Task<Guid> CreateRouteAsync(Guid tenantId, string code)
    {
        var origin = await PutIdAsync(tenantId, "/api/locations", new { code = code + "-O", name = code, locationType = "airport", countryCode = "VN", isActive = true, aliases = Array.Empty<object>() });
        var destination = await PutIdAsync(tenantId, "/api/locations", new { code = code + "-D", name = code, locationType = "airport", countryCode = "VN", isActive = true, aliases = Array.Empty<object>() });
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

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        var created = await PostCreatedAsync(tenantId, "/api/bills", new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        return created.Id;
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Baseline" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Created>(Json))!.Id;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string path, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record Created(Guid Id, Guid RateCardId, Guid RateVersionId);
    private sealed record RatingRow(decimal TotalAmount);
    private sealed record RuleRow(Guid? CarrierPartyId);
    private sealed record CostRow(decimal? ExpectedAmount);
    private sealed record RoleRow(Guid Id, string Code);
    private sealed record PreviewBody(bool CanCommit, List<IssueRow> Issues);
    private sealed record IssueRow(string Field, string Message);
}
