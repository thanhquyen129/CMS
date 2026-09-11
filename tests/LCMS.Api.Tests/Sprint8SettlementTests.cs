using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint8SettlementTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint8SettlementTests(LcmsApiFactory factory)
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
    public async Task PartialSettle_OutstandingDecreasesOnlyAfterFinalize_AndNoCostOrRevenueInvented()
    {
        var tenantId = await CreateTenantAsync("TN-E10-PART", "Settlement Partial");
        var billId = await CreateBillAsync(tenantId, "BL-E10-1", "freight");

        Assert.Empty(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));

        var exposureId = await CreatePayableExposureAsync(tenantId, billId, 1000m);
        var apId = await RecognizePayableAsync(tenantId, exposureId, 1000m);

        var before = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(1000m, before.Outstanding);
        Assert.Equal(0m, before.FinalizedSettledAmount);
        Assert.Equal("open", before.SettlementStatus);

        var paymentId = await CreatePaymentAsync(tenantId, billId, 600m);
        var payment = await GetPaymentAsync(tenantId, paymentId);
        Assert.Equal(600m, payment.UnappliedAmount);
        Assert.Equal(600m, payment.AvailableToAllocate);

        var allocId = await AllocatePaymentAsync(tenantId, paymentId, apId, 400m);

        // AC-007: draft does not change outstanding
        var afterDraft = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(1000m, afterDraft.Outstanding);
        Assert.Equal(0m, afterDraft.FinalizedSettledAmount);
        Assert.Equal("open", afterDraft.SettlementStatus);

        var paymentDraft = await GetPaymentAsync(tenantId, paymentId);
        Assert.Equal(600m, paymentDraft.UnappliedAmount);
        Assert.Equal(200m, paymentDraft.AvailableToAllocate);
        Assert.Equal(0m, paymentDraft.AppliedAmount);
        Assert.Equal(400m, paymentDraft.AllocatedAmount);

        await FinalizePaymentAllocationAsync(tenantId, allocId);

        var afterFinalize = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(400m, afterFinalize.FinalizedSettledAmount);
        Assert.Equal(600m, afterFinalize.Outstanding);
        Assert.Equal("partially_settled", afterFinalize.SettlementStatus);

        var paymentApplied = await GetPaymentAsync(tenantId, paymentId);
        Assert.Equal(400m, paymentApplied.AppliedAmount);
        Assert.Equal(200m, paymentApplied.UnappliedAmount);
        Assert.Equal(200m, paymentApplied.AvailableToAllocate);

        // C-003 / C-004: settlement never invents Cost/Revenue
        Assert.Empty(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));
    }

    [Fact]
    public async Task OverAllocation_IsRejected_AndReversalRestoresOutstandingWithoutHardDelete()
    {
        var tenantId = await CreateTenantAsync("TN-E10-OVER", "Settlement Over");
        var billId = await CreateBillAsync(tenantId, "BL-E10-2", "freight");

        var exposureId = await CreatePayableExposureAsync(tenantId, billId, 500m);
        var apId = await RecognizePayableAsync(tenantId, exposureId, 500m);
        var paymentId = await CreatePaymentAsync(tenantId, billId, 1000m);

        // Over AP obligation (C-008)
        using (var overAp = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/{paymentId}/allocations")
        {
            Content = JsonContent.Create(new { accountsPayableId = apId, amount = 501m })
        })
        {
            overAp.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var response = await _client.SendAsync(overAp);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var err = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("C-008", err!.Message);
        }

        // Partial allocate + finalize
        var allocId = await AllocatePaymentAsync(tenantId, paymentId, apId, 300m);
        await FinalizePaymentAllocationAsync(tenantId, allocId);
        var settled = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(200m, settled.Outstanding);
        Assert.Equal(300m, settled.FinalizedSettledAmount);

        // Over remaining payment available after 300 allocated — payment has 1000, so over AP remaining
        using (var overRemaining = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/{paymentId}/allocations")
        {
            Content = JsonContent.Create(new { accountsPayableId = apId, amount = 250m })
        })
        {
            overRemaining.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var response = await _client.SendAsync(overRemaining);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        // Reversal restores outstanding; allocation remains (status reversed)
        await ReversePaymentAllocationAsync(tenantId, allocId, "Sai số tiền");
        var afterReverse = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(0m, afterReverse.FinalizedSettledAmount);
        Assert.Equal(500m, afterReverse.Outstanding);
        Assert.Equal("open", afterReverse.SettlementStatus);

        var payment = await GetPaymentAsync(tenantId, paymentId);
        var reversed = Assert.Single(payment.Allocations);
        Assert.Equal("reversed", reversed.AllocationStatus);
        Assert.Equal(0m, payment.AppliedAmount);
        Assert.Equal(1000m, payment.AvailableToAllocate);
        Assert.NotNull(reversed.ReversedAt);
        Assert.Equal("Sai số tiền", reversed.ReverseReason);
    }

    [Fact]
    public async Task Collection_PartialSettle_CrossTenantIsolated_NoRevenueInvented()
    {
        var tenantA = await CreateTenantAsync("TN-E10-A", "Settlement A");
        var tenantB = await CreateTenantAsync("TN-E10-B", "Settlement B");
        var billA = await CreateBillAsync(tenantA, "BL-E10-A", "freight");
        var billB = await CreateBillAsync(tenantB, "BL-E10-B", "freight");

        var exposureA = await CreateReceivableExposureAsync(tenantA, billA, 800m);
        var arA = await RecognizeReceivableAsync(tenantA, exposureA, 800m);

        var exposureB = await CreateReceivableExposureAsync(tenantB, billB, 800m);
        var arB = await RecognizeReceivableAsync(tenantB, exposureB, 800m);

        var collectionId = await CreateCollectionAsync(tenantA, billA, 500m);
        var allocId = await AllocateCollectionAsync(tenantA, collectionId, arA, 200m);

        // Draft: AR outstanding unchanged
        var draftAr = await GetAccountsReceivableAsync(tenantA, arA);
        Assert.Equal(800m, draftAr.Outstanding);

        await FinalizeCollectionAllocationAsync(tenantA, allocId);
        var after = await GetAccountsReceivableAsync(tenantA, arA);
        Assert.Equal(200m, after.FinalizedSettledAmount);
        Assert.Equal(600m, after.Outstanding);
        Assert.Equal("partially_settled", after.SettlementStatus);

        // Cross-tenant: tenant B cannot see tenant A collection
        using (var req = new HttpRequestMessage(HttpMethod.Get, $"/api/collections/{collectionId}"))
        {
            req.Headers.Add("X-Tenant-Id", tenantB.ToString());
            var response = await _client.SendAsync(req);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // Tenant B AR untouched
        var arBAfter = await GetAccountsReceivableAsync(tenantB, arB);
        Assert.Equal(800m, arBAfter.Outstanding);
        Assert.Equal(0m, arBAfter.FinalizedSettledAmount);

        // Tenant A cannot allocate to tenant B AR
        using (var cross = new HttpRequestMessage(HttpMethod.Post, $"/api/collections/{collectionId}/allocations")
        {
            Content = JsonContent.Create(new { accountsReceivableId = arB, amount = 50m })
        })
        {
            cross.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var response = await _client.SendAsync(cross);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        Assert.Empty(await ListCostsAsync(tenantA));
        Assert.Empty(await ListRevenuesAsync(tenantA));
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

    private async Task<Guid> CreateCollectionAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/collections")
        {
            Content = JsonContent.Create(new { amount, currencyCode = "VND", billId })
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

    private async Task<PaymentDto> GetPaymentAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/payments/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentDto>(JsonOptions))!;
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
        string AllocationStatus,
        DateTimeOffset? FinalizedAt,
        DateTimeOffset? ReversedAt,
        string? ReverseReason,
        string? Notes);

    private sealed record PaymentDto(
        Guid Id,
        decimal Amount,
        decimal AppliedAmount,
        decimal AllocatedAmount,
        decimal UnappliedAmount,
        decimal AvailableToAllocate,
        string CurrencyCode,
        DateOnly ValueDate,
        Guid? CounterpartyId,
        Guid? BillId,
        string? ReferenceNo,
        string? Notes,
        string Status,
        string RecordStatus,
        List<PaymentAllocationDto> Allocations);

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
        string RecordStatus);

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
