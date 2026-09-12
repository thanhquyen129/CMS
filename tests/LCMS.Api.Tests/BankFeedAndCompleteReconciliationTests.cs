using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class BankFeedAndCompleteReconciliationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public BankFeedAndCompleteReconciliationTests(LcmsApiFactory factory)
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
    public async Task BankFeedLine_CanMatchViaReconciliation_AndCompleteSession()
    {
        var tenantId = await CreateTenantAsync("TN-BF-01", "Bank Feed Tenant");
        var billId = await CreateBillAsync(tenantId, "BL-BF-1", "freight");
        var paymentId = await CreatePaymentAsync(tenantId, billId, 500_000m);

        var bankLineId = await CreateBankFeedLineAsync(tenantId, 500_000m, "debit");

        var list = await ListBankFeedLinesAsync(tenantId, "unmatched");
        Assert.Contains(list, x => x.Id == bankLineId);

        var reconId = await StartReconciliationAsync(tenantId, billId, "manual");
        await AddReconciliationDetailAsync(
            tenantId,
            reconId,
            sourceType: "bank_line",
            sourceId: bankLineId,
            targetType: "payment",
            targetId: paymentId,
            sourceAmount: 500_000m,
            targetAmount: 500_000m,
            matchedAmount: 500_000m);

        var matchedLine = await GetBankFeedLineAsync(tenantId, bankLineId);
        Assert.Equal("matched", matchedLine.Status);
        Assert.NotNull(matchedLine.MatchedReconciliationDetailId);

        using (var complete = new HttpRequestMessage(HttpMethod.Post, $"/api/reconciliations/{reconId}/complete"))
        {
            complete.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(complete)).StatusCode);
        }

        using var getRecon = new HttpRequestMessage(HttpMethod.Get, $"/api/reconciliations/{reconId}");
        getRecon.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var reconRes = await _client.SendAsync(getRecon);
        reconRes.EnsureSuccessStatusCode();
        var recon = (await reconRes.Content.ReadFromJsonAsync<ReconDto>(JsonOptions))!;
        Assert.Equal("completed", recon.Status);
        Assert.NotNull(recon.CompletedAt);
    }

    [Fact]
    public async Task BankFeedLine_Ignore_BlocksReuse()
    {
        var tenantId = await CreateTenantAsync("TN-BF-02", "Bank Feed Ignore");
        var lineId = await CreateBankFeedLineAsync(tenantId, 1000m, "credit");

        using (var ignore = new HttpRequestMessage(HttpMethod.Post, $"/api/bank-feed/lines/{lineId}/ignore")
        {
            Content = JsonContent.Create(new { ignoreReason = "Phí ngân hàng ngoài phạm vi" })
        })
        {
            ignore.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(ignore)).StatusCode);
        }

        var line = await GetBankFeedLineAsync(tenantId, lineId);
        Assert.Equal("ignored", line.Status);

        var billId = await CreateBillAsync(tenantId, "BL-BF-IGN", "freight");
        var reconId = await StartReconciliationAsync(tenantId, billId, "manual");

        using var detail = new HttpRequestMessage(HttpMethod.Post, $"/api/reconciliations/{reconId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceType = "bank_line",
                sourceId = lineId,
                targetType = (string?)null,
                targetId = (Guid?)null,
                sourceAmount = 1000m,
                targetAmount = 0m,
                matchedAmount = 0m,
                currencyCode = "VND"
            })
        };
        detail.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(detail);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(new { code, name })
        };
        var response = await _client.SendAsync(req);
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
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
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
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateBankFeedLineAsync(Guid tenantId, decimal amount, string direction)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bank-feed/lines")
        {
            Content = JsonContent.Create(new
            {
                valueDate = DateOnly.FromDateTime(DateTime.UtcNow),
                amount,
                currencyCode = "VND",
                direction,
                bankReference = "REF-BF",
                counterpartyName = "NH Test",
                description = "Sao kê thử"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<List<BankLineDto>> ListBankFeedLinesAsync(Guid tenantId, string? status)
    {
        var path = status is null ? "/api/bank-feed/lines" : $"/api/bank-feed/lines?status={status}";
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<BankLineDto>>(JsonOptions))!;
    }

    private async Task<BankLineDto> GetBankFeedLineAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/bank-feed/lines/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BankLineDto>(JsonOptions))!;
    }

    private async Task<Guid> StartReconciliationAsync(Guid tenantId, Guid billId, string type)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/reconciliations")
        {
            Content = JsonContent.Create(new { reconciliationType = type, billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task AddReconciliationDetailAsync(
        Guid tenantId,
        Guid reconId,
        string sourceType,
        Guid sourceId,
        string? targetType,
        Guid? targetId,
        decimal sourceAmount,
        decimal targetAmount,
        decimal matchedAmount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/reconciliations/{reconId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceType,
                sourceId,
                targetType,
                targetId,
                sourceAmount,
                targetAmount,
                matchedAmount,
                currencyCode = "VND"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private sealed record IdResponse(Guid Id);

    private sealed record BankLineDto(
        Guid Id,
        string Status,
        Guid? MatchedReconciliationDetailId);

    private sealed record ReconDto(string Status, DateTimeOffset? CompletedAt);
}
