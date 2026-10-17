using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.EconomicCharges;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

/// <summary>UAT-01..20 of LCMS_UAT_RATE_UI_05_07 v1.0 (rule direction, weight condition, hidden charge group).</summary>
[Collection("Api")]
public sealed class RateUi0507UatTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public RateUi0507UatTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Uat01_SellHeader_TwoRulesInheritSell_AndRateAsRevenue()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, Card("RC-U01", "customer", null, new[]
        {
            Rule("A", t.Freight, 10m, sell: true, routeId: t.Route),
            Rule("B", t.Pickup, 5m, sell: true, routeId: t.Route)
        }));
        var rules = await RulesAsync(t.Id, created.RateVersionId);
        Assert.Equal(2, rules.Count);
        Assert.All(rules, rule => Assert.Null(rule.PartyTypeCode));
        await PublishOkAsync(t.Id, created.RateVersionId);

        var billId = await BillAsync(t.Id, "BL-U01", "SGN-HAN");
        var rating = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = created.RateVersionId, quantity = 1m }));
        Assert.Equal("customer", rating.PartyTypeCode);
        Assert.Equal(15m, rating.TotalAmount);
        Assert.All(rating.Details, d => Assert.Equal("revenue", d.FinancialNature));
    }

    [Fact]
    public async Task Uat02_BuyHeader_RuleSendingCustomer_IsRejectedOnEveryWritePath()
    {
        var t = await TenantAsync();
        var rule = Rule("A", t.Freight, 10m, routeId: t.Route);
        rule["partyTypeCode"] = "customer";
        var res = await PostAsync(t.Id, "/api/rate-cards", Card("RC-U02", "vendor", t.Supplier, new[] { rule }));
        var problem = await ProblemAsync(res);
        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("party_type_conflict", problem.Code);
        Assert.Contains("trái với Loại giá ở Thông tin chung", problem.Message);
        Assert.Contains("Giá mua", problem.Message);

        var same = Rule("A", t.Freight, 10m, routeId: t.Route);
        same["partyTypeCode"] = "vendor";
        var ok = await DeclareAsync(t.Id, Card("RC-U02-OK", "vendor", t.Supplier, new[] { same }));
        Assert.Null((await RulesAsync(t.Id, ok.RateVersionId)).Single().PartyTypeCode);

        var cardId = await LegacyCardAsync(t.Id, "RC-U02-L", "vendor");
        var versionId = await LegacyVersionAsync(t.Id, cardId);
        var legacy = await PostAsync(t.Id, $"/api/rate-versions/{versionId}/rules", new
        {
            code = "X", name = "Cước", calcMethod = "fixed", unitAmount = 1m, currencyCode = "USD", partyTypeCode = "customer", sortOrder = 1
        });
        Assert.Equal((HttpStatusCode)422, legacy.StatusCode);

        await PostOkAsync(t.Id, $"/api/rate-versions/{versionId}/rules", new
        {
            code = "SAME", name = "Cước cùng chiều", calcMethod = "fixed", unitAmount = 1m, currencyCode = "USD", partyTypeCode = "vendor", economicChargeTypeId = t.Freight, sortOrder = 1
        });
        var addedSame = (await RulesAsync(t.Id, versionId)).Single(r => r.Code == "SAME");
        Assert.Null(addedSame.PartyTypeCode);
        Assert.Equal("FREIGHT", addedSame.ChargeCode);

        var preview = await PostAsync(t.Id, "/api/rate-imports/preview", new
        {
            cards = new[]
            {
                new
                {
                    code = "RC-U02-IMP", name = "Nhập", partyType = "vendor", currencyCode = "USD",
                    rules = new[] { new { code = "R1", name = "Cước", calcMethod = "fixed", unitAmount = 1m, chargeCode = "FREIGHT", partyTypeCode = "customer" } }
                }
            }
        });
        var body = await preview.Content.ReadFromJsonAsync<PreviewBody>(Json);
        Assert.False(body!.CanCommit);
        Assert.Contains(body.Issues, issue => issue.Field == "partyTypeCode");
    }

    [Fact]
    public async Task Uat03_BuyLegRating_IsCost_EvenWhenTheRequestAsksForSell()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, Card("RC-U03", "vendor", t.Supplier, new[] { Rule("LEG", t.Freight, 10m, anyRoute: true, unitRate: true) }));
        await PublishOkAsync(t.Id, created.RateVersionId);
        var billId = await BillAsync(t.Id, "BL-U03");
        var legId = await LegAsync(t.Id, billId, "U03");
        var rating = await RatingAsync(t.Id, await RateOkAsync(t.Id, new
        {
            billId, rateVersionId = created.RateVersionId, quantity = 4m, contextType = "leg", transportLegId = legId,
            vendorPartyId = t.Supplier, partyTypeCode = "customer"
        }));
        Assert.Equal(40m, rating.TotalAmount);
        Assert.Equal("vendor", rating.PartyTypeCode);
        Assert.All(rating.Details, d => Assert.Equal("cost", d.FinancialNature));

        var other = await PartyAsync(t.Id, "NCC-U03-B", "vendor");
        var wrong = await PostAsync(t.Id, "/api/ratings", new
        {
            billId, rateVersionId = created.RateVersionId, quantity = 4m, contextType = "leg", transportLegId = legId, vendorPartyId = other
        });
        Assert.Equal(HttpStatusCode.Conflict, wrong.StatusCode);
    }

    [Fact]
    public async Task Uat04_PublishedLegacyRuleWithPartyType_ReadsAndRatesWithoutRewrite()
    {
        var t = await TenantAsync();
        var cardId = await LegacyCardAsync(t.Id, "RC-U04", "vendor");
        var versionId = await LegacyVersionAsync(t.Id, cardId);
        await PostOkAsync(t.Id, $"/api/rate-versions/{versionId}/rules", new
        {
            code = "OLD", name = "Cước cũ", calcMethod = "fixed", unitAmount = 70m, currencyCode = "USD", sortOrder = 1
        });
        await MutateRuleAsync(versionId, r => r.PartyTypeCode = "vendor");
        await PublishOkAsync(t.Id, versionId);
        var before = await RowAsync(versionId, "OLD");

        var rules = await RulesAsync(t.Id, versionId);
        Assert.Equal("vendor", Assert.Single(rules).PartyTypeCode);
        var billId = await BillAsync(t.Id, "BL-U04");
        var rating = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = versionId, quantity = 1m }));
        Assert.Equal(70m, rating.TotalAmount);
        Assert.Equal("cost", rating.Details.Single().FinancialNature);
        var after = await RowAsync(versionId, "OLD");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Uat05_AdvancedSell_HasNoRuleLevelPriceDirection()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, new Dictionary<string, object?>
        {
            ["code"] = "RC-U05",
            ["name"] = "Nâng cao bán",
            ["partyType"] = "customer",
            ["currencyCode"] = "USD",
            ["declarationMode"] = "advanced",
            ["explicitChargeGroups"] = true,
            ["groups"] = new[] { Group("G1", t.Route, 2m, 3m, revenue: true, t.Freight) }
        });
        var rules = await RulesAsync(t.Id, created.RateVersionId);
        Assert.NotEmpty(rules);
        Assert.All(rules, rule =>
        {
            Assert.Null(rule.PartyTypeCode);
            Assert.Equal("FREIGHT", rule.ChargeCode);
        });
    }

    [Fact]
    public async Task Uat06_NoBandPayloadWithThreeNulls_IsSavedAsNoBand()
    {
        var t = await TenantAsync();
        var rule = Rule("A", t.Freight, 10m, routeId: t.Route);
        rule["weightBasis"] = null;
        rule["weightMin"] = null;
        rule["weightMax"] = null;
        var created = await DeclareAsync(t.Id, Card("RC-U06", "vendor", t.Supplier, new[] { rule }));
        var saved = (await RulesAsync(t.Id, created.RateVersionId)).Single();
        Assert.Null(saved.WeightBasis);
        Assert.Null(saved.WeightMin);
        Assert.Null(saved.WeightMax);
    }

    [Fact]
    public async Task Uat07_NoBasisWithBand_IsRejectedByTheApi()
    {
        var t = await TenantAsync();
        var res = await PostAsync(t.Id, "/api/rate-cards", Card("RC-U07", "vendor", t.Supplier, new[]
        {
            Rule("A", t.Freight, 10m, routeId: t.Route, basis: null, min: 0m, max: 5m)
        }));
        var problem = await ProblemAsync(res);
        Assert.Equal((HttpStatusCode)422, res.StatusCode);
        Assert.Equal("invalid_weight_band", problem.Code);
        Assert.Contains("Không lọc nấc", problem.Message);

        var cardId = await LegacyCardAsync(t.Id, "RC-U07-ADD", "vendor");
        var versionId = await LegacyVersionAsync(t.Id, cardId);
        var addRes = await PostAsync(t.Id, $"/api/rate-versions/{versionId}/rules", new
        {
            code = "R1", name = "Cước", calcMethod = "fixed", unitAmount = 10m, currencyCode = "USD",
            weightBasis = (string?)null, weightMin = 0m, weightMax = 5m, economicChargeTypeId = t.Freight, sortOrder = 1
        });
        Assert.Equal((HttpStatusCode)422, addRes.StatusCode);
        Assert.Equal("invalid_weight_band", (await ProblemAsync(addRes)).Code);

        var impPreview = await PostAsync(t.Id, "/api/rate-imports/preview", new
        {
            cards = new[]
            {
                new
                {
                    code = "RC-U07-IMP", name = "Nhập", partyType = "vendor", currencyCode = "USD",
                    rules = new[] { new { code = "R1", name = "Cước", calcMethod = "fixed", unitAmount = 10m, chargeCode = "FREIGHT", weightBasis = (string?)null, weightMin = (decimal?)0m, weightMax = (decimal?)5m } }
                }
            }
        });
        var previewBody = await impPreview.Content.ReadFromJsonAsync<PreviewBody>(Json);
        Assert.False(previewBody!.CanCommit);
        Assert.Contains(previewBody.Issues, i => i.Field == "weightBasis");
    }

    [Fact]
    public async Task Uat08_BasicGrossBand_IsHalfOpenAtTheUpperBound()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, Card("RC-U08", "vendor", t.Supplier, new[]
        {
            Rule("G05", t.Freight, 10m, anyRoute: true, basis: "gross", min: 0m, max: 5m)
        }));
        await PublishOkAsync(t.Id, created.RateVersionId);
        var billId = await BillAsync(t.Id, "BL-U08");
        var zero = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = created.RateVersionId, quantity = 1m, grossWeightKg = 0m }));
        Assert.Equal(10m, zero.TotalAmount);
        var hit = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = created.RateVersionId, quantity = 1m, grossWeightKg = 4.999m }));
        Assert.Equal(10m, hit.TotalAmount);
        var miss = await PostAsync(t.Id, "/api/ratings", new { billId, rateVersionId = created.RateVersionId, quantity = 1m, grossWeightKg = 5.000m });
        Assert.Equal(HttpStatusCode.Conflict, miss.StatusCode);
        Assert.Contains("nấc", (await ProblemAsync(miss)).Message);
    }

    [Fact]
    public async Task Uat09_ChargeableBand_UsesChargeableWeightNotGross()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, Card("RC-U09", "vendor", t.Supplier, new[]
        {
            Rule("C510", t.Freight, 77m, anyRoute: true, basis: "chargeable", min: 5m, max: 10m)
        }));
        await PublishOkAsync(t.Id, created.RateVersionId);
        var billId = await BillAsync(t.Id, "BL-U09");
        var hit = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = created.RateVersionId, quantity = 7m, grossWeightKg = 3m }));
        Assert.Equal(77m, hit.TotalAmount);
        var miss = await PostAsync(t.Id, "/api/ratings", new { billId, rateVersionId = created.RateVersionId, quantity = 12m, grossWeightKg = 7m });
        Assert.Equal(HttpStatusCode.Conflict, miss.StatusCode);
    }

    [Fact]
    public async Task Uat10_GrossWithBadOrMissingBounds_IsRejectedOnSaveAndPublish()
    {
        var t = await TenantAsync();
        var reversed = await PostAsync(t.Id, "/api/rate-cards", Card("RC-U10-A", "vendor", t.Supplier, new[]
        {
            Rule("A", t.Freight, 10m, routeId: t.Route, basis: "gross", min: 10m, max: 5m)
        }));
        Assert.Equal((HttpStatusCode)422, reversed.StatusCode);
        Assert.Contains("Mức đến phải lớn hơn mức từ", (await ProblemAsync(reversed)).Message);

        var open = await PostAsync(t.Id, "/api/rate-cards", Card("RC-U10-B", "vendor", t.Supplier, new[]
        {
            Rule("A", t.Freight, 10m, routeId: t.Route, basis: "gross", min: 0m, max: null)
        }));
        Assert.Equal((HttpStatusCode)422, open.StatusCode);
        Assert.Contains("nhập đủ Từ và Đến", (await ProblemAsync(open)).Message);

        var negative = await PostAsync(t.Id, "/api/rate-cards", Card("RC-U10-NEG", "vendor", t.Supplier, new[]
        {
            Rule("A", t.Freight, 10m, routeId: t.Route, basis: "gross", min: -0.001m, max: 5m)
        }));
        Assert.Equal((HttpStatusCode)422, negative.StatusCode);

        var cardId = await LegacyCardAsync(t.Id, "RC-U10-ADD", "vendor");
        var verId = await LegacyVersionAsync(t.Id, cardId);
        var addReversed = await PostAsync(t.Id, $"/api/rate-versions/{verId}/rules", new
        {
            code = "A", name = "Cước", calcMethod = "fixed", unitAmount = 10m, currencyCode = "USD",
            weightBasis = "gross", weightMin = 10m, weightMax = 5m, economicChargeTypeId = t.Freight, sortOrder = 1
        });
        Assert.Equal((HttpStatusCode)422, addReversed.StatusCode);

        var impReversed = await PostAsync(t.Id, "/api/rate-imports/preview", new
        {
            cards = new[]
            {
                new
                {
                    code = "RC-U10-IMP", name = "Nhập", partyType = "vendor", currencyCode = "USD",
                    rules = new[] { new { code = "R1", name = "Cước", calcMethod = "fixed", unitAmount = 10m, chargeCode = "FREIGHT", weightBasis = "gross", weightMin = (decimal?)10m, weightMax = (decimal?)5m } }
                }
            }
        });
        var impBody = await impReversed.Content.ReadFromJsonAsync<PreviewBody>(Json);
        Assert.False(impBody!.CanCommit);
        Assert.Contains(impBody.Issues, i => i.Field == "weightBasis");

        var created = await DeclareAsync(t.Id, Card("RC-U10-C", "vendor", t.Supplier, new[] { Rule("A", t.Freight, 10m, routeId: t.Route) }));
        await MutateRuleAsync(created.RateVersionId, rule =>
        {
            rule.WeightBasis = "gross";
            rule.WeightMin = 0m;
            rule.WeightMax = null;
        });
        var publish = await PostAsync(t.Id, $"/api/rate-versions/{created.RateVersionId}/publish", new { });
        Assert.Equal((HttpStatusCode)422, publish.StatusCode);
        Assert.Equal("draft", await StatusAsync(created.RateVersionId));
    }

    [Fact]
    public async Task Uat11_LegWithoutWeight_DoesNotBorrowTheBillWeight()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, Card("RC-U11", "vendor", t.Supplier, new[]
        {
            Rule("G", t.Freight, 50m, anyRoute: true, basis: "gross", min: 0m, max: 200m)
        }));
        await PublishOkAsync(t.Id, created.RateVersionId);
        var billId = await BillAsync(t.Id, "BL-U11");
        await PatchOkAsync(t.Id, $"/api/operational-references/bill/{billId}", new { changes = new { gross_weight_kg = "100" }, reason = "test" });
        var legId = await LegAsync(t.Id, billId, "U11");
        var res = await PostAsync(t.Id, "/api/ratings", new
        {
            billId, rateVersionId = created.RateVersionId, quantity = 4m, contextType = "leg", transportLegId = legId, vendorPartyId = t.Supplier, seedExpectedCosts = true
        });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Empty(await GetListAsync<CostRow>(t.Id, $"/api/costs?billId={billId}"));
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        Assert.False(await db.Ratings.IgnoreQueryFilters().AnyAsync(r => r.BillId == billId));
    }

    [Fact]
    public async Task Uat12_AdvancedBand_KeepsTheInclusiveUpperBound()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, new Dictionary<string, object?>
        {
            ["code"] = "RC-U12",
            ["name"] = "Nâng cao mua",
            ["partyType"] = "vendor",
            ["currencyCode"] = "USD",
            ["declarationMode"] = "advanced",
            ["explicitChargeGroups"] = true,
            ["supplierPartyId"] = t.Supplier,
            ["groups"] = new[] { Group("G1", t.Route, 2m, null, revenue: false, t.Freight) }
        });
        await PublishOkAsync(t.Id, created.RateVersionId);
        var billId = await BillAsync(t.Id, "BL-U12", "SGN-HAN");
        await PatchOkAsync(t.Id, $"/api/operational-references/bill/{billId}", new { changes = new { commodity_type_id = t.Commodity }, reason = "test" });
        var atBound = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = created.RateVersionId, quantity = 45m }));
        Assert.Equal(90m, atBound.TotalAmount);
        var above = await PostAsync(t.Id, "/api/ratings", new { billId, rateVersionId = created.RateVersionId, quantity = 45.001m });
        Assert.Equal(HttpStatusCode.Conflict, above.StatusCode);
        Assert.Contains("NO_APPLICABLE_RATE", (await ProblemAsync(above)).Message);
    }

    [Fact]
    public async Task Uat13_TwoTiersOfTheSameCharge_OnlyOneIsCharged()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, Card("RC-U13", "vendor", t.Supplier, new[]
        {
            Rule("T1", t.Freight, 10m, anyRoute: true, basis: "gross", min: 0m, max: 5m),
            Rule("T2", t.Freight, 20m, anyRoute: true, basis: "gross", min: 5m, max: 10m)
        }));
        var rules = await RulesAsync(t.Id, created.RateVersionId);
        Assert.All(rules, rule => Assert.Equal("FREIGHT", rule.ChargeCode));
        await PublishOkAsync(t.Id, created.RateVersionId);
        var billId = await BillAsync(t.Id, "BL-U13");
        var low = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = created.RateVersionId, quantity = 1m, grossWeightKg = 4m }));
        Assert.Equal(10m, low.TotalAmount);
        Assert.Equal("T1", Assert.Single(low.Details).RuleCode);
        var high = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = created.RateVersionId, quantity = 1m, grossWeightKg = 6m }));
        Assert.Equal(20m, high.TotalAmount);
        Assert.Equal("T2", Assert.Single(high.Details).RuleCode);
    }

    [Fact]
    public async Task Uat14_TwoIndependentCharges_BothAreAdded()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, Card("RC-U14", "vendor", t.Supplier, new[]
        {
            Rule("FRT", t.Freight, 10m, anyRoute: true),
            Rule("PCK", t.Pickup, 5m, anyRoute: true, costType: "PICKUP")
        }));
        var rules = await RulesAsync(t.Id, created.RateVersionId);
        Assert.Contains(rules, rule => rule.ChargeCode == "FREIGHT");
        Assert.Contains(rules, rule => rule.ChargeCode == "PICKUP");
        await PublishOkAsync(t.Id, created.RateVersionId);
        var billId = await BillAsync(t.Id, "BL-U14");
        var rating = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = created.RateVersionId, quantity = 1m }));
        Assert.Equal(15m, rating.TotalAmount);
        Assert.Equal(2, rating.Details.Count);
    }

    [Fact]
    public async Task Uat15_SameChargeEqualSpecificity_IsAmbiguous()
    {
        var t = await TenantAsync();
        var byCommodity = Rule("COM", t.Freight, 20m, anyRoute: true);
        byCommodity["commodityCode"] = "GENERAL";
        var created = await DeclareAsync(t.Id, Card("RC-U15", "vendor", t.Supplier, new[]
        {
            Rule("RTE", t.Freight, 10m, routeId: t.Route),
            byCommodity
        }));
        await PublishOkAsync(t.Id, created.RateVersionId);
        var billId = await BillAsync(t.Id, "BL-U15", "SGN-HAN");
        await PatchOkAsync(t.Id, $"/api/operational-references/bill/{billId}", new { changes = new { commodity_type_id = t.Commodity }, reason = "test" });
        var res = await PostAsync(t.Id, "/api/ratings", new { billId, rateVersionId = created.RateVersionId, quantity = 1m });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("AMBIGUOUS_RATE_RULE", (await ProblemAsync(res)).Message);
    }

    [Fact]
    public async Task Uat16_NoChargeCatalog_DraftSaves_PublishIsBlocked_NoFreightFallback()
    {
        var tenantId = await CreateTenantAsync();
        var supplier = await PartyAsync(tenantId, "NCC-U16", "vendor");
        var free = await PostAsync(tenantId, "/api/rate-cards", Card("RC-U16-FREE", "vendor", supplier, new[]
        {
            Rule("A", null, 10m, anyRoute: true, chargeCode: "FREIGHT")
        }));
        Assert.Equal((HttpStatusCode)422, free.StatusCode);
        Assert.Equal("invalid_charge_item", (await ProblemAsync(free)).Code);

        var res = await PostAsync(tenantId, "/api/rate-cards", Card("RC-U16", "vendor", supplier, new[] { Rule("A", null, 10m, anyRoute: true) }));
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        var created = JsonSerializer.Deserialize<Created>(text, Json)!;
        Assert.Contains(created.Warnings!, w => w.Contains("chưa có khoản giá chuẩn", StringComparison.Ordinal));
        Assert.Null((await RulesAsync(tenantId, created.RateVersionId)).Single().ChargeCode);

        var publish = await PostAsync(tenantId, $"/api/rate-versions/{created.RateVersionId}/publish", new { });
        var problem = await ProblemAsync(publish);
        Assert.Equal(HttpStatusCode.Conflict, publish.StatusCode);
        Assert.Contains("chưa có khoản giá chuẩn", problem.Message);
        Assert.Equal("draft", await StatusAsync(created.RateVersionId));
        Assert.Null((await RulesAsync(tenantId, created.RateVersionId)).Single().ChargeCode);

        var route = await RouteAsync(tenantId, "SGN-HAN");
        var advRes = await PostAsync(tenantId, "/api/rate-cards", new Dictionary<string, object?>
        {
            ["code"] = "RC-U16-ADV",
            ["name"] = "Nâng cao thiếu danh mục",
            ["partyType"] = "vendor",
            ["currencyCode"] = "USD",
            ["declarationMode"] = "advanced",
            ["explicitChargeGroups"] = true,
            ["supplierPartyId"] = supplier,
            ["groups"] = new[] { Group("G1", route, 2m, null, revenue: false) }
        });
        var advText = await advRes.Content.ReadAsStringAsync();
        Assert.True(advRes.IsSuccessStatusCode, advText);
        var advCreated = JsonSerializer.Deserialize<Created>(advText, Json)!;
        Assert.Contains(advCreated.Warnings!, w => w.Contains("chưa có khoản giá chuẩn", StringComparison.Ordinal));
        var advRules = await RulesAsync(tenantId, advCreated.RateVersionId);
        Assert.NotEmpty(advRules);
        Assert.All(advRules, r => Assert.Null(r.ChargeCode));

        var advPublish = await PostAsync(tenantId, $"/api/rate-versions/{advCreated.RateVersionId}/publish", new { });
        var advProblem = await ProblemAsync(advPublish);
        Assert.Equal(HttpStatusCode.Conflict, advPublish.StatusCode);
        Assert.Contains("chưa có khoản giá chuẩn", advProblem.Message);
        Assert.Equal("draft", await StatusAsync(advCreated.RateVersionId));
    }

    [Fact]
    public async Task Uat17_PublishedLegacyRemoteDeliveryRules_AreFlaggedAndNotRewritten()
    {
        var t = await TenantAsync();
        var cardId = await LegacyCardAsync(t.Id, "RC-NSE", "vendor");
        var versionId = await LegacyVersionAsync(t.Id, cardId);
        await PostOkAsync(t.Id, $"/api/rate-versions/{versionId}/rules", new
        {
            code = "BASE", name = "Cước chính", calcMethod = "fixed", unitAmount = 100m, currencyCode = "USD", sortOrder = 1
        });
        await PublishOkAsync(t.Id, versionId);
        var legacyCodes = new[] { "REMOTE_HN", "REMOTE_DN", "REMOTE_CT", "DELIVERY_1", "DELIVERY_2" };
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var sort = 10;
            foreach (var code in legacyCodes)
            {
                db.PricingRules.Add(new PricingRule
                {
                    TenantId = t.Id,
                    RateVersionId = versionId,
                    Code = code,
                    Name = code.StartsWith("REMOTE", StringComparison.Ordinal) ? "Phụ phí vùng xa" : "Phí giao hàng",
                    CalcMethod = "fixed",
                    UnitAmount = 9m,
                    CurrencyCode = "USD",
                    ChargeCode = code.StartsWith("REMOTE", StringComparison.Ordinal) ? "REMOTE" : "DELIVERY",
                    SortOrder = sort++,
                    IsActive = true
                });
            }

            await db.SaveChangesAsync();
        }

        var before = await RowsAsync(versionId);
        var warnings = await GetListAsync<WarningRow>(t.Id, $"/api/rate-versions/{versionId}/rule-warnings");
        var flagged = Assert.Single(warnings, w => w.Code == "legacy_surcharge_excluded");
        Assert.Equal(legacyCodes.OrderBy(c => c), flagged.RuleCodes.OrderBy(c => c));
        Assert.Contains("nguy cơ bỏ sót", flagged.Message);

        var billId = await BillAsync(t.Id, "BL-NSE");
        var rating = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = versionId, quantity = 1m }));
        Assert.Equal(100m, rating.TotalAmount);
        Assert.Equal(before, await RowsAsync(versionId));
        Assert.Equal("published", await StatusAsync(versionId));
    }

    [Fact]
    public async Task Uat18_NewVersionRuleNamedFuel_IsMainFreightNotSurcharge()
    {
        var t = await TenantAsync();
        var fuel = Rule("FUEL01", t.Freight, 30m, anyRoute: true);
        fuel["name"] = "FUEL cước chính";
        var created = await DeclareAsync(t.Id, Card("RC-U18", "vendor", t.Supplier, new[] { fuel }));
        await PublishOkAsync(t.Id, created.RateVersionId);
        Assert.Empty(await GetListAsync<WarningRow>(t.Id, $"/api/rate-versions/{created.RateVersionId}/rule-warnings"));
        var billId = await BillAsync(t.Id, "BL-U18");
        var rating = await RatingAsync(t.Id, await RateOkAsync(t.Id, new { billId, rateVersionId = created.RateVersionId, quantity = 1m }));
        Assert.Equal(30m, rating.TotalAmount);
        var line = Assert.Single(rating.Details);
        Assert.Equal("FUEL01", line.RuleCode);
        Assert.Equal("base_rate", line.SourceType);
    }

    [Fact]
    public async Task Uat19_LegRetrySameInput_DoesNotDuplicateExpectedCost()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, Card("RC-U19", "vendor", t.Supplier, new[] { Rule("LEG", t.Freight, 10m, anyRoute: true, unitRate: true) }));
        await PublishOkAsync(t.Id, created.RateVersionId);
        var billId = await BillAsync(t.Id, "BL-U19");
        var legId = await LegAsync(t.Id, billId, "U19");
        var body = new
        {
            billId, rateVersionId = created.RateVersionId, quantity = 4m, contextType = "leg", transportLegId = legId, vendorPartyId = t.Supplier, seedExpectedCosts = true
        };
        var first = await RateOkAsync(t.Id, body);
        var second = await RateOkAsync(t.Id, body);
        Assert.Equal(first, second);
        var cost = Assert.Single(await GetListAsync<CostRow>(t.Id, $"/api/costs?billId={billId}"));
        Assert.Equal(40m, cost.ExpectedAmount);
    }

    [Fact]
    public async Task Uat20_HistoricalRatingTraceAndAmounts_StayUnchanged()
    {
        var t = await TenantAsync();
        var created = await DeclareAsync(t.Id, Card("RC-U20", "vendor", t.Supplier, new[] { Rule("HIS", t.Freight, 10m, anyRoute: true) }));
        await PublishOkAsync(t.Id, created.RateVersionId);
        var billId = await BillAsync(t.Id, "BL-U20");
        var ratingId = await RateOkAsync(t.Id, new { billId, rateVersionId = created.RateVersionId, quantity = 1m });
        var before = await RatingAsync(t.Id, ratingId);
        var oldRules = await RowsAsync(created.RateVersionId);

        var draft = await PostOkAsync(t.Id, $"/api/rate-cards/{created.RateCardId}/versions", new { note = "v2" });
        var card = await GetAsync<CardRow>(t.Id, $"/api/rate-cards/{created.RateCardId}");
        var put = await SendAsync(HttpMethod.Put, t.Id, $"/api/rate-cards/{created.RateCardId}", Card("RC-U20", "vendor", t.Supplier, new[] { Rule("HIS", t.Freight, 99m, anyRoute: true) }, card.RowVersion, draft.Id));
        Assert.True(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());
        await PublishOkAsync(t.Id, draft.Id);

        var after = await RatingAsync(t.Id, ratingId);
        Assert.Equal(before.TotalAmount, after.TotalAmount);
        Assert.Equal(before.RateVersionId, after.RateVersionId);
        var oldLine = Assert.Single(before.Details);
        var newLine = Assert.Single(after.Details);
        Assert.Equal(oldLine.RuleCode, newLine.RuleCode);
        Assert.Equal(oldLine.PricingRuleId, newLine.PricingRuleId);
        Assert.Equal(oldLine.SourceVersionId, newLine.SourceVersionId);
        Assert.Equal(oldLine.Amount, newLine.Amount);
        Assert.Equal(created.RateVersionId, newLine.SourceVersionId);
        Assert.Equal(oldRules, await RowsAsync(created.RateVersionId));
    }

    [Fact]
    public async Task Data01_EconomicChargeCatalogSeeder_SeedsOpsAndDemoIdempotently()
    {
        Guid opsId;
        Guid demoId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var ops = await db.Tenants.FirstOrDefaultAsync(t => t.Code.ToLower() == "ops");
            if (ops is null)
            {
                ops = new Tenant { Code = "ops", Name = "Vận hành", IsActive = true };
                db.Tenants.Add(ops);
            }

            var demo = await db.Tenants.FirstOrDefaultAsync(t => t.Code.ToLower() == "demo");
            if (demo is null)
            {
                demo = new Tenant { Code = "demo", Name = "Demo", IsActive = true };
                db.Tenants.Add(demo);
            }

            await db.SaveChangesAsync();
            opsId = ops.Id;
            demoId = demo.Id;

            await EconomicChargeCatalogSeeder.EnsureForDemoAndOpsTenantsAsync(db);
            await EconomicChargeCatalogSeeder.EnsureForDemoAndOpsTenantsAsync(db);
        }

        var expected = new[] { "DELIVERY", "FREIGHT", "FUEL", "HANDLING", "PICKUP", "REMOTE" };
        foreach (var tid in new[] { opsId, demoId })
        {
            var items = await GetListAsync<EconomicChargeTypeDto>(tid, "/api/economic-charge-types");
            Assert.Equal(expected, items.Select(i => i.Code).OrderBy(c => c).ToArray());
            Assert.All(items, i => Assert.True(i.IsActive));
        }
    }

    private sealed record TenantSetup(Guid Id, Guid Supplier, Guid Route, Guid Freight, Guid Pickup, Guid Commodity);

    private async Task<TenantSetup> TenantAsync()
    {
        var tenantId = await CreateTenantAsync();
        var supplier = await PartyAsync(tenantId, "NCC-UAT", "vendor");
        var route = await RouteAsync(tenantId, "SGN-HAN");
        var freight = (await PostOkAsync(tenantId, "/api/economic-charge-types", new { code = "FREIGHT", name = "Cước vận tải" })).Id;
        var pickup = (await PostOkAsync(tenantId, "/api/economic-charge-types", new { code = "PICKUP", name = "Phí lấy hàng" })).Id;
        var commodity = await PutIdAsync(tenantId, "/api/commodities", new { code = "GENERAL", name = "Hàng thường", isActive = true });
        return new TenantSetup(tenantId, supplier, route, freight, pickup, commodity);
    }

    private static Dictionary<string, object?> Card(
        string code,
        string partyType,
        Guid? supplier,
        IEnumerable<Dictionary<string, object?>> rules,
        string? rowVersion = null,
        Guid? rateVersionId = null) => new()
    {
        ["code"] = code,
        ["name"] = code,
        ["partyType"] = partyType,
        ["currencyCode"] = "USD",
        ["declarationMode"] = "basic",
        ["explicitChargeGroups"] = true,
        ["supplierPartyId"] = supplier,
        ["rowVersion"] = rowVersion,
        ["rateVersionId"] = rateVersionId,
        ["rules"] = rules.ToArray()
    };

    private static Dictionary<string, object?> Rule(
        string code,
        Guid? chargeTypeId,
        decimal amount,
        bool sell = false,
        Guid? routeId = null,
        bool anyRoute = false,
        bool unitRate = false,
        string? basis = null,
        decimal? min = null,
        decimal? max = null,
        string costType = "FREIGHT",
        string? chargeCode = null) => new()
    {
        ["code"] = code,
        ["name"] = "Quy tắc " + code,
        ["calcMethod"] = unitRate ? "unit_rate" : "fixed",
        ["unitAmount"] = amount,
        ["currencyCode"] = "USD",
        ["routeId"] = anyRoute ? null : routeId,
        ["appliesAnyRoute"] = anyRoute,
        ["lineRole"] = "base_rate",
        ["economicChargeTypeId"] = chargeTypeId,
        ["chargeCode"] = chargeCode,
        ["weightBasis"] = basis,
        ["weightMin"] = min,
        ["weightMax"] = max,
        ["costTypeCode"] = sell ? null : costType,
        ["revenueTypeCode"] = sell ? costType : null
    };

    private static object Group(string code, Guid routeId, decimal low, decimal? high, bool revenue, Guid? economicChargeTypeId = null) => new
    {
        code,
        routeId,
        appliesAnyRoute = false,
        economicChargeTypeId,
        costTypeCode = revenue ? null : "FREIGHT",
        revenueTypeCode = revenue ? "FREIGHT" : null,
        columns = new[] { new { code = "GENERAL", name = "Hàng thường" } },
        bands = high is null
            ? new object[] { new { minQuantity = 0m, maxQuantity = (decimal?)45m, prices = new decimal?[] { low } } }
            : new object[]
            {
                new { minQuantity = 0m, maxQuantity = (decimal?)45m, prices = new decimal?[] { low } },
                new { minQuantity = 45m, maxQuantity = (decimal?)null, prices = new decimal?[] { high } }
            }
    };

    private async Task<string> RowAsync(Guid versionId, string code) =>
        (await RowsAsync(versionId)).Single(row => row.StartsWith(code + "|", StringComparison.Ordinal));

    private async Task<List<string>> RowsAsync(Guid versionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var rows = await db.PricingRules.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.RateVersionId == versionId)
            .ToListAsync();
        return rows
            .OrderBy(r => r.Code)
            .Select(r => $"{r.Code}|{r.Name}|{r.PartyTypeCode}|{r.ChargeCode}|{r.UnitAmount}|{r.WeightBasis}|{r.WeightMin}|{r.WeightMax}|{r.IsActive}|{r.DeletedAt}")
            .ToList();
    }

    private async Task MutateRuleAsync(Guid versionId, Action<PricingRule> change)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var rule = await db.PricingRules.IgnoreQueryFilters().SingleAsync(r => r.RateVersionId == versionId && r.DeletedAt == null);
        change(rule);
        await db.SaveChangesAsync();
    }

    private async Task<string> StatusAsync(Guid versionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        return await db.RateVersions.IgnoreQueryFilters().Where(v => v.Id == versionId).Select(v => v.Status).SingleAsync();
    }

    private async Task<Guid> LegAsync(Guid tenantId, Guid billId, string suffix)
    {
        var shipmentId = await PutIdAsync(tenantId, "/api/shipments", new { shipmentNo = "SH-" + suffix, sourceSystem = "lcms_manual", externalId = "sh-" + suffix, isActive = true });
        var legId = await PutIdAsync(tenantId, "/api/transport-legs", new { legNo = "LEG-" + suffix, shipmentId, sourceSystem = "lcms_manual", externalId = "leg-" + suffix, isActive = true });
        var link = await PostAsync(tenantId, $"/api/transport-legs/{legId}/bills/{billId}", new { });
        link.EnsureSuccessStatusCode();
        return legId;
    }

    private async Task<Guid> BillAsync(Guid tenantId, string billNo, string? routeCode = null)
    {
        var id = (await PostOkAsync(tenantId, "/api/bills", new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo })).Id;
        if (routeCode is not null)
        {
            await PatchOkAsync(tenantId, $"/api/bills/{id}/context", new { routeCode });
        }

        return id;
    }

    private async Task<Guid> LegacyCardAsync(Guid tenantId, string code, string partyType) =>
        (await PostOkAsync(tenantId, "/api/rate-cards", new { code, name = code, partyType, currencyCode = "USD" })).Id;

    private async Task<Guid> LegacyVersionAsync(Guid tenantId, Guid cardId) =>
        (await PostOkAsync(tenantId, $"/api/rate-cards/{cardId}/versions", new { note = "legacy" })).Id;

    private async Task<Created> DeclareAsync(Guid tenantId, object body) => await PostOkAsync(tenantId, "/api/rate-cards", body);

    private async Task PublishOkAsync(Guid tenantId, Guid versionId)
    {
        var res = await PostAsync(tenantId, $"/api/rate-versions/{versionId}/publish", new { });
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<Guid> RateOkAsync(Guid tenantId, object body) => (await PostOkAsync(tenantId, "/api/ratings", body)).Id;

    private async Task<RatingRow> RatingAsync(Guid tenantId, Guid ratingId) => await GetAsync<RatingRow>(tenantId, $"/api/ratings/{ratingId}");

    private async Task<List<RuleRow>> RulesAsync(Guid tenantId, Guid versionId) =>
        await GetListAsync<RuleRow>(tenantId, $"/api/rate-versions/{versionId}/rules");

    private async Task<Guid> PartyAsync(Guid tenantId, string code, string role) =>
        (await PostOkAsync(tenantId, "/api/business-parties", new { code, name = code, roleCodes = new[] { role } })).Id;

    private async Task<Guid> RouteAsync(Guid tenantId, string code)
    {
        var origin = await PutIdAsync(tenantId, "/api/locations", new { code = code + "-O", name = code, locationType = "airport", countryCode = "VN", isActive = true, aliases = Array.Empty<object>() });
        var destination = await PutIdAsync(tenantId, "/api/locations", new { code = code + "-D", name = code, locationType = "airport", countryCode = "VN", isActive = true, aliases = Array.Empty<object>() });
        return await PutIdAsync(tenantId, "/api/routes", new { code, name = code, originLocationId = origin, destinationLocationId = destination, isActive = true, intermediateLocationIds = Array.Empty<Guid>() });
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var res = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "UAT RATE-UI-05..07" });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<Created>(Json))!.Id;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, Guid tenantId, string path, object? body)
    {
        using var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(req);
    }

    private Task<HttpResponseMessage> PostAsync(Guid tenantId, string path, object body) => SendAsync(HttpMethod.Post, tenantId, path, body);

    private async Task<Created> PostOkAsync(Guid tenantId, string path, object body)
    {
        var res = await PostAsync(tenantId, path, body);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<Created>(text, Json)!;
    }

    private async Task PatchOkAsync(Guid tenantId, string path, object body)
    {
        var res = await SendAsync(HttpMethod.Patch, tenantId, path, body);
        Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
    }

    private async Task<Guid> PutIdAsync(Guid tenantId, string path, object body)
    {
        var res = await SendAsync(HttpMethod.Put, tenantId, path, body);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<Created>(text, Json)!.Id;
    }

    private async Task<T> GetAsync<T>(Guid tenantId, string path)
    {
        var res = await SendAsync(HttpMethod.Get, tenantId, path, null);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        return JsonSerializer.Deserialize<T>(text, Json)!;
    }

    private Task<List<T>> GetListAsync<T>(Guid tenantId, string path) => GetAsync<List<T>>(tenantId, path);

    private static async Task<ProblemRow> ProblemAsync(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<ProblemRow>(Json))!;

    private sealed record Created(Guid Id, Guid RateCardId, Guid RateVersionId, List<string>? Warnings);
    private sealed record RuleRow(string Code, string? PartyTypeCode, string? ChargeCode, string? WeightBasis, decimal? WeightMin, decimal? WeightMax);
    private sealed record RatingRow(decimal TotalAmount, Guid RateVersionId, string? PartyTypeCode, List<DetailRow> Details);
    private sealed record DetailRow(string RuleCode, decimal Amount, string FinancialNature, Guid? PricingRuleId, Guid? SourceVersionId, string? SourceType);
    private sealed record CostRow(decimal? ExpectedAmount);
    private sealed record CardRow(string? RowVersion);
    private sealed record WarningRow(string Code, string Message, List<string> RuleCodes);
    private sealed record ProblemRow(string Code, string Message);
    private sealed record PreviewBody(bool CanCommit, List<IssueRow> Issues);
    private sealed record IssueRow(int Row, string Field, string Message);
}
