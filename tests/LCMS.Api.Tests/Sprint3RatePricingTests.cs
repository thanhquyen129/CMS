using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint3RatePricingTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint3RatePricingTests(LcmsApiFactory factory)
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
    public async Task Publish_MakesRateVersionImmutable_RequiresNewVersion()
    {
        var tenantId = await CreateTenantAsync("TN-RATE-IMM", "Rate Immutable");
        var cardId = await CreateRateCardAsync(tenantId, "RC-VND", "Bảng giá nội địa");
        var versionId = await CreateVersionAsync(tenantId, cardId);
        var ruleId = await AddRuleAsync(tenantId, versionId, "FREIGHT", "Cước vận chuyển", "unit_rate", 1000m);

        using var publish = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish");
        publish.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(publish)).StatusCode);

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/rate-versions/{versionId}");
        get.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var getRes = await _client.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);
        var version = await getRes.Content.ReadFromJsonAsync<RateVersionResponse>(JsonOptions);
        Assert.Equal("published", version!.Status);
        Assert.NotNull(version.PublishedAt);

        using var addRule = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules")
        {
            Content = JsonContent.Create(new
            {
                code = "EXTRA",
                name = "Phụ phí",
                calcMethod = "fixed",
                unitAmount = 50,
                currencyCode = "VND"
            })
        };
        addRule.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var blocked = await _client.SendAsync(addRule);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Contains("không được sửa", err!.Message, StringComparison.OrdinalIgnoreCase);

        using var addComp = new HttpRequestMessage(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/components")
        {
            Content = JsonContent.Create(new
            {
                code = "BASE",
                name = "Cước cơ bản",
                financialNature = "cost",
                amount = 1000,
                currencyCode = "VND"
            })
        };
        addComp.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(addComp)).StatusCode);

        // New draft version is allowed (C-011 — version instead of overwrite)
        var v2 = await CreateVersionAsync(tenantId, cardId);
        await AddRuleAsync(tenantId, v2, "FREIGHT", "Cước vận chuyển v2", "unit_rate", 1100m);

        using var list = new HttpRequestMessage(HttpMethod.Get, $"/api/rate-cards/{cardId}/versions");
        list.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var listRes = await _client.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
        var versions = await listRes.Content.ReadFromJsonAsync<List<RateVersionResponse>>(JsonOptions);
        Assert.Equal(2, versions!.Count);
        Assert.Contains(versions, v => v.VersionNo == 1 && v.Status == "published");
        Assert.Contains(versions, v => v.VersionNo == 2 && v.Status == "draft");
    }

    [Fact]
    public async Task RateCard_CrossTenant_Returns404()
    {
        var tenantA = await CreateTenantAsync("TN-RATE-A", "Rate A");
        var tenantB = await CreateTenantAsync("TN-RATE-B", "Rate B");
        var cardId = await CreateRateCardAsync(tenantA, "RC-A", "Card A");

        using var getAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/rate-cards/{cardId}");
        getAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        var leaked = await _client.SendAsync(getAsB);
        Assert.Equal(HttpStatusCode.NotFound, leaked.StatusCode);

        var err = await leaked.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("not_found", err!.Code);
        Assert.DoesNotContain(cardId.ToString(), err.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRating_CreatesExpectedSnapshotDetails()
    {
        var tenantId = await CreateTenantAsync("TN-RATE-SNAP", "Rate Snapshot");
        var billId = await CreateBillAsync(tenantId, "BL-RATE-1", "freight");
        var cardId = await CreateRateCardAsync(tenantId, "RC-SNAP", "Bảng giá snapshot");
        var versionId = await CreateVersionAsync(tenantId, cardId);
        var ruleId = await AddRuleAsync(tenantId, versionId, "FREIGHT", "Cước", "unit_rate", 1000m);

        using var addComp = new HttpRequestMessage(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/components")
        {
            Content = JsonContent.Create(new
            {
                code = "LINEHAUL",
                name = "Cước chính",
                financialNature = "cost",
                costTypeCode = "FREIGHT",
                amount = 1000,
                currencyCode = "VND",
                sortOrder = 1
            })
        };
        addComp.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(addComp)).StatusCode);

        using var publish = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish");
        publish.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(publish)).StatusCode);

        using var rateReq = new HttpRequestMessage(HttpMethod.Post, "/api/ratings")
        {
            Content = JsonContent.Create(new
            {
                billId,
                rateVersionId = versionId,
                quantity = 3
            })
        };
        rateReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var rateRes = await _client.SendAsync(rateReq);
        Assert.Equal(HttpStatusCode.Created, rateRes.StatusCode);
        var created = await rateRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        Assert.NotEqual(Guid.Empty, created!.Id);

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/ratings/{created.Id}");
        get.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var getRes = await _client.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, getRes.StatusCode);

        var rating = await getRes.Content.ReadFromJsonAsync<RatingResponse>(JsonOptions);
        Assert.NotNull(rating);
        Assert.Equal(billId, rating!.BillId);
        Assert.Equal(versionId, rating.RateVersionId);
        Assert.Equal(3m, rating.Quantity);
        Assert.Equal(3000m, rating.TotalAmount);
        Assert.Single(rating.Details);
        Assert.Equal("LINEHAUL", rating.Details[0].ComponentCode);
        Assert.Equal("expected", rating.Details[0].FinancialMaturity);
        Assert.Equal(3000m, rating.Details[0].Amount);
        Assert.Equal("cost", rating.Details[0].FinancialNature);

        // Re-rating creates another snapshot (history), does not overwrite
        using var rateAgain = new HttpRequestMessage(HttpMethod.Post, "/api/ratings")
        {
            Content = JsonContent.Create(new { billId, rateVersionId = versionId, quantity = 2 })
        };
        rateAgain.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var againRes = await _client.SendAsync(rateAgain);
        Assert.Equal(HttpStatusCode.Created, againRes.StatusCode);
        var again = await againRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        Assert.NotEqual(created.Id, again!.Id);
    }

    [Fact]
    public async Task DraftComponent_CanUpdateAndDelete_PublishedVersionRejectsDelete()
    {
        var tenantId = await CreateTenantAsync("TN-COMP-CRUD", "Component CRUD");
        var cardId = await CreateRateCardAsync(tenantId, "RC-COMP", "Bảng thành phần");
        var versionId = await CreateVersionAsync(tenantId, cardId);
        var ruleId = await AddRuleAsync(tenantId, versionId, "FREIGHT", "Cước", "fixed", 1000m);

        Guid componentId;
        using (var add = new HttpRequestMessage(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/components")
        {
            Content = JsonContent.Create(new
            {
                code = "LINE",
                name = "Cước dòng",
                financialNature = "cost",
                amount = 100m,
                currencyCode = "VND"
            })
        })
        {
            add.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var created = await _client.SendAsync(add);
            created.EnsureSuccessStatusCode();
            componentId = (await created.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        using (var update = new HttpRequestMessage(HttpMethod.Put, $"/api/pricing-rules/components/{componentId}")
        {
            Content = JsonContent.Create(new
            {
                name = "Cước dòng sửa",
                financialNature = "cost",
                amount = 250m,
                currencyCode = "VND"
            })
        })
        {
            update.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(update)).StatusCode);
        }

        var afterUpdate = await ListRuleComponentsAsync(tenantId, versionId);
        Assert.Contains(afterUpdate, c => c.Id == componentId && c.Amount == 250m && c.Name == "Cước dòng sửa");

        using (var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/pricing-rules/components/{componentId}"))
        {
            delete.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(delete)).StatusCode);
        }

        Assert.DoesNotContain(await ListRuleComponentsAsync(tenantId, versionId), c => c.Id == componentId);

        Guid lockedId;
        using (var add = new HttpRequestMessage(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/components")
        {
            Content = JsonContent.Create(new
            {
                code = "LOCK",
                name = "Khóa",
                financialNature = "revenue",
                amount = 10m,
                currencyCode = "VND"
            })
        })
        {
            add.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var created = await _client.SendAsync(add);
            created.EnsureSuccessStatusCode();
            lockedId = (await created.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        using (var publish = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish"))
        {
            publish.Headers.Add("X-Tenant-Id", tenantId.ToString());
            (await _client.SendAsync(publish)).EnsureSuccessStatusCode();
        }

        using var blocked = new HttpRequestMessage(HttpMethod.Delete, $"/api/pricing-rules/components/{lockedId}");
        blocked.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var blockedRes = await _client.SendAsync(blocked);
        Assert.Equal(HttpStatusCode.Conflict, blockedRes.StatusCode);
        Assert.Contains(await ListRuleComponentsAsync(tenantId, versionId), c => c.Id == lockedId);
    }

    private async Task<List<ComponentBody>> ListRuleComponentsAsync(Guid tenantId, Guid versionId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/rate-versions/{versionId}/rules");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var rules = await res.Content.ReadFromJsonAsync<List<RuleWithComponents>>(JsonOptions);
        return rules?.SelectMany(r => r.Components ?? []).ToList() ?? [];
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

    private async Task<Guid> AddRuleAsync(
        Guid tenantId,
        Guid versionId,
        string code,
        string name,
        string calcMethod,
        decimal unitAmount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules")
        {
            Content = JsonContent.Create(new
            {
                code,
                name,
                calcMethod,
                unitAmount,
                currencyCode = "VND",
                sortOrder = 1
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record ComponentBody(Guid Id, string Name, decimal Amount);
    private sealed record RuleWithComponents(Guid Id, List<ComponentBody>? Components);

    private sealed record RateVersionResponse(
        Guid Id,
        Guid RateCardId,
        int VersionNo,
        string Status,
        DateTimeOffset? EffectiveFrom,
        DateTimeOffset? EffectiveTo,
        DateTimeOffset? PublishedAt,
        string? Note);

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
        string Status,
        List<RatingDetailResponse> Details);

    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
