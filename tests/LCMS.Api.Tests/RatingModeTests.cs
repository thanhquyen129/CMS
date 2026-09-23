using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.Demo;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class RatingModeTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public RatingModeTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task WeightBreakPivot_UsesBandThenMinCharge_AndSnapshotStays()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-PIVOT");
        var versionId = await PublishedVersionAsync(tenantId, "RC-PIVOT");
        var ruleId = await AddRuleAsync(tenantId, versionId, new
        {
            code = "AIR",
            name = "Cước air",
            calcMethod = "weight_break_pivot",
            unitAmount = 0,
            currencyCode = "USD",
            chargeCode = "FREIGHT",
            minAmount = 100,
            sortOrder = 1
        });
        await AddBreakAsync(tenantId, ruleId, 1, 0, 45, 4m);
        await AddBreakAsync(tenantId, ruleId, 2, 45.01m, 100, 2.5m);
        await PublishAsync(tenantId, versionId);

        var ratingId = await RateAsync(tenantId, new { billId, rateVersionId = versionId, quantity = 100, transportMode = "air" });
        var rating = await GetAsync(tenantId, ratingId);
        Assert.Equal(250m, rating.TotalAmount);
        Assert.Contains("100", rating.Details[0].FormulaText);
        Assert.False(string.IsNullOrWhiteSpace(rating.ContextJson));

        using var edit = Tenant(HttpMethod.Patch, $"/api/bills/{billId}/context", tenantId);
        edit.Content = JsonContent.Create(new { originCode = "HAN", destinationCode = "SGN" });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(edit)).StatusCode);
        var again = await GetAsync(tenantId, ratingId);
        Assert.Equal(rating.ContextJson, again.ContextJson);
        Assert.Equal(250m, again.TotalAmount);
    }

    [Fact]
    public async Task SameSpecificity_IsAmbiguous_AndMoreSpecificRuleWins()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-AMB");
        var versionId = await PublishedVersionAsync(tenantId, "RC-AMB");
        await AddRuleAsync(tenantId, versionId, new
        {
            code = "A",
            name = "A",
            calcMethod = "fixed",
            unitAmount = 10,
            currencyCode = "VND",
            chargeCode = "FREIGHT",
            sortOrder = 1
        });
        await AddRuleAsync(tenantId, versionId, new
        {
            code = "B",
            name = "B",
            calcMethod = "fixed",
            unitAmount = 20,
            currencyCode = "VND",
            chargeCode = "FREIGHT",
            sortOrder = 2
        });
        await PublishAsync(tenantId, versionId);

        using var ambiguous = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        ambiguous.Content = JsonContent.Create(new { billId, rateVersionId = versionId, quantity = 1 });
        var blocked = await _client.SendAsync(ambiguous);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<Err>(Json);
        Assert.Contains("AMBIGUOUS_RATE_RULE", err!.Message);

        var version2 = await PublishedVersionAsync(tenantId, "RC-SPEC");
        await AddRuleAsync(tenantId, version2, new
        {
            code = "ANY",
            name = "Mọi tuyến",
            calcMethod = "fixed",
            unitAmount = 10,
            currencyCode = "VND",
            chargeCode = "FREIGHT",
            sortOrder = 1
        });
        await AddRuleAsync(tenantId, version2, new
        {
            code = "SGN",
            name = "Tuyến SGN",
            calcMethod = "fixed",
            unitAmount = 30,
            currencyCode = "VND",
            chargeCode = "FREIGHT",
            routeCode = "SGN-FRA",
            sortOrder = 2
        });
        await PublishAsync(tenantId, version2);
        var ratingId = await RateAsync(tenantId, new { billId, rateVersionId = version2, quantity = 1, routeCode = "SGN-FRA" });
        var rating = await GetAsync(tenantId, ratingId);
        Assert.Equal(30m, rating.TotalAmount);
        Assert.Equal("SGN", rating.Details[0].RuleCode);
    }

    [Fact]
    public async Task ExpiredVersion_AndDuplicateBreak_AreRejected()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-EXP");
        var cardId = await CreateCardAsync(tenantId, "RC-EXP");
        using var versionReq = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        versionReq.Content = JsonContent.Create(new
        {
            effectiveFrom = DateTimeOffset.UtcNow.AddDays(-10),
            effectiveTo = DateTimeOffset.UtcNow.AddDays(-1),
            note = "hết hạn"
        });
        var versionRes = await _client.SendAsync(versionReq);
        versionRes.EnsureSuccessStatusCode();
        var versionId = (await versionRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        var ruleId = await AddRuleAsync(tenantId, versionId, new
        {
            code = "OLD",
            name = "Cũ",
            calcMethod = "weight_step",
            unitAmount = 5,
            currencyCode = "VND",
            roundingStep = 0.5m,
            sortOrder = 1
        });
        using var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(publish)).StatusCode);

        using var rate = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        rate.Content = JsonContent.Create(new { billId, rateVersionId = versionId, quantity = 1 });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(rate)).StatusCode);

        var draftCard = await CreateCardAsync(tenantId, "RC-BRK");
        using var draftVersionReq = Tenant(HttpMethod.Post, $"/api/rate-cards/{draftCard}/versions", tenantId);
        draftVersionReq.Content = JsonContent.Create(new { note = "draft" });
        var draftVersionRes = await _client.SendAsync(draftVersionReq);
        draftVersionRes.EnsureSuccessStatusCode();
        var draftVersionId = (await draftVersionRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        var draftRule = await AddRuleAsync(tenantId, draftVersionId, new
        {
            code = "STEP",
            name = "Bậc",
            calcMethod = "weight_step",
            unitAmount = 5,
            currencyCode = "VND",
            sortOrder = 1
        });
        await AddBreakAsync(tenantId, draftRule, 1, 0, 10, 5);
        using var dup = Tenant(HttpMethod.Post, $"/api/pricing-rules/{draftRule}/breaks", tenantId);
        dup.Content = JsonContent.Create(new { sequenceNo = 2, minQuantity = 5, maxQuantity = 12, unitAmount = 4 });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(dup)).StatusCode);
    }

    [Fact]
    public async Task AirChargeable_UsesVolumeFactor_ConfirmedOverrideNeedsReason()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-CW");
        using var ctx = Tenant(HttpMethod.Patch, $"/api/bills/{billId}/context", tenantId);
        ctx.Content = JsonContent.Create(new
        {
            context = new { grossWeightKg = 85m, volumeCbm = 0.68m, chargeableWeightKg = 120m, chargeableConfirmed = true }
        });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(ctx)).StatusCode);

        var versionId = await PublishedVersionAsync(tenantId, "RC-CW");
        await AddRuleAsync(tenantId, versionId, new
        {
            code = "KG",
            name = "Theo kg",
            calcMethod = "unit_rate",
            unitAmount = 2,
            currencyCode = "USD",
            volumetricFactor = 167,
            sortOrder = 1
        });
        await PublishAsync(tenantId, versionId);

        using var blocked = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        blocked.Content = JsonContent.Create(new { billId, rateVersionId = versionId, quantity = 100 });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(blocked)).StatusCode);

        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 100,
            chargeableOverrideReason = "Khách chốt 100 kg"
        });
        var rating = await GetAsync(tenantId, ratingId);
        Assert.Equal("override", rating.ChargeableBasis);
        Assert.Equal(200m, rating.TotalAmount);
    }

    [Fact]
    public async Task Compare_DoesNotPersist_AndImportRejectsOverlappingBreaks()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-CMP");
        var cheap = await PublishedVersionAsync(tenantId, "RC-CHEAP");
        await AddRuleAsync(tenantId, cheap, new
        {
            code = "BASE",
            name = "Rẻ",
            calcMethod = "fixed",
            unitAmount = 10,
            currencyCode = "USD",
            sortOrder = 1
        });
        await PublishAsync(tenantId, cheap);
        var dear = await PublishedVersionAsync(tenantId, "RC-DEAR");
        await AddRuleAsync(tenantId, dear, new
        {
            code = "BASE",
            name = "Đắt",
            calcMethod = "fixed",
            unitAmount = 40,
            currencyCode = "USD",
            sortOrder = 1
        });
        await PublishAsync(tenantId, dear);

        using var compare = Tenant(HttpMethod.Post, "/api/ratings/compare", tenantId);
        compare.Content = JsonContent.Create(new { billId, quantity = 1, partyType = "vendor" });
        var compared = await _client.SendAsync(compare);
        compared.EnsureSuccessStatusCode();
        var quotes = await compared.Content.ReadFromJsonAsync<List<QuoteBody>>(Json);
        Assert.NotNull(quotes);
        Assert.Contains(quotes, q => q.TotalAmount == 10m);
        Assert.Contains(quotes, q => q.TotalAmount == 40m);

        using var history = Tenant(HttpMethod.Get, $"/api/bills/{billId}/ratings", tenantId);
        var listed = await _client.SendAsync(history);
        listed.EnsureSuccessStatusCode();
        var ratings = await listed.Content.ReadFromJsonAsync<List<IdBody>>(Json);
        Assert.Empty(ratings!);

        using var preview = Tenant(HttpMethod.Post, "/api/rate-imports/preview", tenantId);
        preview.Content = JsonContent.Create(new
        {
            cards = new[]
            {
                new
                {
                    code = "RC-IMP",
                    name = "Nhập",
                    partyType = "vendor",
                    currencyCode = "USD",
                    rules = new[]
                    {
                        new
                        {
                            code = "AIR",
                            name = "Air",
                            calcMethod = "weight_break_pivot",
                            unitAmount = 0,
                            breaks = new object[]
                            {
                                new { sequenceNo = 1, minQuantity = 0, maxQuantity = 100, unitAmount = 2 },
                                new { sequenceNo = 2, minQuantity = 50, maxQuantity = 200, unitAmount = 1.5 }
                            }
                        }
                    }
                }
            }
        });
        var previewRes = await _client.SendAsync(preview);
        previewRes.EnsureSuccessStatusCode();
        var body = await previewRes.Content.ReadFromJsonAsync<PreviewBody>(Json);
        Assert.False(body!.CanCommit);
        Assert.Contains("chồng", body.Issues[0].Message);
    }

    [Fact]
    public async Task ContainerRate_SumsTypes_AndCompositeCycleIsRejected()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-BOX");
        var versionId = await PublishedVersionAsync(tenantId, "RC-BOX");
        var ruleId = await AddRuleAsync(tenantId, versionId, new
        {
            code = "FCL",
            name = "Container",
            calcMethod = "container_rate",
            unitAmount = 0,
            currencyCode = "USD",
            sortOrder = 1
        });
        using var box = Tenant(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/container-rates", tenantId);
        box.Content = JsonContent.Create(new { containerType = "20GP", unitAmount = 100 });
        (await _client.SendAsync(box)).EnsureSuccessStatusCode();
        using var box2 = Tenant(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/container-rates", tenantId);
        box2.Content = JsonContent.Create(new { containerType = "40HC", unitAmount = 200 });
        (await _client.SendAsync(box2)).EnsureSuccessStatusCode();
        await PublishAsync(tenantId, versionId);
        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            containers = new[]
            {
                new { containerType = "20GP", quantity = 2 },
                new { containerType = "40HC", quantity = 1 }
            }
        });
        var rating = await GetAsync(tenantId, ratingId);
        Assert.Equal(400m, rating.TotalAmount);

        var cycleVersion = await PublishedVersionAsync(tenantId, "RC-CYC");
        var cycleRule = await AddRuleAsync(tenantId, cycleVersion, new
        {
            code = "MIX",
            name = "Vòng",
            calcMethod = "composite",
            unitAmount = 0,
            currencyCode = "USD",
            sortOrder = 1
        });
        using var a = Tenant(HttpMethod.Post, $"/api/pricing-rules/{cycleRule}/components", tenantId);
        a.Content = JsonContent.Create(new { code = "A", name = "A", financialNature = "cost", amount = 1, currencyCode = "USD", dependsOnCode = "B" });
        (await _client.SendAsync(a)).EnsureSuccessStatusCode();
        using var b = Tenant(HttpMethod.Post, $"/api/pricing-rules/{cycleRule}/components", tenantId);
        b.Content = JsonContent.Create(new { code = "B", name = "B", financialNature = "cost", amount = 1, currencyCode = "USD", dependsOnCode = "A" });
        (await _client.SendAsync(b)).EnsureSuccessStatusCode();
        await PublishAsync(tenantId, cycleVersion);
        using var rate = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        rate.Content = JsonContent.Create(new { billId, rateVersionId = cycleVersion, quantity = 1 });
        var blocked = await _client.SendAsync(rate);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<Err>(Json);
        Assert.Contains("COMPOSITE", err!.Message);
    }

    [Fact]
    public async Task ReferenceTariff_RatesBillByBandCommodityAndRemoteFee()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-NSE");
        var versions = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in ReferenceTariffCatalog.Cards)
        {
            versions[card.Code] = await PublishReferenceCardAsync(tenantId, card);
        }

        var air = versions[ReferenceTariffCatalog.AirCode];
        var sea = versions[ReferenceTariffCatalog.SeaCode];

        var general = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = air,
            quantity = 10.5m,
            commodityCode = ReferenceTariffCatalog.CommodityGeneral
        }));
        Assert.Equal(682_500m, general.TotalAmount);

        var light = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = air,
            quantity = 1.5m,
            commodityCode = ReferenceTariffCatalog.CommodityGeneral
        }));
        Assert.Equal(200_000m, light.TotalAmount);

        var remote = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = air,
            quantity = 10m,
            commodityCode = ReferenceTariffCatalog.CommodityGeneral,
            destinationCode = "SBH"
        }));
        Assert.Equal(1_500_000m, remote.TotalAmount);

        using var express = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        express.Content = JsonContent.Create(new
        {
            billId,
            rateVersionId = air,
            quantity = 5m,
            commodityCode = ReferenceTariffCatalog.CommodityExpress
        });
        var blocked = await _client.SendAsync(express);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<Err>(Json);
        Assert.Contains("NO_APPLICABLE_RATE", err!.Message);

        var seaMin = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = sea,
            quantity = 0.5m,
            commodityCode = ReferenceTariffCatalog.CommodityGeneral,
            transportMode = "sea"
        }));
        Assert.Equal(170m, seaMin.TotalAmount);

        var seaBand = await GetAsync(tenantId, await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = sea,
            quantity = 8m,
            commodityCode = ReferenceTariffCatalog.CommodityCosmeticsFood,
            transportMode = "sea"
        }));
        Assert.Equal(1_320m, seaBand.TotalAmount);

        using var seaRemote = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        seaRemote.Content = JsonContent.Create(new
        {
            billId,
            rateVersionId = sea,
            quantity = 2m,
            commodityCode = ReferenceTariffCatalog.CommodityGeneral,
            destinationCode = "SWK",
            transportMode = "sea"
        });
        var seaBlocked = await _client.SendAsync(seaRemote);
        Assert.Equal(HttpStatusCode.Conflict, seaBlocked.StatusCode);
        var seaErr = await seaBlocked.Content.ReadFromJsonAsync<Err>(Json);
        Assert.Contains("trọng lượng", seaErr!.Message);
    }

    private async Task<Guid> PublishReferenceCardAsync(Guid tenantId, ReferenceTariffCatalog.ReferenceRateCard card)
    {
        using var cardReq = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        cardReq.Content = JsonContent.Create(new
        {
            code = card.Code,
            name = card.Name,
            partyType = "vendor",
            currencyCode = card.CurrencyCode,
            description = card.Description,
            transportMode = card.TransportMode,
            routeCode = ReferenceTariffCatalog.Route,
            carrierName = ReferenceTariffCatalog.Carrier
        });
        var cardRes = await _client.SendAsync(cardReq);
        cardRes.EnsureSuccessStatusCode();
        var cardId = (await cardRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        using var versionReq = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        versionReq.Content = JsonContent.Create(new { effectiveFrom = card.EffectiveFrom, note = card.Note });
        var versionRes = await _client.SendAsync(versionReq);
        versionRes.EnsureSuccessStatusCode();
        var versionId = (await versionRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        foreach (var rule in card.Rules)
        {
            var ruleId = await AddRuleAsync(tenantId, versionId, new
            {
                code = rule.Code,
                name = rule.Name,
                calcMethod = rule.CalcMethod,
                unitAmount = rule.UnitAmount,
                currencyCode = rule.CurrencyCode,
                chargeCode = rule.ChargeCode,
                commodityCode = rule.CommodityCode,
                destinationCode = rule.DestinationCode,
                applicability = rule.Applicability,
                minAmount = rule.MinAmount,
                volumetricFactor = rule.VolumetricFactor,
                sortOrder = rule.SortOrder
            });
            foreach (var band in rule.Breaks)
            {
                await AddBreakAsync(tenantId, ruleId, band.SequenceNo, band.MinQuantity, band.MaxQuantity, band.UnitAmount);
            }
        }

        await PublishAsync(tenantId, versionId);
        return versionId;
    }

    private async Task<Guid> PublishedVersionAsync(Guid tenantId, string code)
    {
        var cardId = await CreateCardAsync(tenantId, code);
        using var versionReq = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        versionReq.Content = JsonContent.Create(new { note = code });
        var versionRes = await _client.SendAsync(versionReq);
        versionRes.EnsureSuccessStatusCode();
        var versionId = (await versionRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        return versionId;
    }

    private async Task<Guid> AddRuleAsync(Guid tenantId, Guid versionId, object body)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task PublishAsync(Guid tenantId, Guid versionId)
    {
        using var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        var res = await _client.SendAsync(publish);
        res.EnsureSuccessStatusCode();
    }

    private async Task AddBreakAsync(Guid tenantId, Guid ruleId, int sequence, decimal min, decimal? max, decimal amount)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/pricing-rules/{ruleId}/breaks", tenantId);
        req.Content = JsonContent.Create(new { sequenceNo = sequence, minQuantity = min, maxQuantity = max, unitAmount = amount });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        using var req = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        req.Content = JsonContent.Create(body);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<RatingBody> GetAsync(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/ratings/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<RatingBody>(Json))!;
    }

    private async Task<Guid> CreateCardAsync(Guid tenantId, string code)
    {
        using var req = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        req.Content = JsonContent.Create(new { code, name = code, partyType = "vendor", currencyCode = "USD" });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
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
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Rate" });
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
    private sealed record Err(string Message);
    private sealed record RatingBody(decimal TotalAmount, string? ContextJson, string? ChargeableBasis, List<DetailBody> Details);
    private sealed record DetailBody(string RuleCode, string? FormulaText);
    private sealed record QuoteBody(decimal TotalAmount);
    private sealed record PreviewBody(bool CanCommit, List<IssueBody> Issues);
    private sealed record IssueBody(string Message);
}
