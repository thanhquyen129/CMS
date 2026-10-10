using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class IndependentSurchargeTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private readonly Dictionary<Guid, Guid> _chargeTypes = new();
    private HttpClient _client = null!;

    public IndependentSurchargeTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task IndependentFsc_CombinesWithBase_AndSkipsNonDg()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-FSC");
        var (cardId, versionId) = await PublishedCardAsync(tenantId, "RC-AIR", "vendor", "VND", 17_000m);
        await CreateSurchargeAsync(tenantId, new
        {
            code = "FSC",
            name = "Fuel",
            direction = "buy",
            calculationMode = "unit_rate",
            basis = "chargeable_weight",
            currencyCode = "VND",
            rateAmountPercent = 2_000m,
            transportMode = "air",
            publish = true
        });
        await CreateSurchargeAsync(tenantId, new
        {
            code = "SSC",
            name = "Security",
            direction = "buy",
            calculationMode = "unit_rate",
            basis = "chargeable_weight",
            currencyCode = "VND",
            rateAmountPercent = 1_000m,
            transportMode = "air",
            publish = true
        });
        await CreateSurchargeAsync(tenantId, new
        {
            code = "HDL",
            name = "Handling",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 50_000m,
            transportMode = "air",
            publish = true
        });
        await CreateSurchargeAsync(tenantId, new
        {
            code = "DG",
            name = "Dangerous goods",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 300_000m,
            transportMode = "air",
            dangerousGoods = true,
            publish = true
        });

        var rating = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 100m,
            transportMode = "air"
        }));

        Assert.Equal(2_050_000m, rating.TotalAmount);
        Assert.Equal(1_700_000m, rating.Details.Single(d => d.SourceType == "base_rate").Amount);
        Assert.Equal(3, rating.Details.Count(d => d.SourceType == "surcharge"));
        Assert.DoesNotContain(rating.Details, d => d.ComponentCode == "DG");
        Assert.All(rating.Details, d => Assert.Equal("expected", d.FinancialMaturity));
        Assert.NotEqual(Guid.Empty, rating.Details.First(d => d.ComponentCode == "FSC").SourceVersionId);
        _ = cardId;
    }

    [Fact]
    public async Task OptionalRateCardScope_AppliesOnlyToThatCard()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-SCOPE");
        var (cardA, versionA) = await PublishedCardAsync(tenantId, "RC-A", "vendor", "VND", 10_000m);
        var (_, versionB) = await PublishedCardAsync(tenantId, "RC-B", "vendor", "VND", 10_000m);
        await CreateSurchargeAsync(tenantId, new
        {
            code = "FSC",
            name = "Fuel A",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 80_000m,
            rateCardId = cardA,
            publish = true
        });

        var onA = await GetAsync(tenantId, await RateAsync(tenantId, new { billId, rateVersionId = versionA, quantity = 1m }));
        var onB = await GetAsync(tenantId, await RateAsync(tenantId, new { billId, rateVersionId = versionB, quantity = 1m }));
        Assert.Contains(onA.Details, d => d.ComponentCode == "FSC");
        Assert.DoesNotContain(onB.Details, d => d.ComponentCode == "FSC");
    }

    [Fact]
    public async Task BuyAndSell_WriteExpectedCostAndRevenue()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-SIDE");
        var (_, buyVersion) = await PublishedCardAsync(tenantId, "RC-BUY", "vendor", "VND", 1_000m);
        var (_, sellVersion) = await PublishedCardAsync(tenantId, "RC-SELL", "customer", "VND", 1_000m);
        await CreateSurchargeAsync(tenantId, new
        {
            code = "FSC-BUY",
            name = "Fuel mua",
            direction = "buy",
            calculationMode = "unit_rate",
            currencyCode = "VND",
            rateAmountPercent = 2_000m,
            publish = true
        });
        await CreateSurchargeAsync(tenantId, new
        {
            code = "FSC-SELL",
            name = "Fuel bán",
            direction = "sell",
            calculationMode = "unit_rate",
            currencyCode = "VND",
            rateAmountPercent = 3_000m,
            publish = true
        });

        var buyId = await RateAsync(tenantId, new { billId, rateVersionId = buyVersion, quantity = 1m, seedExpectedCosts = true });
        var sellId = await RateAsync(tenantId, new { billId, rateVersionId = sellVersion, quantity = 1m, seedExpectedRevenues = true });
        var buy = await GetAsync(tenantId, buyId);
        var sell = await GetAsync(tenantId, sellId);
        Assert.Equal("cost", buy.Details.Single(d => d.ComponentCode == "FSC-BUY").FinancialNature);
        Assert.Equal(3_000m, sell.Details.Single(d => d.ComponentCode == "FSC-SELL").Amount);
        Assert.Equal("revenue", sell.Details.Single(d => d.ComponentCode == "FSC-SELL").FinancialNature);
        Assert.DoesNotContain(buy.Details, d => d.ComponentCode == "FSC-SELL");

        var costs = await GetJsonAsync(tenantId, $"/api/costs?billId={billId}");
        Assert.Contains("\"financialMaturity\":\"expected\"", costs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"financialMaturity\":\"actual\"", costs, StringComparison.OrdinalIgnoreCase);
        var revenues = await GetJsonAsync(tenantId, $"/api/revenues?billId={billId}");
        Assert.Contains("\"financialMaturity\":\"expected\"", revenues, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VersionChange_DoesNotCreateRateVersion_AndAsOfSelectsTheWindow()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-VER");
        var (cardId, versionId) = await PublishedCardAsync(tenantId, "RC-VER", "vendor", "VND", 17_000m);
        var before = await GetJsonAsync(tenantId, $"/api/rate-cards/{cardId}/versions");
        var created = await CreateSurchargeAsync(tenantId, new
        {
            code = "FSC",
            name = "Fuel",
            direction = "buy",
            calculationMode = "unit_rate",
            currencyCode = "VND",
            rateAmountPercent = 2_000m,
            validFrom = "2020-01-01T00:00:00Z",
            validTo = "2026-01-01T00:00:00Z",
            publish = true
        });
        var draftId = await PostIdAsync(tenantId, $"/api/surcharges/{created.SurchargeId}/versions", new { });
        using var update = Tenant(HttpMethod.Put, $"/api/surcharges/{created.SurchargeId}/versions/{draftId}", tenantId);
        update.Content = JsonContent.Create(new
        {
            code = "FSC",
            name = "Fuel",
            direction = "buy",
            calculationMode = "unit_rate",
            currencyCode = "VND",
            rateAmountPercent = 2_500m,
            validFrom = "2026-01-02T00:00:00Z"
        });
        (await _client.SendAsync(update)).EnsureSuccessStatusCode();
        using var publish = Tenant(HttpMethod.Post, $"/api/surcharges/{created.SurchargeId}/versions/{draftId}/publish", tenantId);
        (await _client.SendAsync(publish)).EnsureSuccessStatusCode();
        var after = await GetJsonAsync(tenantId, $"/api/rate-cards/{cardId}/versions");
        Assert.Equal(before, after);

        using var blocked = Tenant(HttpMethod.Put, $"/api/surcharges/{created.SurchargeId}/versions/{created.VersionId}", tenantId);
        blocked.Content = JsonContent.Create(new
        {
            code = "FSC",
            name = "Fuel",
            direction = "buy",
            calculationMode = "unit_rate",
            currencyCode = "VND",
            rateAmountPercent = 9_999m
        });
        var immutable = await _client.SendAsync(blocked);
        Assert.Equal(HttpStatusCode.Conflict, immutable.StatusCode);

        var early = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 100m,
            rateDate = "2025-06-01T00:00:00Z"
        }));
        var late = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 100m,
            rateDate = "2026-06-01T00:00:00Z"
        }));
        Assert.Equal(200_000m, early.Details.Single(d => d.ComponentCode == "FSC").Amount);
        Assert.Equal(250_000m, late.Details.Single(d => d.ComponentCode == "FSC").Amount);
        var again = await GetAsync(tenantId, early.Id);
        Assert.Equal(early.TotalAmount, again.TotalAmount);
    }

    [Fact]
    public async Task Dedup_PicksTheMoreSpecificRule_AndAmbiguityStoresNothing()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-DEDUP");
        var (_, versionId) = await PublishedCardAsync(tenantId, "RC-DEDUP", "vendor", "VND", 1_000m);
        var specific = await CreateSurchargeAsync(tenantId, new
        {
            code = "FSC",
            name = "Fuel",
            direction = "buy",
            calculationMode = "unit_rate",
            currencyCode = "VND",
            rateAmountPercent = 2_000m,
            transportMode = "air",
            publish = false
        });
        await PostIdAsync(tenantId, $"/api/surcharges/versions/{specific.VersionId}/rules", new
        {
            code = "FSC",
            name = "Fuel",
            direction = "buy",
            calculationMode = "unit_rate",
            currencyCode = "VND",
            rateAmountPercent = 9_000m,
            transportMode = "air",
            routeCode = "SGN-HAN"
        });
        using var publish = Tenant(HttpMethod.Post, $"/api/surcharges/{specific.SurchargeId}/versions/{specific.VersionId}/publish", tenantId);
        (await _client.SendAsync(publish)).EnsureSuccessStatusCode();

        var rated = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 100m,
            transportMode = "air",
            routeCode = "SGN-HAN"
        }));
        var fsc = rated.Details.Where(d => d.ComponentCode == "FSC").ToList();
        Assert.Single(fsc);
        Assert.Equal(900_000m, fsc[0].Amount);

        var ambiguous = await CreateSurchargeAsync(tenantId, new
        {
            code = "SSC",
            name = "Security",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 10_000m,
            transportMode = "air",
            publish = false
        });
        await PostIdAsync(tenantId, $"/api/surcharges/versions/{ambiguous.VersionId}/rules", new
        {
            code = "SSC",
            name = "Security",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 20_000m,
            transportMode = "air"
        });
        using var publishAmbiguous = Tenant(HttpMethod.Post, $"/api/surcharges/{ambiguous.SurchargeId}/versions/{ambiguous.VersionId}/publish", tenantId);
        (await _client.SendAsync(publishAmbiguous)).EnsureSuccessStatusCode();
        using var rate = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        rate.Content = JsonContent.Create(new { billId, rateVersionId = versionId, quantity = 100m, transportMode = "air", routeCode = "SGN-HAN" });
        var blocked = await _client.SendAsync(rate);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadAsStringAsync();
        Assert.Contains("AMBIGUOUS_SURCHARGE", err);
    }

    [Fact]
    public async Task PercentageContainerAndFx_StoreTheLineSnapshot()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-FX");
        var (_, versionId) = await PublishedCardAsync(tenantId, "RC-FX", "vendor", "VND", 10_000m);
        await CreateSurchargeAsync(tenantId, new
        {
            code = "FUEL",
            name = "Fuel percent",
            direction = "buy",
            calculationMode = "percentage",
            currencyCode = "VND",
            rateAmountPercent = 10m,
            publish = true
        });
        await CreateSurchargeAsync(tenantId, new
        {
            code = "THC",
            name = "Terminal",
            direction = "buy",
            calculationMode = "container_rate",
            currencyCode = "VND",
            rateAmountPercent = 1_500_000m,
            containerType = "40HC",
            publish = true
        });
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        using (var fx = Tenant(HttpMethod.Post, "/api/fx-rates", tenantId))
        {
            fx.Content = JsonContent.Create(new
            {
                fromCurrencyCode = "USD",
                toCurrencyCode = "VND",
                rateDate = asOf.ToString("yyyy-MM-dd"),
                rate = 26_000m,
                source = "manual",
                version = 1
            });
            (await _client.SendAsync(fx)).EnsureSuccessStatusCode();
        }

        await CreateSurchargeAsync(tenantId, new
        {
            code = "DOC",
            name = "Docs",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "USD",
            rateAmountPercent = 10m,
            publish = true
        });

        var percentOnly = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 100m,
            targetCurrency = "VND"
        }));
        Assert.Equal(100_000m, percentOnly.Details.Single(d => d.ComponentCode == "FUEL").Amount);

        var full = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 100m,
            targetCurrency = "VND",
            containers = new[] { new { containerType = "40HC", quantity = 2 } }
        }));
        Assert.Equal(3_000_000m, full.Details.Single(d => d.ComponentCode == "THC").Amount);
        var doc = full.Details.Single(d => d.ComponentCode == "DOC");
        Assert.Equal(10m, doc.AmountOriginal);
        Assert.Equal("USD", doc.OriginalCurrency);
        Assert.Equal(260_000m, doc.ReportingAmount);
        Assert.Equal(26_000m, doc.LineFxRate);
        Assert.False(string.IsNullOrWhiteSpace(doc.LineFxSource));
    }

    [Fact]
    public async Task Rerate_LeavesThePreviousRatingUntouched()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-RE");
        var (_, versionId) = await PublishedCardAsync(tenantId, "RC-RE", "vendor", "VND", 1_000m);
        var created = await CreateSurchargeAsync(tenantId, new
        {
            code = "FSC",
            name = "Fuel",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 50_000m,
            validFrom = "2020-01-01T00:00:00Z",
            validTo = "2026-01-01T00:00:00Z",
            publish = true
        });
        var firstId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 1m,
            rateDate = "2025-06-01T00:00:00Z"
        });
        var first = await GetAsync(tenantId, firstId);
        var draftId = await PostIdAsync(tenantId, $"/api/surcharges/{created.SurchargeId}/versions", new { });
        using var update = Tenant(HttpMethod.Put, $"/api/surcharges/{created.SurchargeId}/versions/{draftId}", tenantId);
        update.Content = JsonContent.Create(new
        {
            code = "FSC",
            name = "Fuel",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 80_000m,
            validFrom = "2026-02-01T00:00:00Z"
        });
        (await _client.SendAsync(update)).EnsureSuccessStatusCode();
        using var publish = Tenant(HttpMethod.Post, $"/api/surcharges/{created.SurchargeId}/versions/{draftId}/publish", tenantId);
        (await _client.SendAsync(publish)).EnsureSuccessStatusCode();

        var secondId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 1m,
            rateDate = "2026-06-01T00:00:00Z",
            supersedesRatingId = firstId
        });
        var old = await GetAsync(tenantId, firstId);
        var newer = await GetAsync(tenantId, secondId);
        Assert.Equal(first.TotalAmount, old.TotalAmount);
        Assert.Equal("superseded", old.Status);
        Assert.Equal(80_000m, newer.Details.Single(d => d.ComponentCode == "FSC").Amount);
        Assert.NotEqual(firstId, secondId);
    }

    [Fact]
    public async Task RateVersion_RejectsSurcharge_RatesBaseOnly_AndMigrationSkipsBase()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-MIG");
        var cardId = await CreateCardAsync(tenantId, "RC-MIG", "vendor", "VND");
        var draftVersion = await CreateVersionAsync(tenantId, cardId);
        var draftRule = await AddRuleAsync(tenantId, draftVersion, "BASE", "Base freight", "fixed", 100m);
        await AddComponentAsync(tenantId, draftRule, "BASE_FREIGHT", "Base freight", 100m);
        await AddComponentAsync(tenantId, draftRule, "MISC", "Other", 5m);

        using var fsc = Tenant(HttpMethod.Post, $"/api/pricing-rules/{draftRule}/components", tenantId);
        fsc.Content = JsonContent.Create(new
        {
            code = "FSC",
            name = "Fuel",
            financialNature = "cost",
            amount = 20m,
            currencyCode = "VND"
        });
        var denied = await _client.SendAsync(fsc);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);

        using var remote = Tenant(HttpMethod.Post, $"/api/rate-versions/{draftVersion}/rules", tenantId);
        remote.Content = JsonContent.Create(new
        {
            code = "REMOTE-SBH",
            name = "Phụ phí vùng SBH",
            calcMethod = "unit_rate",
            unitAmount = 10m,
            currencyCode = "VND",
            chargeCode = "REMOTE"
        });
        var deniedRule = await _client.SendAsync(remote);
        Assert.Equal(HttpStatusCode.Conflict, deniedRule.StatusCode);

        var publishedVersion = await CreateVersionAsync(tenantId, cardId, "2020-01-01T00:00:00Z");
        await AddRuleAsync(tenantId, publishedVersion, "FREIGHT", "Base freight", "fixed", 100m);
        using (var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{publishedVersion}/publish", tenantId))
        {
            (await _client.SendAsync(publish)).EnsureSuccessStatusCode();
        }

        var ratingId = await RateAsync(tenantId, new { billId, rateVersionId = publishedVersion, quantity = 1m });
        var before = await GetAsync(tenantId, ratingId);
        Assert.Equal(100m, before.TotalAmount);
        Assert.DoesNotContain(before.Details, d => d.SourceType == "surcharge");

        var first = await PostAsync(tenantId, "/api/surcharges/migrate-legacy", new { });
        var migrated = await first.Content.ReadFromJsonAsync<MigrateBody>(Json);
        Assert.Equal(0, migrated!.Migrated);
        Assert.True(migrated.SkippedBase >= 1);
        Assert.True(migrated.SkippedUnknown >= 1);

        var second = await PostAsync(tenantId, "/api/surcharges/migrate-legacy", new { });
        var again = await second.Content.ReadFromJsonAsync<MigrateBody>(Json);
        Assert.Equal(0, again!.Migrated);
        Assert.True(again.AlreadyMapped >= 2);

        var after = await GetAsync(tenantId, ratingId);
        Assert.Equal(before.TotalAmount, after.TotalAmount);
    }

    private async Task<(Guid CardId, Guid VersionId)> PublishedCardAsync(
        Guid tenantId, string code, string partyType, string currency, decimal unitAmount)
    {
        var cardId = await CreateCardAsync(tenantId, code, partyType, currency);
        var versionId = await CreateVersionAsync(tenantId, cardId, "2020-01-01T00:00:00Z");
        await AddRuleAsync(tenantId, versionId, "BASE", "Cước chính", "unit_rate", unitAmount, currency);
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

        var created = await PostIdAsync(tenantId, "/api/economic-charge-types", new { code = "DONGGO", name = "Đóng gói" });
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

    private async Task<Guid> AddRuleAsync(
        Guid tenantId, Guid versionId, string code, string name, string calcMethod, decimal amount, string currency = "VND")
    {
        using var req = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId);
        req.Content = JsonContent.Create(new { code, name, calcMethod, unitAmount = amount, currencyCode = currency, sortOrder = 1 });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task AddComponentAsync(Guid tenantId, Guid ruleId, string code, string name, decimal amount)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/components", tenantId);
        req.Content = JsonContent.Create(new { code, name, financialNature = "cost", amount, currencyCode = "VND" });
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateVersionAsync(Guid tenantId, Guid cardId, string? effectiveFrom = null)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        req.Content = JsonContent.Create(new { note = "v", effectiveFrom });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateCardAsync(Guid tenantId, string code, string partyType, string currency)
    {
        using var req = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        req.Content = JsonContent.Create(new { code, name = code, partyType, currencyCode = currency });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        var res = await PostAsync(tenantId, "/api/ratings", body);
        if (!res.IsSuccessStatusCode)
        {
            var text = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{(int)res.StatusCode} {text}");
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

    private async Task<string> GetJsonAsync(Guid tenantId, string url)
    {
        using var req = Tenant(HttpMethod.Get, url, tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadAsStringAsync();
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
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Surcharge" });
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
    private sealed record MigrateBody(int Migrated, int SkippedBase, int SkippedUnknown, int SkippedPublished, int AlreadyMapped);
    private sealed record ListRow(Guid Id, string Code, string? SourceKind);
    private sealed record DetailBody(Guid? SourceLegacyComponentId);
    private sealed record RatingBody(Guid Id, decimal TotalAmount, string? Status, List<DetailBodyLine> Details);
    private sealed record DetailBodyLine(
        string ComponentCode,
        string? SourceType,
        Guid? SourceVersionId,
        string? FinancialNature,
        string? FinancialMaturity,
        decimal Amount,
        decimal? AmountOriginal,
        string? OriginalCurrency,
        decimal? ReportingAmount,
        decimal? LineFxRate,
        string? LineFxSource);
}
