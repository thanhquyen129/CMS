using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class SettlementBillNoTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public SettlementBillNoTests(LcmsApiFactory factory)
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
    public async Task ListAndGet_PaymentsAndCollections_IncludeBillNo()
    {
        var tenantId = await CreateTenantAsync("TN-G6-BILLNO", "Settlement BillNo");
        var billId = await CreateBillAsync(tenantId, "UAT-G6-BILL-01", "freight");

        var paymentId = await CreatePaymentAsync(tenantId, billId, 1000m);
        var collectionId = await CreateCollectionAsync(tenantId, billId, 2000m);

        var payments = await ListPaymentsAsync(tenantId);
        var paymentRow = Assert.Single(payments, p => p.Id == paymentId);
        Assert.Equal(billId, paymentRow.BillId);
        Assert.Equal("UAT-G6-BILL-01", paymentRow.BillNo);

        var payment = await GetPaymentAsync(tenantId, paymentId);
        Assert.Equal("UAT-G6-BILL-01", payment.BillNo);

        var collections = await ListCollectionsAsync(tenantId);
        var collectionRow = Assert.Single(collections, c => c.Id == collectionId);
        Assert.Equal(billId, collectionRow.BillId);
        Assert.Equal("UAT-G6-BILL-01", collectionRow.BillNo);

        var collection = await GetCollectionAsync(tenantId, collectionId);
        Assert.Equal("UAT-G6-BILL-01", collection.BillNo);
    }

    [Fact]
    public async Task PaymentWithoutBill_HasNullBillNo()
    {
        var tenantId = await CreateTenantAsync("TN-G6-NOBILL", "Settlement No Bill");
        var paymentId = await CreatePaymentAsync(tenantId, billId: null, 500m);

        var payment = await GetPaymentAsync(tenantId, paymentId);
        Assert.Null(payment.BillId);
        Assert.Null(payment.BillNo);
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

    private async Task<Guid> CreatePaymentAsync(Guid tenantId, Guid? billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new
            {
                amount,
                currencyCode = "VND",
                billId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateCollectionAsync(Guid tenantId, Guid? billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/collections")
        {
            Content = JsonContent.Create(new
            {
                amount,
                currencyCode = "VND",
                billId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<List<SettlementCashDto>> ListPaymentsAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/payments");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<SettlementCashDto>>(JsonOptions))!;
    }

    private async Task<SettlementCashDto> GetPaymentAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/payments/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SettlementCashDto>(JsonOptions))!;
    }

    private async Task<List<SettlementCashDto>> ListCollectionsAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/collections");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<SettlementCashDto>>(JsonOptions))!;
    }

    private async Task<SettlementCashDto> GetCollectionAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/collections/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SettlementCashDto>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record SettlementCashDto(
        Guid Id,
        Guid? BillId,
        string? BillNo);
}
