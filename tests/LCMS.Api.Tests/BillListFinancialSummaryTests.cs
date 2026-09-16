using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

/// <summary>
/// GET /api/bills returns batch financial summary on each row (no N+1 profile).
/// </summary>
[Collection("Api")]
public sealed class BillListFinancialSummaryTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public BillListFinancialSummaryTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task List_bills_includes_revenue_cost_profit_best_available()
    {
        var tenantId = await CreateTenantAsync();
        var billNo = $"BL-SUM-{Guid.NewGuid():N}"[..16];
        var billId = await CreateBillAsync(tenantId, billNo);

        using (var createRev = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount = 1000m,
                currencyCode = "VND",
                effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                revenueTypeCode = "FREIGHT",
                customerPartyId = (Guid?)null,
                sourceType = (string?)null,
                sourceId = (Guid?)null,
                recognitionPolicyVersion = (string?)null,
            })
        })
        {
            createRev.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(createRev);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        using (var createCost = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 400m,
                currencyCode = "VND",
                effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
                costTypeCode = "FREIGHT",
                vendorPartyId = (Guid?)null,
                sourceType = (string?)null,
                sourceId = (Guid?)null,
                organizationId = (Guid?)null,
            })
        })
        {
            createCost.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(createCost);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        }

        using var list = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/bills?q={Uri.EscapeDataString(billNo)}");
        list.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var listRes = await _client.SendAsync(list);
        listRes.EnsureSuccessStatusCode();
        var items = await listRes.Content.ReadFromJsonAsync<List<BillListRow>>(JsonOptions);
        Assert.NotNull(items);
        var row = Assert.Single(items!, b => b.Id == billId);

        Assert.Equal("VND", row.SummaryCurrencyCode);
        Assert.Equal(1000m, row.RevenueBestAvailable);
        Assert.Equal(1000m, row.RevenueExpectedTotal);
        Assert.Equal(400m, row.CostBestAvailable);
        Assert.Equal(600m, row.ProfitBestAvailable);
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var code = $"TN-SUM-{Guid.NewGuid():N}"[..12];
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(new { code, name = "Bill list summary" })
        };
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new
            {
                billNo,
                billType = "air",
                sourceSystem = (string?)null,
                externalId = (string?)null,
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record BillListRow(
        Guid Id,
        string BillNo,
        string? SummaryCurrencyCode,
        decimal? RevenueBestAvailable,
        decimal? CostBestAvailable,
        decimal? ProfitBestAvailable,
        decimal? RevenueExpectedTotal);
}
