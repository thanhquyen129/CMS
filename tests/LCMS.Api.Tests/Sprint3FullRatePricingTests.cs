using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint3FullRatePricingTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint3FullRatePricingTests(LcmsApiFactory factory)
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
    public async Task Applicability_FiltersRulesByServiceTypePartyAndRoute()
    {
        var tenantId = await CreateTenantAsync("TN-S3F-APP", "S3F Applicability");
        var billId = await CreateBillAsync(tenantId, "BL-S3F-APP", "freight");
        var cardId = await CreateRateCardAsync(tenantId, "RC-APP", "Bảng giá lọc");
        var versionId = await CreateVersionAsync(tenantId, cardId);

        await AddRuleAsync(tenantId, versionId, new
        {
            code = "FREIGHT_ONLY",
            name = "Cước freight",
            calcMethod = "fixed",
            unitAmount = 1000,
            currencyCode = "VND",
            serviceTypeCode = "freight",
            partyTypeCode = "vendor",
            routeCode = "HN-HCM",
            sortOrder = 1
        });
        await AddRuleAsync(tenantId, versionId, new
        {
            code = "CUSTOMS_ONLY",
            name = "Phí hải quan",
            calcMethod = "fixed",
            unitAmount = 500,
            currencyCode = "VND",
            serviceTypeCode = "customs",
            sortOrder = 2
        });

        await PublishAsync(tenantId, versionId);

        // Matches FREIGHT_ONLY only
        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 1,
            serviceTypeCode = "freight",
            partyTypeCode = "vendor",
            routeCode = "HN-HCM"
        });

        var rating = await GetRatingAsync(tenantId, ratingId);
        Assert.Equal(1000m, rating.TotalAmount);
        Assert.Single(rating.Details);
        Assert.Equal("FREIGHT_ONLY", rating.Details[0].RuleCode);

        // Wrong route → no applicable rules
        using var badRoute = new HttpRequestMessage(HttpMethod.Post, "/api/ratings")
        {
            Content = JsonContent.Create(new
            {
                billId,
                rateVersionId = versionId,
                quantity = 1,
                serviceTypeCode = "freight",
                partyTypeCode = "vendor",
                routeCode = "DN-HCM"
            })
        };
        badRoute.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var blocked = await _client.SendAsync(badRoute);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Contains("phù hợp", err!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PercentOfBase_And_MinMaxClamp_ComputeExpectedAmounts()
    {
        var tenantId = await CreateTenantAsync("TN-S3F-FORM", "S3F Formula");
        var billId = await CreateBillAsync(tenantId, "BL-S3F-FORM", "freight");
        var cardId = await CreateRateCardAsync(tenantId, "RC-FORM", "Bảng giá công thức");
        var versionId = await CreateVersionAsync(tenantId, cardId);

        await AddRuleAsync(tenantId, versionId, new
        {
            code = "BASE",
            name = "Cước cơ bản",
            calcMethod = "unit_rate",
            unitAmount = 1000,
            currencyCode = "VND",
            sortOrder = 1
        });
        await AddRuleAsync(tenantId, versionId, new
        {
            code = "FUEL",
            name = "Phụ phí nhiên liệu 10%",
            calcMethod = "percent_of_base",
            unitAmount = 10,
            currencyCode = "VND",
            sortOrder = 2
        });
        await AddRuleAsync(tenantId, versionId, new
        {
            code = "CAP",
            name = "Cước có trần",
            calcMethod = "min_max_clamp",
            unitAmount = 2000,
            currencyCode = "VND",
            minAmount = 500,
            maxAmount = 3000,
            sortOrder = 3
        });

        await PublishAsync(tenantId, versionId);

        // qty=2 → BASE 2000; FUEL 10% of running 2000 = 200; CAP raw 4000 clamp max 3000
        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 2
        });

        var rating = await GetRatingAsync(tenantId, ratingId);
        Assert.Equal(3, rating.Details.Count);
        Assert.Equal(2000m, rating.Details.Single(d => d.RuleCode == "BASE").Amount);
        Assert.Equal(200m, rating.Details.Single(d => d.RuleCode == "FUEL").Amount);
        Assert.Equal(3000m, rating.Details.Single(d => d.RuleCode == "CAP").Amount);
        Assert.Equal(5200m, rating.TotalAmount);

        // Explicit baseAmount for percent (ignore running): 10% of 10000 = 1000 on FUEL only scenario via separate version
        var v2 = await CreateVersionAsync(tenantId, cardId);
        await AddRuleAsync(tenantId, v2, new
        {
            code = "PCT",
            name = "Phần trăm cơ sở tường minh",
            calcMethod = "percent_of_base",
            unitAmount = 10,
            currencyCode = "VND",
            sortOrder = 1
        });
        await PublishAsync(tenantId, v2);
        var pctId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = v2,
            quantity = 1,
            baseAmount = 10000
        });
        var pct = await GetRatingAsync(tenantId, pctId);
        Assert.Equal(1000m, pct.TotalAmount);
    }

    [Fact]
    public async Task Rerate_SupersedesPrior_KeepsImmutableHistory_And_ListsByBill()
    {
        var tenantId = await CreateTenantAsync("TN-S3F-RR", "S3F Rerate");
        var billId = await CreateBillAsync(tenantId, "BL-S3F-RR", "freight");
        var cardId = await CreateRateCardAsync(tenantId, "RC-RR", "Bảng giá re-rate");
        var versionId = await CreateVersionAsync(tenantId, cardId);
        await AddRuleAsync(tenantId, versionId, new
        {
            code = "FREIGHT",
            name = "Cước",
            calcMethod = "unit_rate",
            unitAmount = 1000,
            currencyCode = "VND",
            sortOrder = 1
        });
        await PublishAsync(tenantId, versionId);

        var firstId = await RateAsync(tenantId, new { billId, rateVersionId = versionId, quantity = 3 });
        var first = await GetRatingAsync(tenantId, firstId);
        Assert.Equal("completed", first.Status);
        Assert.Equal(3000m, first.TotalAmount);
        Assert.Null(first.SupersedesRatingId);

        var secondId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 5,
            weight = 5,
            supersedesRatingId = firstId
        });
        var second = await GetRatingAsync(tenantId, secondId);
        Assert.Equal("completed", second.Status);
        Assert.Equal(firstId, second.SupersedesRatingId);
        Assert.Equal(5000m, second.TotalAmount);
        Assert.Equal(5m, second.Weight);

        var prior = await GetRatingAsync(tenantId, firstId);
        Assert.Equal("superseded", prior.Status);
        Assert.Equal(3000m, prior.TotalAmount); // history immutable amounts
        Assert.Single(prior.Details);

        using var list = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billId}/ratings");
        list.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var listRes = await _client.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var history = await listRes.Content.ReadFromJsonAsync<List<RatingHistoryItem>>(JsonOptions);
        Assert.Equal(2, history!.Count);
        Assert.Contains(history, h => h.Id == firstId && h.Status == "superseded");
        Assert.Contains(history, h => h.Id == secondId && h.Status == "completed" && h.SupersedesRatingId == firstId);

        // Cannot supersede an already-superseded rating
        using var again = new HttpRequestMessage(HttpMethod.Post, "/api/ratings")
        {
            Content = JsonContent.Create(new
            {
                billId,
                rateVersionId = versionId,
                quantity = 1,
                supersedesRatingId = firstId
            })
        };
        again.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var blocked = await _client.SendAsync(again);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Contains("thay thế", err!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SeedExpectedCosts_OnRating_CreatesCosts_IdempotentViaExistingSeed()
    {
        var tenantId = await CreateTenantAsync("TN-S3F-SEED", "S3F Seed");
        var billId = await CreateBillAsync(tenantId, "BL-S3F-SEED", "freight");
        var cardId = await CreateRateCardAsync(tenantId, "RC-SEED", "Bảng giá seed");
        var versionId = await CreateVersionAsync(tenantId, cardId);
        var ruleId = await AddRuleAsync(tenantId, versionId, new
        {
            code = "FREIGHT",
            name = "Cước",
            calcMethod = "fixed",
            unitAmount = 1500,
            currencyCode = "VND",
            sortOrder = 1
        });

        using var addComp = new HttpRequestMessage(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/components")
        {
            Content = JsonContent.Create(new
            {
                code = "LINEHAUL",
                name = "Cước chính",
                financialNature = "cost",
                costTypeCode = "FREIGHT",
                amount = 1500,
                currencyCode = "VND",
                sortOrder = 1
            })
        };
        addComp.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(addComp)).StatusCode);

        await PublishAsync(tenantId, versionId);

        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 1,
            seedExpectedCosts = true
        });

        using var listCosts = new HttpRequestMessage(HttpMethod.Get, $"/api/costs?billId={billId}");
        listCosts.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var costsRes = await _client.SendAsync(listCosts);
        Assert.Equal(HttpStatusCode.OK, costsRes.StatusCode);
        var costs = await costsRes.Content.ReadFromJsonAsync<List<CostListItem>>(JsonOptions);
        Assert.Single(costs!);
        Assert.Equal(1500m, costs[0].Amount);
        Assert.Equal("expected", costs[0].FinancialMaturity);

        // Explicit seed endpoint remains idempotent
        using var seedAgain = new HttpRequestMessage(HttpMethod.Post, $"/api/ratings/{ratingId}/seed-expected-costs");
        seedAgain.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var seedRes = await _client.SendAsync(seedAgain);
        Assert.Equal(HttpStatusCode.OK, seedRes.StatusCode);
        var seedBody = await seedRes.Content.ReadFromJsonAsync<SeedResult>(JsonOptions);
        Assert.Equal(0, seedBody!.CreatedCount);
        Assert.Equal(1, seedBody.ExistingCount);
    }

    [Fact]
    public async Task Publish_RemainsImmutable_And_CrossTenantRatingHidden()
    {
        var tenantA = await CreateTenantAsync("TN-S3F-A", "S3F A");
        var tenantB = await CreateTenantAsync("TN-S3F-B", "S3F B");
        var billA = await CreateBillAsync(tenantA, "BL-S3F-ISO", "freight");
        var cardId = await CreateRateCardAsync(tenantA, "RC-ISO", "Card ISO");
        var versionId = await CreateVersionAsync(tenantA, cardId);
        await AddRuleAsync(tenantA, versionId, new
        {
            code = "R1",
            name = "Rule",
            calcMethod = "fixed",
            unitAmount = 100,
            currencyCode = "VND",
            sortOrder = 1
        });
        await PublishAsync(tenantA, versionId);

        using var addRule = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules")
        {
            Content = JsonContent.Create(new
            {
                code = "R2",
                name = "Blocked",
                calcMethod = "fixed",
                unitAmount = 1,
                currencyCode = "VND"
            })
        };
        addRule.Headers.Add("X-Tenant-Id", tenantA.ToString());
        var blocked = await _client.SendAsync(addRule);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Contains("không được sửa", err!.Message, StringComparison.OrdinalIgnoreCase);

        var ratingId = await RateAsync(tenantA, new { billId = billA, rateVersionId = versionId, quantity = 1 });

        using var getAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/ratings/{ratingId}");
        getAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(getAsB)).StatusCode);

        using var listAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billA}/ratings");
        listAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(listAsB)).StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo, string billType)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo, billType })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> CreateRateCardAsync(Guid tenantId, string code, string name)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/rate-cards")
        {
            Content = JsonContent.Create(new
            {
                code,
                name,
                partyType = "vendor",
                currencyCode = "VND",
                description = (string?)null
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> CreateVersionAsync(Guid tenantId, Guid rateCardId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-cards/{rateCardId}/versions")
        {
            Content = JsonContent.Create(new { note = "draft" })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> AddRuleAsync(Guid tenantId, Guid versionId, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return created!.Id;
    }

    private async Task PublishAsync(Guid tenantId, Guid versionId)
    {
        using var publish = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish");
        publish.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(publish)).StatusCode);
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/ratings")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return created!.Id;
    }

    private async Task<RatingResponse> GetRatingAsync(Guid tenantId, Guid ratingId)
    {
        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/ratings/{ratingId}");
        get.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var getRes = await _client.SendAsync(get);
        getRes.EnsureSuccessStatusCode();
        return (await getRes.Content.ReadFromJsonAsync<RatingResponse>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record RatingDetailResponse(
        Guid Id,
        string RuleCode,
        string ComponentCode,
        string ComponentName,
        string FinancialNature,
        string FinancialMaturity,
        decimal Amount,
        string CurrencyCode);

    private sealed record RatingResponse(
        Guid Id,
        Guid BillId,
        Guid RateVersionId,
        DateTimeOffset RatedAt,
        string CurrencyCode,
        decimal TotalAmount,
        decimal Quantity,
        decimal? Weight,
        string? ServiceTypeCode,
        string? PartyTypeCode,
        string? RouteCode,
        decimal? BaseAmount,
        string Status,
        Guid? SupersedesRatingId,
        List<RatingDetailResponse> Details);

    private sealed record RatingHistoryItem(
        Guid Id,
        Guid BillId,
        Guid RateVersionId,
        DateTimeOffset RatedAt,
        string CurrencyCode,
        decimal TotalAmount,
        decimal Quantity,
        decimal? Weight,
        string Status,
        Guid? SupersedesRatingId);

    private sealed record CostListItem(
        Guid Id,
        decimal Amount,
        string FinancialMaturity);

    private sealed record SeedResult(Guid RatingId, int CreatedCount, int ExistingCount, List<Guid> CostIds);

    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
