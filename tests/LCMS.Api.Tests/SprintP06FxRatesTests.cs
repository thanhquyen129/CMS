using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class SprintP06FxRatesTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public SprintP06FxRatesTests(LcmsApiFactory factory)
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
    public async Task DatedFxRate_SetsFxRateId_And_OverridesStub()
    {
        var tenantId = await CreateTenantAsync("TN-P06-FX", "P06 FX");
        var billId = await CreateBillAsync(tenantId, "BL-P06-FX", "freight");
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);

        Guid fxId;
        using (var upsert = new HttpRequestMessage(HttpMethod.Post, "/api/fx-rates")
        {
            Content = JsonContent.Create(new
            {
                fromCurrencyCode = "USD",
                toCurrencyCode = "VND",
                rateDate = asOf.ToString("yyyy-MM-dd"),
                rate = 26000m,
                source = "manual",
                version = 1,
                note = "P06 test"
            })
        })
        {
            upsert.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(upsert);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            fxId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        using (var resolve = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/fx-rates/resolve?fromCurrencyCode=USD&toCurrencyCode=VND&asOf={asOf:yyyy-MM-dd}"))
        {
            resolve.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(resolve);
            res.EnsureSuccessStatusCode();
            var row = await res.Content.ReadFromJsonAsync<FxRateDto>(JsonOptions);
            Assert.NotNull(row);
            Assert.Equal(fxId, row!.Id);
            Assert.Equal(26000m, row.Rate);
        }

        Guid costId;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 2m,
                currencyCode = "USD",
                effectiveDate = asOf.ToString("yyyy-MM-dd"),
                costTypeCode = "FX"
            })
        })
        {
            create.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(create);
            res.EnsureSuccessStatusCode();
            costId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        using (var get = new HttpRequestMessage(HttpMethod.Get, $"/api/costs/{costId}"))
        {
            get.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(get);
            res.EnsureSuccessStatusCode();
            var cost = await res.Content.ReadFromJsonAsync<CostFxDto>(JsonOptions);
            Assert.NotNull(cost);
            Assert.Equal(52_000m, cost!.BaseAmount);
            Assert.Equal(fxId, cost.FxRateId);
        }

        // Older rate must not win when a newer RateDate ≤ asOf exists.
        var older = asOf.AddDays(-7);
        using (var olderUpsert = new HttpRequestMessage(HttpMethod.Post, "/api/fx-rates")
        {
            Content = JsonContent.Create(new
            {
                fromCurrencyCode = "USD",
                toCurrencyCode = "VND",
                rateDate = older.ToString("yyyy-MM-dd"),
                rate = 10000m,
                source = "manual",
                version = 1
            })
        })
        {
            olderUpsert.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(olderUpsert)).StatusCode);
        }

        using (var create2 = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 1m,
                currencyCode = "USD",
                effectiveDate = asOf.ToString("yyyy-MM-dd"),
                costTypeCode = "FX2"
            })
        })
        {
            create2.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(create2);
            res.EnsureSuccessStatusCode();
            var id2 = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
            using var get2 = new HttpRequestMessage(HttpMethod.Get, $"/api/costs/{id2}");
            get2.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var cost2 = await (await _client.SendAsync(get2)).Content.ReadFromJsonAsync<CostFxDto>(JsonOptions);
            Assert.Equal(26_000m, cost2!.BaseAmount);
            Assert.Equal(fxId, cost2.FxRateId);
        }
    }

    [Fact]
    public async Task MissingFxAndStub_Rejected()
    {
        var tenantId = await CreateTenantAsync("TN-P06-MISS", "P06 miss");
        var billId = await CreateBillAsync(tenantId, "BL-P06-MISS", "freight");

        using (var currency = new HttpRequestMessage(HttpMethod.Put, "/api/currencies")
        {
            Content = JsonContent.Create(new
            {
                code = "SGD",
                name = "Singapore Dollar",
                decimalPlaces = 2,
                isActive = true
            })
        })
        {
            currency.Headers.Add("X-Tenant-Id", tenantId.ToString());
            (await _client.SendAsync(currency)).EnsureSuccessStatusCode();
        }

        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 1m,
                currencyCode = "SGD",
                costTypeCode = "SGD"
            })
        };
        create.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.True(
            body.Contains("SGD", StringComparison.OrdinalIgnoreCase)
                || body.Contains("fx_rates", StringComparison.OrdinalIgnoreCase)
                || body.Contains("StubFxRatesToBase", StringComparison.OrdinalIgnoreCase)
                || body.Contains("CurrencyCode", StringComparison.OrdinalIgnoreCase),
            body);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
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
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record FxRateDto(
        Guid Id,
        string FromCurrencyCode,
        string ToCurrencyCode,
        DateOnly RateDate,
        decimal Rate,
        string Source,
        int Version,
        string? Note);

    private sealed record CostFxDto(decimal? BaseAmount, Guid? FxRateId);
}
