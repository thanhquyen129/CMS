using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class ChargeProfitVatAcceptanceTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public ChargeProfitVatAcceptanceTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Packaging_loss_uses_net_and_published_vat_stays_put()
    {
        var tenantId = await CreateTenantAsync();
        var supplierA = await CreatePartyAsync(tenantId, "NCC-A", "vendor");
        var supplierB = await CreatePartyAsync(tenantId, "NCC-B", "vendor");
        var customer = await CreatePartyAsync(tenantId, "KH-A", "customer");
        await PostOk(tenantId, "/api/economic-charge-types", new { code = "PACKAGING", name = "Đóng gói" });
        await PostOk(tenantId, "/api/economic-charge-types", new { code = "PICKUP", name = "Lấy hàng" });

        var billId = await CreateBillAsync(tenantId, "BL-CP-1");
        var buyCard = await CreateCardAsync(tenantId, "RC-BUY", "vendor");
        await PutOk(tenantId, $"/api/rate-cards/{buyCard}/partner", new { supplierPartyId = supplierA });
        var buyVersion = await CreateVersionAsync(tenantId, buyCard, null);
        await AddRuleAsync(tenantId, buyVersion, "PACKAGING", 200_000m);
        await AddRuleAsync(tenantId, buyVersion, "PICKUP", 10_000m);
        await PublishAsync(tenantId, buyVersion);

        var sellCard = await CreateCardAsync(tenantId, "RC-SELL", "customer");
        await PutOk(tenantId, $"/api/rate-cards/{sellCard}/partner", new { customerPartyId = customer });
        var sellVersion = await CreateVersionAsync(tenantId, sellCard, 10m);
        await AddRuleAsync(tenantId, sellVersion, "PACKAGING", 150_000m);
        await AddRuleAsync(tenantId, sellVersion, "PICKUP", 40_000m);
        await PublishAsync(tenantId, sellVersion);

        var buyRating = await RateAsync(tenantId, new { billId, rateVersionId = buyVersion, quantity = 1m, seedExpectedCosts = true });
        var sellRating = await RateAsync(tenantId, new { billId, rateVersionId = sellVersion, quantity = 1m, seedExpectedRevenues = true });
        var buy = await GetRatingAsync(tenantId, buyRating);
        var sell = await GetRatingAsync(tenantId, sellRating);
        var sellPack = sell.Details.Single(d => d.ComponentCode == "PACKAGING");
        Assert.Equal(150_000m, sellPack.Amount);
        Assert.Equal(10m, sellPack.VatRate);
        Assert.Equal(15_000m, sellPack.VatAmount);
        Assert.Equal(165_000m, sellPack.GrossAmount);
        Assert.Null(buy.Details.Single(d => d.ComponentCode == "PACKAGING").VatRate);
        Assert.NotEqual(0m, sellPack.VatAmount);

        var profit = await GetProfitAsync(tenantId, $"/api/bills/{billId}/charge-profitability?view=expected");
        var packaging = profit.Rows.Single(r => r.ChargeCode == "PACKAGING");
        var pickup = profit.Rows.Single(r => r.ChargeCode == "PICKUP");
        Assert.Equal(-50_000m, packaging.ProfitReporting);
        Assert.True(packaging.NegativeFlag);
        Assert.Equal(30_000m, pickup.ProfitReporting);
        Assert.False(pickup.NegativeFlag);
        Assert.Contains(packaging.Sources, s => s.PartnerId == supplierA);

        var deniedVat = await SendAsync(tenantId, HttpMethod.Put, $"/api/rate-versions/{sellVersion}/vat", new { vatRate = 8m });
        Assert.Equal(HttpStatusCode.Conflict, deniedVat.StatusCode);
        var versions = await GetAsync<List<VersionBody>>(tenantId, $"/api/rate-cards/{sellCard}/versions");
        Assert.Equal(10m, versions.Single().VatRate);
        var sellAfter = await GetRatingAsync(tenantId, sellRating);
        Assert.Equal(15_000m, sellAfter.Details.Single(d => d.ComponentCode == "PACKAGING").VatAmount);

        var wrongWay = await SendAsync(tenantId, HttpMethod.Post, "/api/surcharges", new
        {
            code = "BAD-BUY",
            name = "Sai chiều",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 1_000m,
            customerPartyId = customer
        });
        Assert.Equal(HttpStatusCode.Conflict, wrongWay.StatusCode);

        var surcharge = await PostOk(tenantId, "/api/surcharges", new
        {
            code = "HANDLING",
            name = "Xếp dỡ",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 5_000m,
            publish = true,
            vatRate = 10m
        });
        var secondBuy = await RateAsync(tenantId, new { billId, rateVersionId = buyVersion, quantity = 1m });
        var withSurcharge = await GetRatingAsync(tenantId, secondBuy);
        Assert.Contains(withSurcharge.Details, d => d.SourceType == "surcharge" && d.Amount == 5_000m);
        var history = await GetRatingAsync(tenantId, buyRating);
        Assert.DoesNotContain(history.Details, d => d.SourceType == "surcharge");
        Assert.Equal(buy.TotalAmount, history.TotalAmount);
        _ = surcharge;

        await PostOk(tenantId, "/api/costs", new
        {
            billId,
            attributionType = "direct",
            amount = 1_000m,
            currencyCode = "VND",
            vendorPartyId = supplierB,
            costTypeCode = "OTHER"
        });
        var costs = await GetAsync<List<CostBody>>(tenantId, $"/api/costs?billId={billId}");
        var vendors = costs.Select(c => c.VendorPartyId).Where(id => id is not null).Distinct().ToList();
        Assert.Contains(supplierA, vendors);
        Assert.Contains(supplierB, vendors);

        var readiness = await GetAsync<ReadinessBody>(tenantId, "/api/economic-charge-types/readiness");
        Assert.True(readiness.RateVersionsMissingVat >= 1);
        Assert.Contains("không phải 0%", readiness.Note);
    }

    [Fact]
    public async Task Order_sums_each_bill_once()
    {
        var tenantId = await CreateTenantAsync();
        var chargeType = await PostOk(tenantId, "/api/economic-charge-types", new { code = "PACKAGING", name = "Đóng gói" });
        var billA = await CreateBillAsync(tenantId, "BL-OR-A");
        var billB = await CreateBillAsync(tenantId, "BL-OR-B");
        await PostOk(tenantId, "/api/costs", new { billId = billA, attributionType = "direct", amount = 40_000m, currencyCode = "VND", economicChargeTypeId = chargeType.Id, costTypeCode = "PACKAGING" });
        await PostOk(tenantId, "/api/costs", new { billId = billB, attributionType = "direct", amount = 60_000m, currencyCode = "VND", economicChargeTypeId = chargeType.Id, costTypeCode = "PACKAGING" });
        await PostOk(tenantId, "/api/revenues", new { billId = billA, amount = 50_000m, currencyCode = "VND", economicChargeTypeId = chargeType.Id, revenueTypeCode = "PACKAGING" });
        await PostOk(tenantId, "/api/revenues", new { billId = billB, amount = 40_000m, currencyCode = "VND", economicChargeTypeId = chargeType.Id, revenueTypeCode = "PACKAGING" });
        var orderId = await PutId(tenantId, "/api/orders", new { orderNo = "OR-CP-1", sourceSystem = "lcms_manual", externalId = "or-cp-1", isActive = true });
        (await SendAsync(tenantId, HttpMethod.Post, $"/api/orders/{orderId}/bills/{billA}", new { })).EnsureSuccessStatusCode();
        (await SendAsync(tenantId, HttpMethod.Post, $"/api/orders/{orderId}/bills/{billB}", new { })).EnsureSuccessStatusCode();

        var order = await GetProfitAsync(tenantId, $"/api/orders/{orderId}/charge-profitability?view=expected");
        var row = Assert.Single(order.Rows);
        Assert.Equal(100_000m, row.CostReporting);
        Assert.Equal(90_000m, row.RevenueReporting);
        var bill = await GetProfitAsync(tenantId, $"/api/bills/{billA}/charge-profitability?view=expected");
        Assert.Equal(40_000m, Assert.Single(bill.Rows).CostReporting);
    }

    [Fact]
    public async Task Invoice_vat_variance_does_not_rewrite_the_rating()
    {
        var tenantId = await CreateTenantAsync();
        await PostOk(tenantId, "/api/economic-charge-types", new { code = "PACKAGING", name = "Đóng gói" });
        var billId = await CreateBillAsync(tenantId, "BL-VAT-1");
        var card = await CreateCardAsync(tenantId, "RC-VAT", "customer");
        var version = await CreateVersionAsync(tenantId, card, 10m);
        await AddRuleAsync(tenantId, version, "PACKAGING", 150_000m);
        await PublishAsync(tenantId, version);
        var ratingId = await RateAsync(tenantId, new { billId, rateVersionId = version, quantity = 1m });
        var before = await GetRatingAsync(tenantId, ratingId);
        var detail = before.Details.Single(d => d.ComponentCode == "PACKAGING");

        var documentId = await PostId(tenantId, "/api/financial-documents", new
        {
            documentType = "invoice",
            documentNo = "INV-VAT-1",
            direction = "receivable",
            totalAmount = 162_000m,
            currencyCode = "VND",
            billId
        });
        await PostOk(tenantId, $"/api/financial-documents/{documentId}/lines", new
        {
            amount = 162_000m,
            description = "Đóng gói",
            netAmount = 150_000m,
            vatRate = 8m,
            ratingDetailId = detail.Id
        });

        var document = await GetAsync<DocumentBody>(tenantId, $"/api/financial-documents/{documentId}");
        var line = Assert.Single(document.Lines);
        Assert.Equal(162_000m, line.Amount);
        Assert.Equal(12_000m, line.VatAmount);
        Assert.Equal(-3_000m, line.VatVarianceAmount);
        var after = await GetRatingAsync(tenantId, ratingId);
        Assert.Equal(15_000m, after.Details.Single().VatAmount);
        Assert.Equal(10m, after.Details.Single().VatRate);
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Charge" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreatePartyAsync(Guid tenantId, string code, string role)
    {
        var res = await PostOk(tenantId, "/api/business-parties", new { code, name = code, roleCodes = new[] { role } });
        return res.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        var res = await PostOk(tenantId, "/api/bills", new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        return res.Id;
    }

    private async Task<Guid> CreateCardAsync(Guid tenantId, string code, string partyType)
    {
        var res = await PostOk(tenantId, "/api/rate-cards", new { code, name = code, partyType, currencyCode = "VND" });
        return res.Id;
    }

    private async Task<Guid> CreateVersionAsync(Guid tenantId, Guid cardId, decimal? vatRate)
    {
        var res = await PostOk(tenantId, $"/api/rate-cards/{cardId}/versions", new { note = "v", effectiveFrom = "2020-01-01T00:00:00Z", vatRate });
        return res.Id;
    }

    private async Task AddRuleAsync(Guid tenantId, Guid versionId, string code, decimal amount)
    {
        (await PostOk(tenantId, $"/api/rate-versions/{versionId}/rules", new { code, name = code, calcMethod = "unit_rate", unitAmount = amount, currencyCode = "VND", sortOrder = 1 })).Ensure();
    }

    private async Task PublishAsync(Guid tenantId, Guid versionId)
    {
        var res = await SendAsync(tenantId, HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", new { });
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode} publish {await res.Content.ReadAsStringAsync()}");
        }
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        var res = await SendAsync(tenantId, HttpMethod.Post, "/api/ratings", body);
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }

        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<RatingBody> GetRatingAsync(Guid tenantId, Guid id) =>
        await GetAsync<RatingBody>(tenantId, $"/api/ratings/{id}");

    private async Task<ProfitBody> GetProfitAsync(Guid tenantId, string url) =>
        await GetAsync<ProfitBody>(tenantId, url);

    private async Task<IdBody> PostOk(Guid tenantId, string url, object body)
    {
        var res = await SendAsync(tenantId, HttpMethod.Post, url, body);
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode} {url} {await res.Content.ReadAsStringAsync()}");
        }

        var text = await res.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new IdBody(Guid.Empty);
        }

        return JsonSerializer.Deserialize<IdBody>(text, Json) ?? new IdBody(Guid.Empty);
    }

    private async Task<Guid> PostId(Guid tenantId, string url, object body) => (await PostOk(tenantId, url, body)).Id;

    private async Task PutOk(Guid tenantId, string url, object body)
    {
        (await SendAsync(tenantId, HttpMethod.Put, url, body)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> PutId(Guid tenantId, string url, object body)
    {
        var res = await SendAsync(tenantId, HttpMethod.Put, url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<T> GetAsync<T>(Guid tenantId, string url)
    {
        var res = await SendAsync(tenantId, HttpMethod.Get, url, null);
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode} {url} {await res.Content.ReadAsStringAsync()}");
        }

        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<HttpResponseMessage> SendAsync(Guid tenantId, HttpMethod method, string url, object? body)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (body is not null && method != HttpMethod.Get)
        {
            req.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(req);
    }

    private sealed record IdBody(Guid Id)
    {
        public void Ensure()
        {
        }
    }

    private sealed record VersionBody(decimal? VatRate);
    private sealed record CostBody(Guid? VendorPartyId);
    private sealed record ReadinessBody(int RateVersionsMissingVat, string Note);
    private sealed record RatingBody(decimal TotalAmount, List<DetailBody> Details);
    private sealed record DetailBody(Guid Id, string ComponentCode, string? SourceType, decimal Amount, decimal? VatRate, decimal? VatAmount, decimal? GrossAmount);
    private sealed record ProfitBody(List<ProfitRow> Rows);
    private sealed record ProfitRow(string ChargeCode, decimal? CostReporting, decimal? RevenueReporting, decimal? ProfitReporting, bool NegativeFlag, List<SourceBody> Sources);
    private sealed record SourceBody(Guid? PartnerId, string SourceKind);
    private sealed record DocumentBody(List<LineBody> Lines);
    private sealed record LineBody(decimal Amount, decimal? VatAmount, decimal? VatVarianceAmount);
}
