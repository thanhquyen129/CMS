using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

/// <summary>
/// Pass 2 Sprint 7 FULL — multi-recognize, aging buckets, document→exposure link, outstanding vs settlement.
/// </summary>
[Collection("Api")]
public sealed class Sprint7FullExposureApArTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint7FullExposureApArTests(LcmsApiFactory factory)
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
    public async Task MultiRecognize_TracksRecognizedAmount_RejectsOverRecognize_AndListsSlices()
    {
        var tenantId = await CreateTenantAsync("TN-S7F-REC", "S7 Full Recognize");
        var billId = await CreateBillAsync(tenantId, "BL-S7F-1", "freight");

        var exposureId = await CreatePayableExposureAsync(tenantId, billId, 1000m, dueDate: null);
        var ap1 = await RecognizePayableAsync(tenantId, exposureId, 250m);
        var ap2 = await RecognizePayableAsync(tenantId, exposureId, 350m);

        var exposure = await GetPayableExposureAsync(tenantId, exposureId);
        Assert.Equal("partially_recognized", exposure.Status);
        Assert.Equal(600m, exposure.RecognizedAmount);
        Assert.Equal(400m, exposure.OpenAmount);
        Assert.Equal(2, exposure.Recognitions.Count);
        Assert.Contains(exposure.Recognitions, r => r.Id == ap1 && r.RecognizedAmount == 250m);
        Assert.Contains(exposure.Recognitions, r => r.Id == ap2 && r.RecognizedAmount == 350m);

        using (var over = new HttpRequestMessage(HttpMethod.Post, $"/api/payable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount = 401m })
        })
        {
            over.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var overResp = await _client.SendAsync(over);
            Assert.Equal(HttpStatusCode.Conflict, overResp.StatusCode);
            var err = await overResp.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("vượt phần còn lại", err!.Message);
        }

        await RecognizePayableAsync(tenantId, exposureId, 400m);
        var full = await GetPayableExposureAsync(tenantId, exposureId);
        Assert.Equal("fully_recognized", full.Status);
        Assert.Equal(1000m, full.RecognizedAmount);
        Assert.Equal(0m, full.OpenAmount);
        Assert.Equal(3, full.Recognitions.Count);

        using (var again = new HttpRequestMessage(HttpMethod.Post, $"/api/payable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount = 1m })
        })
        {
            again.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var againResp = await _client.SendAsync(again);
            Assert.Equal(HttpStatusCode.Conflict, againResp.StatusCode);
        }

        Assert.Empty(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));
    }

    [Fact]
    public async Task AgingBuckets_Query_ClassifiesByDueDate_AndOutstandingRespectsFinalizedSettlement()
    {
        var tenantId = await CreateTenantAsync("TN-S7F-AGE", "S7 Full Aging");
        var billId = await CreateBillAsync(tenantId, "BL-S7F-AGE", "freight");
        var asOf = new DateOnly(2026, 9, 12);

        // current (due today)
        var expCurrent = await CreatePayableExposureAsync(tenantId, billId, 100m, asOf);
        var apCurrent = await RecognizePayableAsync(tenantId, expCurrent, 100m, asOf);

        // 1_30
        var exp30 = await CreatePayableExposureAsync(tenantId, billId, 200m, asOf.AddDays(-15));
        var ap30 = await RecognizePayableAsync(tenantId, exp30, 200m, asOf.AddDays(-15));

        // 31_60
        var exp60 = await CreatePayableExposureAsync(tenantId, billId, 300m, asOf.AddDays(-45));
        var ap60 = await RecognizePayableAsync(tenantId, exp60, 300m, asOf.AddDays(-45));

        // 61_90
        var exp90 = await CreatePayableExposureAsync(tenantId, billId, 400m, asOf.AddDays(-75));
        var ap90 = await RecognizePayableAsync(tenantId, exp90, 400m, asOf.AddDays(-75));

        // 90_plus
        var expPlus = await CreatePayableExposureAsync(tenantId, billId, 500m, asOf.AddDays(-120));
        var apPlus = await RecognizePayableAsync(tenantId, expPlus, 500m, asOf.AddDays(-120));

        // no_due_date
        var expNone = await CreatePayableExposureAsync(tenantId, billId, 50m, null);
        var apNone = await RecognizePayableAsync(tenantId, expNone, 50m, null);

        var apCurrentDto = await GetAccountsPayableAsync(tenantId, apCurrent, asOf);
        Assert.Equal("current", apCurrentDto.AgingBucket);
        Assert.Equal(0, apCurrentDto.DaysPastDue);

        var ap30Dto = await GetAccountsPayableAsync(tenantId, ap30, asOf);
        Assert.Equal("1_30", ap30Dto.AgingBucket);
        Assert.Equal(15, ap30Dto.DaysPastDue);

        var ap60Dto = await GetAccountsPayableAsync(tenantId, ap60, asOf);
        Assert.Equal("31_60", ap60Dto.AgingBucket);

        var ap90Dto = await GetAccountsPayableAsync(tenantId, ap90, asOf);
        Assert.Equal("61_90", ap90Dto.AgingBucket);

        var apPlusDto = await GetAccountsPayableAsync(tenantId, apPlus, asOf);
        Assert.Equal("90_plus", apPlusDto.AgingBucket);
        Assert.Equal(120, apPlusDto.DaysPastDue);

        var apNoneDto = await GetAccountsPayableAsync(tenantId, apNone, asOf);
        Assert.Equal("no_due_date", apNoneDto.AgingBucket);
        Assert.Null(apNoneDto.DaysPastDue);

        // Settle 100 of the 1_30 row — draft must NOT change outstanding (AC-007)
        var paymentId = await CreatePaymentAsync(tenantId, billId, 100m);
        var allocId = await AllocatePaymentAsync(tenantId, paymentId, ap30, 100m);
        var afterDraft = await GetAccountsPayableAsync(tenantId, ap30, asOf);
        Assert.Equal(200m, afterDraft.Outstanding);
        Assert.Equal(0m, afterDraft.FinalizedSettledAmount);

        await FinalizePaymentAllocationAsync(tenantId, allocId);
        var afterFinalize = await GetAccountsPayableAsync(tenantId, ap30, asOf);
        Assert.Equal(100m, afterFinalize.FinalizedSettledAmount);
        Assert.Equal(100m, afterFinalize.Outstanding);
        Assert.Equal("partially_settled", afterFinalize.SettlementStatus);
        Assert.Equal("1_30", afterFinalize.AgingBucket);

        using var agingReq = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/accounts-payable/aging?asOf={asOf:yyyy-MM-dd}");
        agingReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var agingResp = await _client.SendAsync(agingReq);
        agingResp.EnsureSuccessStatusCode();
        var aging = (await agingResp.Content.ReadFromJsonAsync<AgingReportDto>(JsonOptions))!;

        Assert.Equal(asOf, aging.AsOf);
        Assert.Equal(6, aging.PayableItems!.Count);
        Assert.Equal(100m, BucketOutstanding(aging, "current"));
        Assert.Equal(100m, BucketOutstanding(aging, "1_30")); // after settlement
        Assert.Equal(300m, BucketOutstanding(aging, "31_60"));
        Assert.Equal(400m, BucketOutstanding(aging, "61_90"));
        Assert.Equal(500m, BucketOutstanding(aging, "90_plus"));
        Assert.Equal(50m, BucketOutstanding(aging, "no_due_date"));

        Assert.Empty(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));
    }

    [Fact]
    public async Task DocumentToExposureLink_DoesNotCreateCostOrRevenue_AndWrongDirectionRejected()
    {
        var tenantId = await CreateTenantAsync("TN-S7F-DOC", "S7 Full Doc Link");
        var billId = await CreateBillAsync(tenantId, "BL-S7F-DOC", "freight");

        var payableDoc = await ReceiveDocumentAsync(tenantId, billId, "INV-PAY-1", 800m, "payable");
        var receivableDoc = await ReceiveDocumentAsync(tenantId, billId, "INV-AR-1", 900m, "receivable");

        Assert.Empty(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));

        // Create with document link
        Guid exposureFromCreate;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/payable-exposures")
        {
            Content = JsonContent.Create(new
            {
                amount = 800m,
                currencyCode = "VND",
                billId,
                financialDocumentId = payableDoc
            })
        })
        {
            create.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var resp = await _client.SendAsync(create);
            resp.EnsureSuccessStatusCode();
            exposureFromCreate = (await resp.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        var linkedCreate = await GetPayableExposureAsync(tenantId, exposureFromCreate);
        Assert.Equal(payableDoc, linkedCreate.FinancialDocumentId);

        // Link after create
        var exposureLater = await CreatePayableExposureAsync(tenantId, billId, 500m, null);
        using (var link = new HttpRequestMessage(HttpMethod.Post, $"/api/payable-exposures/{exposureLater}/link-document")
        {
            Content = JsonContent.Create(new { financialDocumentId = payableDoc })
        })
        {
            link.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(link)).StatusCode);
        }

        var afterLink = await GetPayableExposureAsync(tenantId, exposureLater);
        Assert.Equal(payableDoc, afterLink.FinancialDocumentId);

        // Wrong direction rejected
        using (var bad = new HttpRequestMessage(HttpMethod.Post, $"/api/payable-exposures/{exposureLater}/link-document")
        {
            Content = JsonContent.Create(new { financialDocumentId = receivableDoc })
        })
        {
            bad.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var badResp = await _client.SendAsync(bad);
            Assert.Equal(HttpStatusCode.Conflict, badResp.StatusCode);
            var err = await badResp.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("phải thu", err!.Message);
        }

        // Receivable side
        var arExposure = await CreateReceivableExposureAsync(tenantId, billId, 900m);
        using (var linkAr = new HttpRequestMessage(HttpMethod.Post, $"/api/receivable-exposures/{arExposure}/link-document")
        {
            Content = JsonContent.Create(new { financialDocumentId = receivableDoc })
        })
        {
            linkAr.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(linkAr)).StatusCode);
        }

        var arLinked = await GetReceivableExposureAsync(tenantId, arExposure);
        Assert.Equal(receivableDoc, arLinked.FinancialDocumentId);

        Assert.Empty(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));
    }

    private static decimal BucketOutstanding(AgingReportDto report, string bucket) =>
        report.Buckets.Single(b => b.Bucket == bucket).Outstanding;

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

    private async Task<Guid> CreatePayableExposureAsync(
        Guid tenantId,
        Guid billId,
        decimal amount,
        DateOnly? dueDate)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payable-exposures")
        {
            Content = JsonContent.Create(new
            {
                amount,
                currencyCode = "VND",
                billId,
                dueDate
            })
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

    private async Task<Guid> RecognizePayableAsync(
        Guid tenantId,
        Guid exposureId,
        decimal amount,
        DateOnly? dueDate = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount, dueDate })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> ReceiveDocumentAsync(
        Guid tenantId,
        Guid billId,
        string documentNo,
        decimal totalAmount,
        string direction)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/financial-documents")
        {
            Content = JsonContent.Create(new
            {
                documentType = "invoice",
                documentNo,
                direction,
                totalAmount,
                currencyCode = "VND",
                billId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> CreatePaymentAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new { amount, currencyCode = "VND", billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> AllocatePaymentAsync(
        Guid tenantId,
        Guid paymentId,
        Guid accountsPayableId,
        decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/{paymentId}/allocations")
        {
            Content = JsonContent.Create(new { accountsPayableId, amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task FinalizePaymentAllocationAsync(Guid tenantId, Guid allocationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payment-allocations/{allocationId}/finalize");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
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

    private async Task<AccountsPayableDto> GetAccountsPayableAsync(
        Guid tenantId,
        Guid id,
        DateOnly? asOf = null)
    {
        var url = asOf.HasValue
            ? $"/api/accounts-payable/{id}?asOf={asOf:yyyy-MM-dd}"
            : $"/api/accounts-payable/{id}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountsPayableDto>(JsonOptions))!;
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

    private sealed record ExposureRecognitionDto(
        Guid Id,
        decimal RecognizedAmount,
        decimal Outstanding,
        decimal FinalizedSettledAmount,
        string SettlementStatus,
        DateOnly? DueDate,
        DateTimeOffset RecognizedAt);

    private sealed record PayableExposureDto(
        Guid Id,
        decimal Amount,
        decimal RecognizedAmount,
        decimal OpenAmount,
        string CurrencyCode,
        string Status,
        Guid? FinancialDocumentId,
        IReadOnlyList<ExposureRecognitionDto> Recognitions);

    private sealed record ReceivableExposureDto(
        Guid Id,
        decimal Amount,
        decimal RecognizedAmount,
        Guid? FinancialDocumentId);

    private sealed record AccountsPayableDto(
        Guid Id,
        decimal RecognizedAmount,
        decimal AdjustmentAmount,
        decimal FinalizedSettledAmount,
        decimal Outstanding,
        string SettlementStatus,
        DateOnly? DueDate,
        int? DaysPastDue,
        string AgingBucket);

    private sealed record AgingBucketSummaryDto(string Bucket, int Count, decimal Outstanding);

    private sealed record AgingReportDto(
        DateOnly AsOf,
        IReadOnlyList<AgingBucketSummaryDto> Buckets,
        IReadOnlyList<AccountsPayableDto>? PayableItems,
        IReadOnlyList<AccountsPayableDto>? ReceivableItems);
}
