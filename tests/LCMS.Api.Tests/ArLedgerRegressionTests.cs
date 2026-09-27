using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

/// <summary>
/// UAT AR regression 2026-09-27: AR ledger chain, write-off reversal (compensating row),
/// allocation currency, audit on adjust / allocation reverse.
/// </summary>
[Collection("Api")]
public sealed class ArLedgerRegressionTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public ArLedgerRegressionTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task ArLedger_Chain_WriteOffReversal_RestoresBalance_WithoutTouchingRevenue()
    {
        var tenantId = await CreateTenantAsync("TN-ARLEDGER-1", "AR Ledger Regression");
        var billId = await CreateBillAsync(tenantId, "HAWB-UAT-LEDGER-1");
        var exposureId = await PostIdAsync(tenantId, "/api/receivable-exposures",
            new { amount = 280m, currencyCode = "USD", billId });
        var arId = await PostIdAsync(tenantId, $"/api/receivable-exposures/{exposureId}/recognize",
            new { amount = 280m });
        var revenueCountBefore = await CountAsync(tenantId, "/api/revenues");

        await PostExpectAsync(tenantId, $"/api/accounts-receivable/{arId}/adjust",
            new { deltaAmount = -20m, reason = "Giảm giá theo thỏa thuận" }, HttpStatusCode.Created);
        await PostExpectAsync(tenantId, $"/api/accounts-receivable/{arId}/adjust",
            new { deltaAmount = 20m, reason = "Khôi phục giảm giá" }, HttpStatusCode.Created);

        var collectionId = await PostIdAsync(tenantId, "/api/collections",
            new { amount = 35m, currencyCode = "USD", billId });
        var allocationId = await PostIdAsync(tenantId, $"/api/collections/{collectionId}/allocations",
            new { accountsReceivableId = arId, amount = 35m });
        var collection = await GetAsync<JsonElement>(tenantId, $"/api/collections/{collectionId}");
        Assert.Equal("USD", collection.GetProperty("allocations")[0].GetProperty("currencyCode").GetString());

        await PostExpectAsync(tenantId, $"/api/collection-allocations/{allocationId}/finalize", null, HttpStatusCode.NoContent);
        Assert.Equal(245m, (await GetArAsync(tenantId, arId)).Outstanding);
        await PostExpectAsync(tenantId, $"/api/collection-allocations/{allocationId}/reverse",
            new { reason = "Phân bổ nhầm" }, HttpStatusCode.NoContent);
        Assert.Equal(280m, (await GetArAsync(tenantId, arId)).Outstanding);

        await PostExpectAsync(tenantId, $"/api/accounts-receivable/{arId}/write-off",
            new { amount = 30m, reason = "Khách không thanh toán phần chênh" }, HttpStatusCode.NoContent);
        Assert.Equal(250m, (await GetArAsync(tenantId, arId)).Outstanding);

        var ledger = await GetAsync<LedgerDto>(tenantId, $"/api/accounts-receivable/{arId}/ledger");
        Assert.True(ledger.Reconciled);
        Assert.Equal(
            new[] { "recognition", "adjustment", "adjustment", "allocation", "allocation_reversal", "write_off" },
            ledger.Entries.Select(e => e.EntryType).ToArray());
        Assert.Equal(
            new[] { 280m, 260m, 280m, 245m, 280m, 250m },
            ledger.Entries.Select(e => e.BalanceAfter).ToArray());
        Assert.All(ledger.Entries, e => Assert.Equal("USD", e.CurrencyCode));
        var writeOff = ledger.Entries[^1];
        Assert.Equal(-30m, writeOff.Amount);
        Assert.Equal("posted", writeOff.Status);
        Assert.Equal("Khách không thanh toán phần chênh", writeOff.Reason);
        Assert.Equal(collectionId, ledger.Entries[3].CashId);

        await PostExpectAsync(tenantId, $"/api/accounts-receivable/{arId}/write-offs/{writeOff.SourceId}/reverse",
            new { reason = "Khách xác nhận trả đủ" }, HttpStatusCode.Created);
        var ar = await GetArAsync(tenantId, arId);
        Assert.Equal(280m, ar.Outstanding);
        Assert.Equal(0m, ar.AdjustmentAmount);

        var after = await GetAsync<LedgerDto>(tenantId, $"/api/accounts-receivable/{arId}/ledger");
        Assert.True(after.Reconciled);
        var reversal = after.Entries[^1];
        Assert.Equal("write_off_reversal", reversal.EntryType);
        Assert.Equal(30m, reversal.Amount);
        Assert.Equal(280m, reversal.BalanceAfter);
        Assert.Equal(writeOff.Id, reversal.ReversesEntryId);
        var original = after.Entries.Single(e => e.Id == writeOff.Id);
        Assert.Equal("reversed", original.Status);
        Assert.Equal(reversal.Id, original.ReversedByEntryId);

        await PostExpectAsync(tenantId, $"/api/accounts-receivable/{arId}/write-offs/{writeOff.SourceId}/reverse",
            new { reason = "Lặp" }, HttpStatusCode.Conflict);

        Assert.Equal(revenueCountBefore, await CountAsync(tenantId, "/api/revenues"));

        var arAudit = await GetAsync<List<JsonElement>>(tenantId,
            $"/api/audit-events?objectType=accounts_receivable&objectId={arId}&take=50");
        var actions = arAudit.Select(e => e.GetProperty("action").GetString()).ToList();
        Assert.Contains("accounts_receivable.adjust", actions);
        Assert.Contains("accounts_receivable.write_off", actions);
        Assert.Contains("accounts_receivable.write_off_reverse", actions);

        var allocAudit = await GetAsync<List<JsonElement>>(tenantId,
            $"/api/audit-events?objectType=collection_allocation&objectId={allocationId}&take=50");
        Assert.Contains(allocAudit, e => e.GetProperty("action").GetString() == "collection_allocation.reverse"
                                         && e.GetProperty("reason").GetString() == "Phân bổ nhầm");

        var history = await GetAsync<List<LedgerDto>>(tenantId, $"/api/bills/{billId}/financial-history");
        var arHistory = Assert.Single(history);
        Assert.Equal(arId, arHistory.AccountId);
        Assert.Equal(7, arHistory.Entries.Count);
    }

    [Fact]
    public async Task ReverseWriteOff_RejectsNonWriteOffRow_And_OtherTenant()
    {
        var tenantId = await CreateTenantAsync("TN-ARLEDGER-2", "AR Ledger Guard");
        var billId = await CreateBillAsync(tenantId, "HAWB-UAT-LEDGER-2");
        var exposureId = await PostIdAsync(tenantId, "/api/receivable-exposures",
            new { amount = 100m, currencyCode = "VND", billId });
        var arId = await PostIdAsync(tenantId, $"/api/receivable-exposures/{exposureId}/recognize",
            new { amount = 100m });
        var adjId = await PostIdAsync(tenantId, $"/api/accounts-receivable/{arId}/adjust",
            new { deltaAmount = -10m, reason = "Điều chỉnh" });

        await PostExpectAsync(tenantId, $"/api/accounts-receivable/{arId}/write-offs/{adjId}/reverse",
            new { reason = "Không phải xóa nợ" }, HttpStatusCode.Conflict);

        var otherTenant = await CreateTenantAsync("TN-ARLEDGER-3", "AR Ledger Other");
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/accounts-receivable/{arId}/ledger");
        req.Headers.Add("X-Tenant-Id", otherTenant.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private Task<Guid> CreateBillAsync(Guid tenantId, string billNo) =>
        PostIdAsync(tenantId, "/api/bills", new { billNo, billType = "freight" });

    private async Task<Guid> PostIdAsync(Guid tenantId, string path, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task PostExpectAsync(Guid tenantId, string path, object? body, HttpStatusCode expected)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        Assert.True(response.StatusCode == expected,
            $"{path}: expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task<T> GetAsync<T>(Guid tenantId, string path)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        Assert.True(response.IsSuccessStatusCode, $"{path}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task<int> CountAsync(Guid tenantId, string path) =>
        (await GetAsync<List<JsonElement>>(tenantId, path)).Count;

    private Task<ArDto> GetArAsync(Guid tenantId, Guid id) =>
        GetAsync<ArDto>(tenantId, $"/api/accounts-receivable/{id}");

    private sealed record IdResponse(Guid Id);

    private sealed record ArDto(Guid Id, decimal Outstanding, decimal AdjustmentAmount, string CurrencyCode);

    private sealed record LedgerEntryDto(
        string Id,
        string EntryType,
        decimal Amount,
        string CurrencyCode,
        decimal BalanceBefore,
        decimal BalanceAfter,
        string? Reason,
        Guid SourceId,
        Guid? CashId,
        string? ReversesEntryId,
        string? ReversedByEntryId,
        string Status);

    private sealed record LedgerDto(Guid AccountId, bool Reconciled, List<LedgerEntryDto> Entries);
}
