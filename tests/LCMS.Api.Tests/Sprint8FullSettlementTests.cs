using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint8FullSettlementTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint8FullSettlementTests(LcmsApiFactory factory)
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
    public async Task Unapplied_MultiAllocate_OverReject_And_OutstandingAfterSettle()
    {
        var tenantId = await CreateTenantAsync("TN-E10F-MULTI", "Settlement FULL Multi");
        var billId = await CreateBillAsync(tenantId, "BL-E10F-1", "freight");

        var ap1 = await RecognizePayableAsync(tenantId, await CreatePayableExposureAsync(tenantId, billId, 400m), 400m);
        var ap2 = await RecognizePayableAsync(tenantId, await CreatePayableExposureAsync(tenantId, billId, 300m), 300m);

        var paymentId = await CreatePaymentAsync(tenantId, billId, 500m, "VND");
        var payment = await GetPaymentAsync(tenantId, paymentId);
        Assert.Equal(500m, payment.UnappliedAmount);
        Assert.Equal(500m, payment.AvailableToAllocate);
        Assert.Equal(500m, payment.BaseAmount);
        Assert.Null(payment.FxRateId);

        var alloc1 = await AllocatePaymentAsync(tenantId, paymentId, ap1, 250m);
        var afterDraft = await GetPaymentAsync(tenantId, paymentId);
        Assert.Equal(500m, afterDraft.UnappliedAmount); // draft not applied
        Assert.Equal(250m, afterDraft.AvailableToAllocate);
        Assert.Equal(250m, afterDraft.AllocatedAmount);

        // Multi-AP second allocation from remaining available
        var alloc2 = await AllocatePaymentAsync(tenantId, paymentId, ap2, 200m);
        var afterMulti = await GetPaymentAsync(tenantId, paymentId);
        Assert.Equal(50m, afterMulti.AvailableToAllocate);
        Assert.Equal(450m, afterMulti.AllocatedAmount);
        Assert.Equal(2, afterMulti.Allocations.Count);

        // Over remaining payment available → C-008
        using (var overPay = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/{paymentId}/allocations")
        {
            Content = JsonContent.Create(new { accountsPayableId = ap1, amount = 51m })
        })
        {
            overPay.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var response = await _client.SendAsync(overPay);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var err = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("C-008", err!.Message);
        }

        await FinalizePaymentAllocationAsync(tenantId, alloc1);
        await FinalizePaymentAllocationAsync(tenantId, alloc2);

        var paymentApplied = await GetPaymentAsync(tenantId, paymentId);
        Assert.Equal(450m, paymentApplied.AppliedAmount);
        Assert.Equal(50m, paymentApplied.UnappliedAmount);
        Assert.Equal(50m, paymentApplied.AvailableToAllocate);

        var ap1After = await GetAccountsPayableAsync(tenantId, ap1);
        Assert.Equal(250m, ap1After.FinalizedSettledAmount);
        Assert.Equal(150m, ap1After.Outstanding);
        Assert.Equal("partially_settled", ap1After.SettlementStatus);

        var ap2After = await GetAccountsPayableAsync(tenantId, ap2);
        Assert.Equal(200m, ap2After.FinalizedSettledAmount);
        Assert.Equal(100m, ap2After.Outstanding);

        // Subsequent allocate of remaining unapplied cash
        var alloc3 = await AllocatePaymentAsync(tenantId, paymentId, ap1, 50m);
        await FinalizePaymentAllocationAsync(tenantId, alloc3);
        var fullyApplied = await GetPaymentAsync(tenantId, paymentId);
        Assert.Equal(0m, fullyApplied.UnappliedAmount);
        Assert.Equal(0m, fullyApplied.AvailableToAllocate);
        Assert.Equal(500m, fullyApplied.AppliedAmount);

        var ap1Final = await GetAccountsPayableAsync(tenantId, ap1);
        Assert.Equal(300m, ap1Final.FinalizedSettledAmount);
        Assert.Equal(100m, ap1Final.Outstanding);
    }

    [Fact]
    public async Task FxStub_WriteOff_IdempotentFinalize_TenantIsolation()
    {
        var tenantA = await CreateTenantAsync("TN-E10F-A", "Settlement FULL A");
        var tenantB = await CreateTenantAsync("TN-E10F-B", "Settlement FULL B");
        var billA = await CreateBillAsync(tenantA, "BL-E10F-A", "freight");
        var billB = await CreateBillAsync(tenantB, "BL-E10F-B", "freight");

        // FX stub: USD → VND × 25000
        var paymentUsdId = await CreatePaymentAsync(tenantA, billA, 2m, "USD");
        var paymentUsd = await GetPaymentAsync(tenantA, paymentUsdId);
        Assert.Equal(50_000m, paymentUsd.BaseAmount);
        Assert.Null(paymentUsd.FxRateId);

        // Missing stub rate → validation
        using (var badFx = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new { amount = 1m, currencyCode = "JPY", billId = billA })
        })
        {
            badFx.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var response = await _client.SendAsync(badFx);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // AR path: partial settle then write-off small remainder
        var arId = await RecognizeReceivableAsync(
            tenantA,
            await CreateReceivableExposureAsync(tenantA, billA, 1000m),
            1000m);
        var collectionId = await CreateCollectionAsync(tenantA, billA, 995m, "VND");
        var coll = await GetCollectionAsync(tenantA, collectionId);
        Assert.Equal(995m, coll.BaseAmount);

        var allocId = await AllocateCollectionAsync(tenantA, collectionId, arId, 995m);
        await FinalizeCollectionAllocationAsync(tenantA, allocId);

        var afterSettle = await GetAccountsReceivableAsync(tenantA, arId);
        Assert.Equal(5m, afterSettle.Outstanding);
        Assert.Equal(995m, afterSettle.FinalizedSettledAmount);
        Assert.Equal("partially_settled", afterSettle.SettlementStatus);

        // Write-off over max (1000) rejected when amount > max
        using (var overMax = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-receivable/{arId}/write-off")
        {
            Content = JsonContent.Create(new { amount = 1001m, reason = "Quá trần" })
        })
        {
            overMax.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var response = await _client.SendAsync(overMax);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        // Write-off requires reason
        using (var noReason = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-receivable/{arId}/write-off")
        {
            Content = JsonContent.Create(new { amount = 5m, reason = "" })
        })
        {
            noReason.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var response = await _client.SendAsync(noReason);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        await WriteOffReceivableAsync(tenantA, arId, 5m, "Chênh lệch làm tròn ngân hàng");
        var afterWriteOff = await GetAccountsReceivableAsync(tenantA, arId);
        Assert.Equal(0m, afterWriteOff.Outstanding);
        Assert.Equal(-5m, afterWriteOff.AdjustmentAmount);
        Assert.Equal("settled", afterWriteOff.SettlementStatus);
        Assert.Contains("xóa nợ", afterWriteOff.Notes!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chênh lệch làm tròn ngân hàng", afterWriteOff.Notes!);

        // Idempotent finalize: repeat → 204, outstanding unchanged
        await FinalizeCollectionAllocationAsync(tenantA, allocId);
        await FinalizeCollectionAllocationAsync(tenantA, allocId);
        var still = await GetAccountsReceivableAsync(tenantA, arId);
        Assert.Equal(995m, still.FinalizedSettledAmount);
        Assert.Equal(0m, still.Outstanding);

        // Tenant isolation
        var arB = await RecognizeReceivableAsync(
            tenantB,
            await CreateReceivableExposureAsync(tenantB, billB, 1000m),
            1000m);
        using (var cross = new HttpRequestMessage(HttpMethod.Get, $"/api/collections/{collectionId}"))
        {
            cross.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(cross)).StatusCode);
        }

        using (var crossWo = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-receivable/{arId}/write-off")
        {
            Content = JsonContent.Create(new { amount = 1m, reason = "Xâm nhập" })
        })
        {
            crossWo.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(crossWo)).StatusCode);
        }

        var arBAfter = await GetAccountsReceivableAsync(tenantB, arB);
        Assert.Equal(1000m, arBAfter.Outstanding);

        Assert.Empty(await ListCostsAsync(tenantA));
        Assert.Empty(await ListRevenuesAsync(tenantA));
    }

    [Fact]
    public async Task ApWriteOff_And_FinalizeReversed_IsConflict()
    {
        var tenantId = await CreateTenantAsync("TN-E10F-WO", "Settlement FULL WO");
        var billId = await CreateBillAsync(tenantId, "BL-E10F-WO", "freight");
        var apId = await RecognizePayableAsync(
            tenantId,
            await CreatePayableExposureAsync(tenantId, billId, 100m),
            100m);

        var paymentId = await CreatePaymentAsync(tenantId, billId, 90m, "VND");
        var allocId = await AllocatePaymentAsync(tenantId, paymentId, apId, 90m);
        await FinalizePaymentAllocationAsync(tenantId, allocId);

        await WriteOffPayableAsync(tenantId, apId, 10m, "Miễn phần lẻ");
        var ap = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(0m, ap.Outstanding);
        Assert.Equal(-10m, ap.AdjustmentAmount);
        Assert.Equal("settled", ap.SettlementStatus);
        Assert.Contains("Miễn phần lẻ", ap.Notes!);

        await ReversePaymentAllocationAsync(tenantId, allocId, "Sai đối tượng");
        using (var finalizeReversed = new HttpRequestMessage(
                   HttpMethod.Post, $"/api/payment-allocations/{allocId}/finalize"))
        {
            finalizeReversed.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var response = await _client.SendAsync(finalizeReversed);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var err = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("đã đảo", err!.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Idempotent finalize on a fresh finalized allocation
        var payment2 = await CreatePaymentAsync(tenantId, billId, 50m, "VND");
        // After reverse + write-off, AP outstanding = recognized + adj - settled = 100 - 10 - 0 = 90
        // Wait: reverse restores finalized settled to 0, write-off already applied -10
        // outstanding = 100 + (-10) - 0 = 90. Payment2 allocates 50.
        var alloc2 = await AllocatePaymentAsync(tenantId, payment2, apId, 50m);
        await FinalizePaymentAllocationAsync(tenantId, alloc2);
        await FinalizePaymentAllocationAsync(tenantId, alloc2); // no-op
        var apAfter = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(50m, apAfter.FinalizedSettledAmount);
        Assert.Equal(40m, apAfter.Outstanding);
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

    private async Task<Guid> CreatePaymentAsync(Guid tenantId, Guid billId, decimal amount, string currency)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new { amount, currencyCode = currency, billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> CreateCollectionAsync(Guid tenantId, Guid billId, decimal amount, string currency)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/collections")
        {
            Content = JsonContent.Create(new { amount, currencyCode = currency, billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> AllocatePaymentAsync(Guid tenantId, Guid paymentId, Guid apId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/{paymentId}/allocations")
        {
            Content = JsonContent.Create(new { accountsPayableId = apId, amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> AllocateCollectionAsync(Guid tenantId, Guid collectionId, Guid arId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/collections/{collectionId}/allocations")
        {
            Content = JsonContent.Create(new { accountsReceivableId = arId, amount })
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
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task FinalizeCollectionAllocationAsync(Guid tenantId, Guid allocationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/collection-allocations/{allocationId}/finalize");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task ReversePaymentAllocationAsync(Guid tenantId, Guid allocationId, string reason)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payment-allocations/{allocationId}/reverse")
        {
            Content = JsonContent.Create(new { reason })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task WriteOffPayableAsync(Guid tenantId, Guid apId, decimal amount, string reason)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-payable/{apId}/write-off")
        {
            Content = JsonContent.Create(new { amount, reason })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task WriteOffReceivableAsync(Guid tenantId, Guid arId, decimal amount, string reason)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-receivable/{arId}/write-off")
        {
            Content = JsonContent.Create(new { amount, reason })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<PaymentDto> GetPaymentAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/payments/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentDto>(JsonOptions))!;
    }

    private async Task<CollectionDto> GetCollectionAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/collections/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CollectionDto>(JsonOptions))!;
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

    private sealed record PaymentAllocationDto(
        Guid Id,
        Guid PaymentId,
        Guid AccountsPayableId,
        decimal Amount,
        string CurrencyCode,
        decimal? BaseAmount,
        Guid? FxRateId,
        string AllocationStatus);

    private sealed record PaymentDto(
        Guid Id,
        decimal Amount,
        decimal? BaseAmount,
        Guid? FxRateId,
        decimal AppliedAmount,
        decimal AllocatedAmount,
        decimal UnappliedAmount,
        decimal AvailableToAllocate,
        string CurrencyCode,
        List<PaymentAllocationDto> Allocations);

    private sealed record CollectionDto(
        Guid Id,
        decimal Amount,
        decimal? BaseAmount,
        Guid? FxRateId,
        decimal AppliedAmount,
        decimal AllocatedAmount,
        decimal UnappliedAmount,
        decimal AvailableToAllocate,
        string CurrencyCode);

    private sealed record AccountsPayableDto(
        Guid Id,
        decimal RecognizedAmount,
        decimal AdjustmentAmount,
        decimal FinalizedSettledAmount,
        decimal Outstanding,
        string SettlementStatus,
        string? Notes);

    private sealed record AccountsReceivableDto(
        Guid Id,
        decimal RecognizedAmount,
        decimal AdjustmentAmount,
        decimal FinalizedSettledAmount,
        decimal Outstanding,
        string SettlementStatus,
        string? Notes);
}
