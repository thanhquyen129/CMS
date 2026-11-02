using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

/// <summary>TC-22 / TC-23 / TC-24 + COST-RATING-SOURCE for the 2026-11-02 consolidated UAT package.</summary>
[Collection("Api")]
public sealed class ConsolidatedUat20261102ApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public ConsolidatedUat20261102ApiTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Cost_rating_trace_reads_freight_base_rate_and_donggoi_surcharge_from_snapshot()
    {
        var tenantId = await CreateTenantAsync("TN-TRACE");
        var billId = await CreateBillAsync(tenantId, "TESTLSS002");

        Guid freightCostId;
        Guid packCostId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var cardId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var ruleId = Guid.NewGuid();
            var ratingId = Guid.NewGuid();
            var freightDetailId = Guid.NewGuid();
            var packDetailId = Guid.NewGuid();
            var surchargeId = Guid.NewGuid();
            var surchargeVersionId = Guid.NewGuid();
            freightCostId = Guid.NewGuid();
            packCostId = Guid.NewGuid();

            db.RateCards.Add(new RateCard
            {
                Id = cardId,
                TenantId = tenantId,
                Code = "TDNT_SGN_HAN",
                Name = "TDNT SGN HAN",
                PartyType = "vendor",
                CurrencyCode = "VND",
                IsActive = true
            });
            db.RateVersions.Add(new RateVersion
            {
                Id = versionId,
                TenantId = tenantId,
                RateCardId = cardId,
                VersionNo = 2,
                Status = "published"
            });
            db.PricingRules.Add(new PricingRule
            {
                Id = ruleId,
                TenantId = tenantId,
                RateVersionId = versionId,
                Code = "QT0001",
                Name = "Cước chính",
                CalcMethod = "unit_rate",
                UnitAmount = 12000m,
                ChargeCode = "FREIGHT",
                RouteCode = "SGN-HAN",
                OriginCode = "SGN",
                DestinationCode = "HAN",
                IsActive = true
            });
            db.Surcharges.Add(new Surcharge
            {
                Id = surchargeId,
                TenantId = tenantId,
                Code = "DONGGOI",
                Name = "Phụ phí đóng gói",
                Direction = SurchargeDirections.Buy,
                Status = SurchargeStatuses.Active
            });
            db.SurchargeVersions.Add(new SurchargeVersion
            {
                Id = surchargeVersionId,
                TenantId = tenantId,
                SurchargeId = surchargeId,
                VersionNo = 2,
                PublishStatus = SurchargeVersionStatuses.Published,
                PublishedAt = DateTimeOffset.UtcNow
            });
            db.SurchargeRules.Add(new SurchargeRule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SurchargeVersionId = surchargeVersionId,
                CalculationMode = SurchargeCalcModes.FixedRate,
                RateAmountPercent = 300000m,
                CurrencyCode = "VND"
            });

            const string contextJson =
                """{"rateCardCode":"TDNT_SGN_HAN","rateCardName":"TDNT SGN HAN","versionNo":2,"chargeable":120,"rules":[{"code":"QT0001","unitAmount":12000,"calcMethod":"unit_rate","applied_weight":120}],"surcharges":["DONGGOI v2"]}""";

            db.Ratings.Add(new Rating
            {
                Id = ratingId,
                TenantId = tenantId,
                BillId = billId,
                RateCardId = cardId,
                RateVersionId = versionId,
                PricingRuleId = ruleId,
                TotalAmount = 1740000m,
                ChargeableWeightKg = 120m,
                CurrencyCode = "VND",
                Status = RatingStatuses.Completed,
                ContextJson = contextJson,
                RateDate = new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero)
            });
            db.RatingDetails.Add(new RatingDetail
            {
                Id = freightDetailId,
                TenantId = tenantId,
                RatingId = ratingId,
                PricingRuleId = ruleId,
                RuleCode = "QT0001",
                ComponentCode = "FREIGHT",
                ComponentName = "Cước chính",
                Amount = 1440000m,
                NetAmount = 1440000m,
                CurrencyCode = "VND",
                SourceType = RatingSourceTypes.BaseRate,
                SourceId = cardId,
                SourceVersionId = versionId,
                FormulaText = "120 kg × 12.000 ₫/kg"
            });
            db.RatingDetails.Add(new RatingDetail
            {
                Id = packDetailId,
                TenantId = tenantId,
                RatingId = ratingId,
                RuleCode = "DONGGOI",
                ComponentCode = "DONGGOI",
                ComponentName = "Phụ phí đóng gói",
                Amount = 300000m,
                NetAmount = 300000m,
                CurrencyCode = "VND",
                SourceType = RatingSourceTypes.Surcharge,
                SourceId = surchargeId,
                SourceVersionId = surchargeVersionId,
                FormulaText = "Cố định 300.000 ₫ / khoản"
            });
            db.Costs.Add(new Cost
            {
                Id = freightCostId,
                TenantId = tenantId,
                BillId = billId,
                CostTypeCode = "FREIGHT",
                AttributionType = CostAttributionTypes.Direct,
                FinancialMaturity = CostMaturities.Expected,
                ExpectedAmount = 1440000m,
                Amount = 1440000m,
                NetAmount = 1440000m,
                CurrencyCode = "VND",
                SourceType = CostSourceTypes.RatingDetail,
                SourceId = freightDetailId,
                RateCardId = cardId,
                RateVersionId = versionId,
                PricingRuleId = ruleId,
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                RecordStatus = "active"
            });
            db.Costs.Add(new Cost
            {
                Id = packCostId,
                TenantId = tenantId,
                BillId = billId,
                CostTypeCode = "DONGGOI",
                AttributionType = CostAttributionTypes.Direct,
                FinancialMaturity = CostMaturities.Expected,
                ExpectedAmount = 300000m,
                Amount = 300000m,
                NetAmount = 300000m,
                CurrencyCode = "VND",
                SourceType = CostSourceTypes.RatingDetail,
                SourceId = packDetailId,
                EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
                RecordStatus = "active"
            });
            await db.SaveChangesAsync();
        }

        var freight = await GetCostAsync(tenantId, freightCostId);
        Assert.NotNull(freight.RatingTrace);
        Assert.Equal("TDNT_SGN_HAN", freight.RatingTrace!.RateCardCode);
        Assert.Equal(2, freight.RatingTrace.RateVersionNo);
        Assert.Equal("QT0001", freight.RatingTrace.RuleCode);
        Assert.Equal(120m, freight.RatingTrace.ChargeableWeightKg);
        Assert.Equal(12000m, freight.RatingTrace.UnitRate);
        Assert.False(freight.RatingTrace.IsSurcharge);
        Assert.Equal("rating", freight.RatingTrace.SourceOrigin);

        var pack = await GetCostAsync(tenantId, packCostId);
        Assert.NotNull(pack.RatingTrace);
        Assert.True(pack.RatingTrace!.IsSurcharge);
        Assert.Equal("DONGGOI", pack.RatingTrace.SurchargeCode);
        Assert.Equal(2, pack.RatingTrace.RateVersionNo);
        Assert.Equal("fixed", pack.RatingTrace.CalculationMode);
        Assert.Equal("surcharge", pack.RatingTrace.SourceOrigin);
        Assert.Equal(300000m, pack.RatingTrace.NetAmount);
    }

    [Fact]
    public async Task Tc22_partial_collection_keeps_bill_profit_unchanged()
    {
        var tenantId = await CreateTenantAsync("TN-TC22");
        var billId = await CreateBillAsync(tenantId, "BL-TC22");
        await CreateCostAsync(tenantId, billId, 120000m);
        var revenueId = await CreateRevenueAsync(tenantId, billId, 500000m);

        var profitBefore = await GetProfitAsync(tenantId, billId);
        Assert.Equal(380000m, Assert.Single(profitBefore.ByCurrency).ProfitAmount);

        var exposureId = await CreateReceivableExposureAsync(tenantId, billId, revenueId, 500000m);
        var arId = await RecognizeReceivableAsync(tenantId, exposureId, 500000m);

        var collectionId = await CreateCollectionAsync(tenantId, billId, 200000m);
        var allocId = await AllocateCollectionAsync(tenantId, collectionId, arId, 200000m);
        await FinalizeCollectionAsync(tenantId, allocId);

        var ar = await GetArAsync(tenantId, arId);
        Assert.Equal(200000m, ar.FinalizedSettledAmount);
        Assert.Equal(300000m, ar.Outstanding);
        Assert.Equal("partially_settled", ar.SettlementStatus);

        var profitAfter = await GetProfitAsync(tenantId, billId);
        Assert.Equal(380000m, Assert.Single(profitAfter.ByCurrency).ProfitAmount);
    }

    [Fact]
    public async Task Tc23_remaining_collection_settles_ar_and_retry_is_idempotent()
    {
        var tenantId = await CreateTenantAsync("TN-TC23");
        var billId = await CreateBillAsync(tenantId, "BL-TC23");
        await CreateCostAsync(tenantId, billId, 120000m);
        var revenueId = await CreateRevenueAsync(tenantId, billId, 500000m);
        var exposureId = await CreateReceivableExposureAsync(tenantId, billId, revenueId, 500000m);
        var arId = await RecognizeReceivableAsync(tenantId, exposureId, 500000m);

        var first = await CreateCollectionAsync(tenantId, billId, 200000m);
        var firstAlloc = await AllocateCollectionAsync(tenantId, first, arId, 200000m);
        await FinalizeCollectionAsync(tenantId, firstAlloc);

        var rest = await CreateCollectionAsync(tenantId, billId, 300000m);
        var restAlloc = await AllocateCollectionAsync(tenantId, rest, arId, 300000m);
        await FinalizeCollectionAsync(tenantId, restAlloc);

        var ar = await GetArAsync(tenantId, arId);
        Assert.Equal(500000m, ar.FinalizedSettledAmount);
        Assert.Equal(0m, ar.Outstanding);
        Assert.Contains(ar.SettlementStatus, new[] { "settled", "closed" });

        var aging = await GetAsync<AgingBody>(tenantId, $"/api/accounts-receivable/aging?asOf={DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}&includeSettled=true");
        Assert.Contains(aging.ReceivableItems ?? [], r => r.Id == arId && r.Outstanding == 0m);

        using var retry = Tenant(HttpMethod.Post, $"/api/collection-allocations/{restAlloc}/finalize", tenantId);
        var retryRes = await _client.SendAsync(retry);
        Assert.True(
            retryRes.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict,
            $"retry finalize => {(int)retryRes.StatusCode}");

        var afterRetry = await GetArAsync(tenantId, arId);
        Assert.Equal(500000m, afterRetry.FinalizedSettledAmount);
        Assert.Equal(0m, afterRetry.Outstanding);

        Assert.Equal(380000m, Assert.Single((await GetProfitAsync(tenantId, billId)).ByCurrency).ProfitAmount);
    }

    [Fact]
    public async Task Tc24_revenue_split_conserves_500k_across_two_bills_without_doubling()
    {
        var tenantId = await CreateTenantAsync("TN-TC24");
        var billA = await CreateBillAsync(tenantId, "BL-TC24-A");
        var billB = await CreateBillAsync(tenantId, "BL-TC24-B");
        var revenueId = await CreateRevenueAsync(tenantId, billA, 500000m);

        using var map = Tenant(HttpMethod.Post, $"/api/revenues/{revenueId}/mappings", tenantId);
        map.Content = JsonContent.Create(new
        {
            allocationBasis = "manual_ratio",
            details = new[]
            {
                new { billId = billA, basisValue = 3m },
                new { billId = billB, basisValue = 2m }
            }
        });
        var mapRes = await _client.SendAsync(map);
        mapRes.EnsureSuccessStatusCode();
        var mappingId = (await mapRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        using var fin = Tenant(HttpMethod.Post, $"/api/revenue-mappings/{mappingId}/finalize", tenantId);
        (await _client.SendAsync(fin)).EnsureSuccessStatusCode();

        var profitA = await GetProfitAsync(tenantId, billA);
        var profitB = await GetProfitAsync(tenantId, billB);
        var shareA = Assert.Single(profitA.ByCurrency).RevenueAmount;
        var shareB = Assert.Single(profitB.ByCurrency).RevenueAmount;
        Assert.Equal(300000m, shareA);
        Assert.Equal(200000m, shareB);
        Assert.Equal(500000m, shareA + shareB);

        using var map2 = Tenant(HttpMethod.Post, $"/api/revenues/{revenueId}/mappings", tenantId);
        map2.Content = JsonContent.Create(new
        {
            allocationBasis = "manual_ratio",
            details = new[]
            {
                new { billId = billA, basisValue = 3m },
                new { billId = billB, basisValue = 2m }
            }
        });
        var map2Res = await _client.SendAsync(map2);
        map2Res.EnsureSuccessStatusCode();
        var mapping2 = (await map2Res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var fin2 = Tenant(HttpMethod.Post, $"/api/revenue-mappings/{mapping2}/finalize", tenantId);
        (await _client.SendAsync(fin2)).EnsureSuccessStatusCode();

        var afterA = Assert.Single((await GetProfitAsync(tenantId, billA)).ByCurrency).RevenueAmount;
        var afterB = Assert.Single((await GetProfitAsync(tenantId, billB)).ByCurrency).RevenueAmount;
        Assert.Equal(500000m, afterA + afterB);
        Assert.NotEqual(1000000m, afterA + afterB);
    }

    [Fact]
    public async Task Exception_exposure_requires_notes_and_valid_ar_role()
    {
        var tenantId = await CreateTenantAsync("TN-EXP-EX");

        using var vendor = Tenant(HttpMethod.Post, "/api/business-parties", tenantId);
        vendor.Content = JsonContent.Create(new
        {
            code = "VEN-ONLY",
            name = "Chỉ vendor",
            roleCodes = new[] { "vendor" }
        });
        var vendorId = (await (await _client.SendAsync(vendor)).Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        using var missingNotes = Tenant(HttpMethod.Post, "/api/receivable-exposures", tenantId);
        missingNotes.Content = JsonContent.Create(new
        {
            amount = 100m,
            currencyCode = "VND",
            counterpartyId = vendorId
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(missingNotes)).StatusCode);

        using var badRole = Tenant(HttpMethod.Post, "/api/receivable-exposures", tenantId);
        badRole.Content = JsonContent.Create(new
        {
            amount = 100m,
            currencyCode = "VND",
            counterpartyId = vendorId,
            notes = "Căn cứ ngoại lệ thiếu vai trò AR"
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(badRole)).StatusCode);

        using var okParty = Tenant(HttpMethod.Post, "/api/business-parties", tenantId);
        okParty.Content = JsonContent.Create(new
        {
            code = "CUS-OK",
            name = "Khách ngoại lệ",
            roleCodes = new[] { "customer", "payer" }
        });
        var okId = (await (await _client.SendAsync(okParty)).Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        using var ok = Tenant(HttpMethod.Post, "/api/receivable-exposures", tenantId);
        ok.Content = JsonContent.Create(new
        {
            amount = 100m,
            currencyCode = "VND",
            counterpartyId = okId,
            notes = "Căn cứ ngoại lệ hợp lệ"
        });
        (await _client.SendAsync(ok)).EnsureSuccessStatusCode();
    }

    private async Task<CostBody> GetCostAsync(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/costs/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CostBody>(Json))!;
    }

    private async Task<Guid> CreateCostAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = Tenant(HttpMethod.Post, "/api/costs", tenantId);
        req.Content = JsonContent.Create(new
        {
            billId,
            attributionType = "direct",
            amount,
            currencyCode = "VND",
            costTypeCode = "FREIGHT"
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateRevenueAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = Tenant(HttpMethod.Post, "/api/revenues", tenantId);
        req.Content = JsonContent.Create(new
        {
            billId,
            amount,
            currencyCode = "VND",
            revenueTypeCode = "FREIGHT"
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateReceivableExposureAsync(Guid tenantId, Guid billId, Guid revenueId, decimal amount)
    {
        using var req = Tenant(HttpMethod.Post, "/api/receivable-exposures", tenantId);
        req.Content = JsonContent.Create(new
        {
            amount,
            currencyCode = "VND",
            billId,
            revenueId
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> RecognizeReceivableAsync(Guid tenantId, Guid exposureId, decimal amount)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/receivable-exposures/{exposureId}/recognize", tenantId);
        req.Content = JsonContent.Create(new { amount });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateCollectionAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = Tenant(HttpMethod.Post, "/api/collections", tenantId);
        req.Content = JsonContent.Create(new { amount, currencyCode = "VND", billId });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> AllocateCollectionAsync(Guid tenantId, Guid collectionId, Guid arId, decimal amount)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/collections/{collectionId}/allocations", tenantId);
        req.Content = JsonContent.Create(new { accountsReceivableId = arId, amount });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task FinalizeCollectionAsync(Guid tenantId, Guid allocationId)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/collection-allocations/{allocationId}/finalize", tenantId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<ArBody> GetArAsync(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/accounts-receivable/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ArBody>(Json))!;
    }

    private async Task<ProfitBody> GetProfitAsync(Guid tenantId, Guid billId)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/bills/{billId}/profitability?view=expected", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ProfitBody>(Json))!;
    }

    private async Task<T> GetAsync<T>(Guid tenantId, string url)
    {
        using var req = Tenant(HttpMethod.Get, url, tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = Tenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateTenantAsync(string code)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = code + Guid.NewGuid().ToString("N")[..4], name = code });
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
    private sealed record CostBody(Guid Id, CostTraceBody? RatingTrace);
    private sealed record CostTraceBody(
        string? RateCardCode,
        int? RateVersionNo,
        string? RuleCode,
        decimal? ChargeableWeightKg,
        decimal? UnitRate,
        bool IsSurcharge,
        string? SurchargeCode,
        string? CalculationMode,
        string SourceOrigin,
        decimal? NetAmount);
    private sealed record ArBody(
        decimal FinalizedSettledAmount,
        decimal Outstanding,
        string SettlementStatus);
    private sealed record ProfitBody(List<Bucket> ByCurrency);
    private sealed record Bucket(string CurrencyCode, decimal RevenueAmount, decimal ProfitAmount);
    private sealed record AgingBody(List<AgingRow>? ReceivableItems);
    private sealed record AgingRow(Guid Id, decimal Outstanding);
}
