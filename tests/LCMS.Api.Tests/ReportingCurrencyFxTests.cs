using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.Fx;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class ReportingCurrencyFxTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public ReportingCurrencyFxTests(LcmsApiFactory factory) => _factory = factory;

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
    public void SplitReporting_PartsSumToTheSource()
    {
        var parts = FxMath.SplitReporting(100m, [1m, 1m, 1m]);
        Assert.Equal(3, parts.Length);
        Assert.Equal(100m, parts.Sum());
    }

    [Fact]
    public async Task SameCurrency_UsesIdentityRate_AndMixedBillSumsReportingOnly()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-FX-1");

        await CreateRevenueAsync(tenantId, billId, 500_000m, "VND", null, null);
        var vnd = await GetProfileAsync(tenantId, billId);
        Assert.Equal("VND", vnd.Reporting!.ReportingCurrencyCode);
        Assert.Equal(500_000m, vnd.Reporting.RevenueBestAvailable);
        Assert.True(vnd.Reporting.Complete);
        var identity = Assert.Single(vnd.Reporting.Lines, l => l.Kind == "revenue");
        Assert.Equal(1m, identity.FxRate);
        Assert.Equal("identity", identity.FxSourceType);

        using var missing = Tenant(HttpMethod.Post, "/api/revenues", tenantId);
        missing.Content = JsonContent.Create(new
        {
            billId,
            amount = 10m,
            currencyCode = "JPY",
            revenueTypeCode = "FREIGHT"
        });
        var rejected = await _client.SendAsync(missing);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        await CreateRevenueAsync(tenantId, billId, 10m, "USD", 24_000m, "Tỷ giá hợp đồng");
        var mixed = await GetProfileAsync(tenantId, billId);
        Assert.Equal(740_000m, mixed.Reporting!.RevenueBestAvailable);
        Assert.True(mixed.Reporting.Complete);
        Assert.Equal(0, mixed.Reporting.MissingFxCount);
        var usd = Assert.Single(mixed.Reporting.Lines, l => l.CurrencyCode == "USD");
        Assert.Equal(24_000m, usd.FxRate);
        Assert.Equal("override", usd.FxSourceType);
        Assert.Equal(240_000m, usd.ReportingAmount);

        using var preview = Tenant(HttpMethod.Get, "/api/fx-rates/preview?currencyCode=USD&amount=10", tenantId);
        var previewRes = await _client.SendAsync(preview);
        previewRes.EnsureSuccessStatusCode();
        var quote = (await previewRes.Content.ReadFromJsonAsync<PreviewBody>(Json))!;
        Assert.False(quote.SameCurrency);
        Assert.NotEqual(1m, quote.Rate);
        Assert.True(quote.RateAvailable);
    }

    [Fact]
    public async Task BookOverride_KeepsTheEnteredRate_NotTheProviderRate()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-FX-2");
        using (var fx = Tenant(HttpMethod.Post, "/api/fx-rates", tenantId))
        {
            fx.Content = JsonContent.Create(new
            {
                fromCurrencyCode = "USD",
                toCurrencyCode = "VND",
                rateDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                rate = 24_000m,
                source = "manual"
            });
            (await _client.SendAsync(fx)).EnsureSuccessStatusCode();
        }

        await CreateRevenueAsync(tenantId, billId, 10m, "USD", 24_100m, "Lệch hợp đồng");
        var profile = await GetProfileAsync(tenantId, billId);
        var line = Assert.Single(profile.Reporting!.Lines);
        Assert.Equal("override", line.FxSourceType);
        Assert.Equal(24_100m, line.FxRate);
        Assert.Equal(241_000m, profile.Reporting.RevenueBestAvailable);
    }

    private async Task<ProfileBody> GetProfileAsync(Guid tenantId, Guid billId)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/bills/{billId}/financial-profile", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ProfileBody>(Json))!;
    }

    private async Task CreateRevenueAsync(
        Guid tenantId,
        Guid billId,
        decimal amount,
        string currency,
        decimal? fxRate,
        string? reason)
    {
        using var req = Tenant(HttpMethod.Post, "/api/revenues", tenantId);
        req.Content = JsonContent.Create(new
        {
            billId,
            amount,
            currencyCode = currency,
            revenueTypeCode = "FREIGHT",
            fxRate,
            fxOverrideReason = reason
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
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
        var response = await _client.PostAsJsonAsync(
            "/api/tenants",
            new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "FX" });
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
    private sealed record ProfileBody(ReportingBody? Reporting);
    private sealed record ReportingBody(
        string ReportingCurrencyCode,
        decimal? RevenueBestAvailable,
        int MissingFxCount,
        bool Complete,
        List<LineBody> Lines);
    private sealed record LineBody(
        string Kind,
        string CurrencyCode,
        decimal? FxRate,
        string? FxSourceType,
        decimal? ReportingAmount);
    private sealed record PreviewBody(bool SameCurrency, decimal? Rate, bool RateAvailable);
}
