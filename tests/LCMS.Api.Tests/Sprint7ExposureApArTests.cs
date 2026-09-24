using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint7ExposureApArTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint7ExposureApArTests(LcmsApiFactory factory)
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
    public async Task Exposure_IsSeparateFrom_RecognizedAp_AndRecognizeDoesNotCreateCostOrRevenue()
    {
        var tenantId = await CreateTenantAsync("TN-E09-SEP", "Exposure Separate");
        var billId = await CreateBillAsync(tenantId, "BL-E09-1", "freight");

        Assert.Empty(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));

        var exposureId = await CreatePayableExposureAsync(tenantId, billId, 1000m);
        var exposure = await GetPayableExposureAsync(tenantId, exposureId);
        Assert.Equal("BL-E09-1", exposure.BillNo);
        Assert.Equal("open", exposure.Status);
        Assert.Equal(0m, exposure.RecognizedAmount);
        Assert.Equal(1000m, exposure.OpenAmount);
        Assert.Empty(await ListAccountsPayableAsync(tenantId));

        var apId = await RecognizePayableAsync(tenantId, exposureId, 400m);
        Assert.NotEqual(exposureId, apId);

        var afterPartial = await GetPayableExposureAsync(tenantId, exposureId);
        Assert.Equal("partially_recognized", afterPartial.Status);
        Assert.Equal(400m, afterPartial.RecognizedAmount);
        Assert.Equal(600m, afterPartial.OpenAmount);

        var ap = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal("BL-E09-1", ap.BillNo);
        Assert.Equal(exposureId, ap.PayableExposureId);
        Assert.Equal(400m, ap.RecognizedAmount);
        Assert.Equal(0m, ap.AdjustmentAmount);
        Assert.Equal(0m, ap.FinalizedSettledAmount);
        Assert.Equal(400m, ap.Outstanding);
        Assert.Equal("open", ap.SettlementStatus);

        // Still no Cost/Revenue invented (C-003 / C-004)
        Assert.Empty(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));

        // Exposure row still exists as its own record — not collapsed into AP status
        Assert.NotEqual(ap.Id, afterPartial.Id);
        Assert.Equal("partially_recognized", afterPartial.Status);
    }

    [Fact]
    public async Task Outstanding_IsDerived_NotUserEntered_AndPartialRecognitionAllowed()
    {
        var tenantId = await CreateTenantAsync("TN-E09-OUT", "Outstanding Derived");
        var billId = await CreateBillAsync(tenantId, "BL-E09-2", "freight");

        var exposureId = await CreatePayableExposureAsync(tenantId, billId, 1000m);
        var ap1 = await RecognizePayableAsync(tenantId, exposureId, 300m);
        var ap2 = await RecognizePayableAsync(tenantId, exposureId, 200m);

        var exposure = await GetPayableExposureAsync(tenantId, exposureId);
        Assert.Equal("partially_recognized", exposure.Status);
        Assert.Equal(500m, exposure.RecognizedAmount);
        Assert.Equal(500m, exposure.OpenAmount);

        var first = await GetAccountsPayableAsync(tenantId, ap1);
        Assert.Equal(300m, first.Outstanding);

        using (var adjust = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-payable/{ap1}/adjust")
        {
            Content = JsonContent.Create(new { deltaAmount = 50m, reason = "Điều chỉnh phí phát sinh" })
        })
        {
            adjust.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(adjust)).StatusCode);
        }

        var afterAdjust = await GetAccountsPayableAsync(tenantId, ap1);
        // C-015: outstanding = recognized + adjustment - finalized settlement (0)
        Assert.Equal(300m, afterAdjust.RecognizedAmount);
        Assert.Equal(50m, afterAdjust.AdjustmentAmount);
        Assert.Equal(0m, afterAdjust.FinalizedSettledAmount);
        Assert.Equal(350m, afterAdjust.Outstanding);

        var second = await GetAccountsPayableAsync(tenantId, ap2);
        Assert.Equal(200m, second.Outstanding);

        // Over-recognize rejected
        using (var over = new HttpRequestMessage(HttpMethod.Post, $"/api/payable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount = 600m })
        })
        {
            over.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var overResp = await _client.SendAsync(over);
            Assert.Equal(HttpStatusCode.Conflict, overResp.StatusCode);
            var err = await overResp.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("vượt phần còn lại", err!.Message);
        }

        // Finish recognition
        await RecognizePayableAsync(tenantId, exposureId, 500m);
        var full = await GetPayableExposureAsync(tenantId, exposureId);
        Assert.Equal("fully_recognized", full.Status);
        Assert.Equal(0m, full.OpenAmount);
    }

    [Fact]
    public async Task ReceivableExposure_Recognize_AndCrossTenantIsolation()
    {
        var tenantA = await CreateTenantAsync("TN-E09-A", "Tenant A");
        var tenantB = await CreateTenantAsync("TN-E09-B", "Tenant B");
        var billA = await CreateBillAsync(tenantA, "BL-A", "freight");
        var billB = await CreateBillAsync(tenantB, "BL-B", "freight");

        var expA = await CreateReceivableExposureAsync(tenantA, billA, 800m);
        var expB = await CreateReceivableExposureAsync(tenantB, billB, 900m);
        var arA = await RecognizeReceivableAsync(tenantA, expA, 800m);

        using (var req = new HttpRequestMessage(HttpMethod.Get, $"/api/receivable-exposures/{expA}"))
        {
            req.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(req)).StatusCode);
        }

        using (var req = new HttpRequestMessage(HttpMethod.Get, $"/api/accounts-receivable/{arA}"))
        {
            req.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(req)).StatusCode);
        }

        var listB = await ListReceivableExposuresAsync(tenantB);
        Assert.Single(listB);
        Assert.Equal(expB, listB[0].Id);

        var ar = await GetAccountsReceivableAsync(tenantA, arA);
        Assert.Equal(800m, ar.Outstanding);
        Assert.Equal(0m, ar.FinalizedSettledAmount);
        Assert.Empty(await ListCostsAsync(tenantA));
        Assert.Empty(await ListRevenuesAsync(tenantA));

        var exposure = await GetReceivableExposureAsync(tenantA, expA);
        Assert.Equal("fully_recognized", exposure.Status);
        Assert.NotEqual(ar.Id, exposure.Id);
    }

    [Fact]
    public async Task AdjustReplay_KeepsTheSameDelta()
    {
        var tenantId = await CreateTenantAsync("TN-E09-ADJ", "Adjust Replay");
        var billId = await CreateBillAsync(tenantId, "BL-E09-ADJ", "freight");
        var exposureId = await CreatePayableExposureAsync(tenantId, billId, 1000m);
        var apId = await RecognizePayableAsync(tenantId, exposureId, 300m);

        using (var first = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-payable/{apId}/adjust")
        {
            Content = JsonContent.Create(new { deltaAmount = 25m, reason = "Phí phát sinh" })
        })
        {
            first.Headers.Add("X-Tenant-Id", tenantId.ToString());
            first.Headers.TryAddWithoutValidation("Idempotency-Key", "adj-replay-1");
            Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(first)).StatusCode);
        }

        using (var again = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-payable/{apId}/adjust")
        {
            Content = JsonContent.Create(new { deltaAmount = 999m, reason = "Không cộng lần hai" })
        })
        {
            again.Headers.Add("X-Tenant-Id", tenantId.ToString());
            again.Headers.TryAddWithoutValidation("Idempotency-Key", "adj-replay-1");
            Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(again)).StatusCode);
        }

        var ap = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(25m, ap.AdjustmentAmount);
        Assert.Equal(325m, ap.Outstanding);
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
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> CreatePayableExposureAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payable-exposures")
        {
            Content = JsonContent.Create(new { amount, currencyCode = "VND", billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> CreateReceivableExposureAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/receivable-exposures")
        {
            Content = JsonContent.Create(new { amount, currencyCode = "VND", billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> RecognizePayableAsync(Guid tenantId, Guid exposureId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> RecognizeReceivableAsync(Guid tenantId, Guid exposureId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/receivable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<PayableExposureDto> GetPayableExposureAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/payable-exposures/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PayableExposureDto>(JsonOptions))!;
    }

    private async Task<ReceivableExposureDto> GetReceivableExposureAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/receivable-exposures/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReceivableExposureDto>(JsonOptions))!;
    }

    private async Task<List<ReceivableExposureDto>> ListReceivableExposuresAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/receivable-exposures");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ReceivableExposureDto>>(JsonOptions))!;
    }

    private async Task<AccountsPayableDto> GetAccountsPayableAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/accounts-payable/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountsPayableDto>(JsonOptions))!;
    }

    private async Task<AccountsReceivableDto> GetAccountsReceivableAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/accounts-receivable/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountsReceivableDto>(JsonOptions))!;
    }

    private async Task<List<AccountsPayableDto>> ListAccountsPayableAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/accounts-payable");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AccountsPayableDto>>(JsonOptions))!;
    }

    private async Task<List<JsonElement>> ListCostsAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/costs");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions))!;
    }

    private async Task<List<JsonElement>> ListRevenuesAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/revenues");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);

    private sealed record PayableExposureDto(
        Guid Id,
        decimal Amount,
        decimal RecognizedAmount,
        decimal OpenAmount,
        string CurrencyCode,
        string Status,
        DateOnly EffectiveDate,
        DateOnly? DueDate,
        Guid? BillId,
        Guid? CounterpartyId,
        Guid? CostId,
        Guid? FinancialDocumentId,
        string? Notes,
        string RecordStatus,
        string? BillNo = null);

    private sealed record ReceivableExposureDto(
        Guid Id,
        decimal Amount,
        decimal RecognizedAmount,
        decimal OpenAmount,
        string CurrencyCode,
        string Status,
        DateOnly EffectiveDate,
        DateOnly? DueDate,
        Guid? BillId,
        Guid? CounterpartyId,
        Guid? RevenueId,
        Guid? FinancialDocumentId,
        string? Notes,
        string RecordStatus);

    private sealed record AccountsPayableDto(
        Guid Id,
        Guid PayableExposureId,
        decimal RecognizedAmount,
        decimal AdjustmentAmount,
        decimal FinalizedSettledAmount,
        decimal Outstanding,
        string CurrencyCode,
        DateOnly? DueDate,
        string SettlementStatus,
        Guid? BillId,
        Guid? CounterpartyId,
        DateTimeOffset RecognizedAt,
        string? Notes,
        string RecordStatus,
        string? BillNo = null);

    private sealed record AccountsReceivableDto(
        Guid Id,
        Guid ReceivableExposureId,
        decimal RecognizedAmount,
        decimal AdjustmentAmount,
        decimal FinalizedSettledAmount,
        decimal Outstanding,
        string CurrencyCode,
        DateOnly? DueDate,
        string SettlementStatus,
        Guid? BillId,
        Guid? CounterpartyId,
        DateTimeOffset RecognizedAt,
        string? Notes,
        string RecordStatus);
}
