using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class RevenuePackageTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public RevenuePackageTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Mapping_ConservesRemainder_AndReplacesParentOnEachBill()
    {
        var tenantId = await CreateTenantAsync();
        var a = await CreateBillAsync(tenantId, "BL-E-A");
        var b = await CreateBillAsync(tenantId, "BL-E-B");
        var c = await CreateBillAsync(tenantId, "BL-E-C");
        var revenueId = await CreateRevenueAsync(tenantId, a, 100m, null);

        using var zero = Tenant(HttpMethod.Post, $"/api/revenues/{revenueId}/mappings", tenantId);
        zero.Content = JsonContent.Create(new
        {
            allocationBasis = "chargeable",
            details = new[] { new { billId = a }, new { billId = b } }
        });
        var blocked = await _client.SendAsync(zero);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("ZERO_ALLOCATION_BASIS", (await blocked.Content.ReadFromJsonAsync<Err>(Json))!.Message);

        var mappingId = await MapEqual(tenantId, revenueId, new[] { a, b, c });
        await PostNoContent(tenantId, $"/api/revenue-mappings/{mappingId}/finalize");

        var profits = new[] { a, b, c };
        decimal sum = 0;
        var sawRemainder = false;
        foreach (var billId in profits)
        {
            var view = await GetProfit(tenantId, billId, "expected");
            var vnd = Assert.Single(view.ByCurrency);
            sum += vnd.RevenueAmount;
            if (vnd.RevenueAmount != decimal.Round(100m / 3m, 4, MidpointRounding.AwayFromZero))
            {
                sawRemainder = true;
            }
        }

        Assert.Equal(100m, sum);
        Assert.True(sawRemainder);
        var anchor = await GetProfit(tenantId, a, "expected");
        Assert.NotEqual(100m, Assert.Single(anchor.ByCurrency).RevenueAmount);

        var again = await MapEqual(tenantId, revenueId, new[] { a, b });
        await PostNoContent(tenantId, $"/api/revenue-mappings/{again}/finalize");
        var after = await GetProfit(tenantId, c, "expected");
        Assert.Empty(after.ByCurrency);
    }

    [Fact]
    public async Task FinalizeMapping_StaleIfMatch_LeavesTheSplitOffTheOtherBill()
    {
        var tenantId = await CreateTenantAsync();
        var a = await CreateBillAsync(tenantId, "BL-E-VER-A");
        var b = await CreateBillAsync(tenantId, "BL-E-VER-B");
        var revenueId = await CreateRevenueAsync(tenantId, a, 100m, null);
        var mappingId = await MapEqual(tenantId, revenueId, new[] { a, b });

        using var fin = Tenant(HttpMethod.Post, $"/api/revenue-mappings/{mappingId}/finalize", tenantId);
        fin.Headers.TryAddWithoutValidation("If-Match", "not-a-version");
        var blocked = await _client.SendAsync(fin);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<Err>(Json);
        Assert.Equal("concurrency_conflict", err!.Code);

        var other = await GetProfit(tenantId, b, "expected");
        Assert.Empty(other.ByCurrency);
    }

    [Fact]
    public async Task CancelMapping_StaleIfMatch_StillAllowsFinalize()
    {
        var tenantId = await CreateTenantAsync();
        var a = await CreateBillAsync(tenantId, "BL-E-CAN-A");
        var b = await CreateBillAsync(tenantId, "BL-E-CAN-B");
        var revenueId = await CreateRevenueAsync(tenantId, a, 40m, null);
        var mappingId = await MapEqual(tenantId, revenueId, new[] { a, b });

        using var cancel = Tenant(HttpMethod.Post, $"/api/revenue-mappings/{mappingId}/cancel", tenantId);
        cancel.Headers.TryAddWithoutValidation("If-Match", "not-a-version");
        var blocked = await _client.SendAsync(cancel);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("concurrency_conflict", (await blocked.Content.ReadFromJsonAsync<Err>(Json))!.Code);

        await PostNoContent(tenantId, $"/api/revenue-mappings/{mappingId}/finalize");
        var split = await GetProfit(tenantId, b, "expected");
        Assert.Equal(20m, Assert.Single(split.ByCurrency).RevenueAmount);
    }

    [Fact]
    public async Task MarginIsNullWhenRevenueIsZero_AndFxDoesNotAddRawCurrencies()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-E-M");
        using (var cost = Tenant(HttpMethod.Post, "/api/costs", tenantId))
        {
            cost.Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 10m,
                currencyCode = "VND",
                costTypeCode = "FREIGHT"
            });
            (await _client.SendAsync(cost)).EnsureSuccessStatusCode();
        }

        var empty = await GetProfit(tenantId, billId, "expected");
        var bucket = Assert.Single(empty.ByCurrency);
        Assert.Equal(-10m, bucket.ProfitAmount);
        Assert.Null(bucket.MarginRate);

        await CreateRevenueAsync(tenantId, billId, 100m, null);
        await CreateRevenueAsync(tenantId, billId, 2m, "USD");
        var mixed = await GetProfit(tenantId, billId, "expected", "VND");
        Assert.True(mixed.HasMixedCurrencies);
        Assert.Null(mixed.ReportingProfit);
        Assert.Contains("USD", mixed.UnconvertedCurrencies!);

        using var fx = Tenant(HttpMethod.Post, "/api/fx-rates", tenantId);
        fx.Content = JsonContent.Create(new
        {
            fromCurrencyCode = "USD",
            toCurrencyCode = "VND",
            rateDate = "2026-09-22",
            rate = 25000m,
            source = "manual"
        });
        (await _client.SendAsync(fx)).EnsureSuccessStatusCode();
        var converted = await GetProfit(tenantId, billId, "expected", "VND");
        Assert.Equal(100m + 50_000m, converted.ReportingRevenue);
        Assert.NotNull(converted.FxTrace);
        Assert.Contains(converted.FxTrace!, t => t.CurrencyCode == "USD" && t.Rate == 25000m);
    }

    [Fact]
    public async Task ExternalOwnerCannotOverwriteActual_AndGroupsKeepOneCurrency()
    {
        var tenantId = await CreateTenantAsync();
        var air = await CreateBillAsync(tenantId, "BL-E-AIR");
        var sea = await CreateBillAsync(tenantId, "BL-E-SEA");
        await PatchMode(tenantId, air, "air");
        await PatchMode(tenantId, sea, "sea");
        var revenueId = await CreateRevenueAsync(tenantId, air, 80m, null, "ops_tms");
        await CreateRevenueAsync(tenantId, sea, 20m, null);
        using (var confirm = Tenant(HttpMethod.Post, $"/api/revenues/{revenueId}/confirm", tenantId))
        {
            confirm.Content = JsonContent.Create(new { confirmedAmount = 80m });
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(confirm)).StatusCode);
        }

        using var denied = Tenant(HttpMethod.Post, $"/api/revenues/{revenueId}/actualize", tenantId);
        denied.Content = JsonContent.Create(new { actualAmount = 90m });
        var deniedRes = await _client.SendAsync(denied);
        Assert.Equal(HttpStatusCode.Conflict, deniedRes.StatusCode);
        Assert.Contains("RV-06", (await deniedRes.Content.ReadFromJsonAsync<Err>(Json))!.Message);

        using var allowed = Tenant(HttpMethod.Post, $"/api/revenues/{revenueId}/actualize", tenantId);
        allowed.Content = JsonContent.Create(new { actualAmount = 90m, overrideReason = "Nguồn ngoài xác nhận lại" });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(allowed)).StatusCode);

        using var groups = Tenant(HttpMethod.Get, "/api/profitability/groups?groupBy=mode&view=expected", tenantId);
        var rows = await (await _client.SendAsync(groups)).Content.ReadFromJsonAsync<List<GroupRow>>(Json);
        Assert.Contains(rows!, g => g.Label == "air · VND" && g.RevenueAmount == 80m);
        Assert.Contains(rows!, g => g.Label == "sea · VND" && g.RevenueAmount == 20m);
    }

    private async Task PatchMode(Guid tenantId, Guid billId, string mode)
    {
        using var req = Tenant(HttpMethod.Patch, $"/api/bills/{billId}/context", tenantId);
        req.Content = JsonContent.Create(new { transportMode = mode });
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> MapEqual(Guid tenantId, Guid revenueId, Guid[] bills)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/revenues/{revenueId}/mappings", tenantId);
        req.Content = JsonContent.Create(new
        {
            allocationBasis = "equal",
            details = bills.Select(id => new { billId = id }).ToArray()
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateRevenueAsync(Guid tenantId, Guid billId, decimal amount, string? currency, string? owner = null)
    {
        using var req = Tenant(HttpMethod.Post, "/api/revenues", tenantId);
        req.Content = JsonContent.Create(new
        {
            billId,
            amount,
            currencyCode = currency ?? "VND",
            revenueTypeCode = "FREIGHT",
            actualRevenueOwner = owner
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<ProfitBody> GetProfit(Guid tenantId, Guid billId, string view, string? reporting = null)
    {
        var url = $"/api/bills/{billId}/profitability?view={view}";
        if (reporting is not null)
        {
            url += "&reportingCurrency=" + reporting;
        }

        using var req = Tenant(HttpMethod.Get, url, tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ProfitBody>(Json))!;
    }

    private async Task PostNoContent(Guid tenantId, string url)
    {
        using var req = Tenant(HttpMethod.Post, url, tenantId);
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
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
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Rev" });
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
    private sealed record Err(string? Code, string Message);
    private sealed record ProfitBody(
        bool HasMixedCurrencies,
        List<Bucket> ByCurrency,
        decimal? ReportingRevenue,
        decimal? ReportingProfit,
        List<string>? UnconvertedCurrencies,
        List<FxRow>? FxTrace);
    private sealed record Bucket(string CurrencyCode, decimal RevenueAmount, decimal ProfitAmount, decimal? MarginRate);
    private sealed record FxRow(string CurrencyCode, decimal Rate);
    private sealed record GroupRow(string Label, decimal RevenueAmount);
}
