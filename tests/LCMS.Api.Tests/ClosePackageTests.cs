using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class ClosePackageTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public ClosePackageTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task OpenCostAllocation_AndUnallocatedCash_BlockSnapshot()
    {
        var tenantId = await CreateTenantAsync();
        var billA = await CreateBillAsync(tenantId, "BL-G-ALLOC-A");
        var billB = await CreateBillAsync(tenantId, "BL-G-ALLOC-B");
        var costId = await PostId(tenantId, "/api/costs", new
        {
            attributionType = "shared",
            amount = 100m,
            currencyCode = "VND",
            costTypeCode = "SHARED"
        });
        await PostId(tenantId, $"/api/costs/{costId}/allocations", new
        {
            allocationBasis = "equal",
            details = new[] { new { billId = billA }, new { billId = billB } }
        });

        var closeId = await PostId(tenantId, "/api/financial-closes", new
        {
            scopeType = "tenant",
            policyVersion = "controlled"
        });
        await AssertSnapshotBlocked(tenantId, closeId, "phân bổ");

        var cashBill = await CreateBillAsync(tenantId, "BL-G-CASH");
        await PostId(tenantId, "/api/payments", new { amount = 50m, currencyCode = "VND", billId = cashBill });
        var closeCash = await PostId(tenantId, "/api/financial-closes", new
        {
            scopeType = "bill",
            scopeId = cashBill,
            policyVersion = "controlled"
        });
        await AssertSnapshotBlocked(tenantId, closeCash, "chưa gán");
    }

    [Fact]
    public async Task WaivedException_IsRecordedInSnapshot_AndLateMaterialDocumentRequiresReopen()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-G-WAIVE");
        var exceptionId = await PostId(tenantId, "/api/exceptions", new
        {
            ruleCode = "CR-05",
            severity = "low",
            title = "Miễn trước chốt",
            billId
        });
        await PostNoContent(tenantId, $"/api/exceptions/{exceptionId}/waive", new { reason = "Đã duyệt nghiệp vụ" });

        var closeId = await PostId(tenantId, "/api/financial-closes", new
        {
            scopeType = "bill",
            scopeId = billId,
            policyVersion = "controlled"
        });
        var snapshotId = await PostId(tenantId, $"/api/financial-closes/{closeId}/snapshot", null);
        var close = await GetAsync<CloseBody>(tenantId, $"/api/financial-closes/{closeId}");
        Assert.Equal("locked", close.Status);
        Assert.Contains(close.Snapshots.SelectMany(s => s.Details), d => d.MetricKey == "waiver");

        using var late = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        late.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-G-LATE",
            direction = "payable",
            totalAmount = 10m,
            currencyCode = "VND",
            billId,
            documentDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        var blocked = await _client.SendAsync(late);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("mở lại chốt", (await blocked.Content.ReadFromJsonAsync<Err>(Json))!.Message);

        await PostNoContent(tenantId, $"/api/financial-closes/{closeId}/reopen", new { reason = "Nhận chứng từ trễ" });
        var after = await GetAsync<CloseBody>(tenantId, $"/api/financial-closes/{closeId}");
        Assert.Equal("reopened", after.Status);
        Assert.Equal(snapshotId, Assert.Single(after.Snapshots).Id);
    }

    [Fact]
    public async Task AgingAsOf_ReflectsSettledAmount_AndCashReportReturnsRows()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-G-AGE");
        var exposureId = await PostId(tenantId, "/api/payable-exposures", new
        {
            amount = 1000m,
            currencyCode = "VND",
            billId
        });
        var apId = await PostId(tenantId, $"/api/payable-exposures/{exposureId}/recognize", new { amount = 1000m });
        var paymentId = await PostId(tenantId, "/api/payments", new
        {
            amount = 400m,
            currencyCode = "VND",
            billId,
            valueDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        var allocId = await PostId(tenantId, $"/api/payments/{paymentId}/allocations", new
        {
            accountsPayableId = apId,
            amount = 400m
        });
        await PostNoContent(tenantId, $"/api/payment-allocations/{allocId}/finalize", null);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var aging = await GetAsync<AgingBody>(tenantId, $"/api/accounts-payable/aging?asOf={today:yyyy-MM-dd}");
        var row = Assert.Single(aging.PayableItems!);
        Assert.Equal(600m, row.Outstanding);

        var cash = await GetAsync<CashBody>(tenantId, $"/api/reports/cash-settlement");
        Assert.Contains(cash.Items, i => i.Kind == "payment" && i.Id == paymentId);
    }

    private async Task AssertSnapshotBlocked(Guid tenantId, Guid closeId, string fragment)
    {
        using var req = Tenant(HttpMethod.Post, $"/api/financial-closes/{closeId}/snapshot", tenantId);
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains(fragment, (await res.Content.ReadFromJsonAsync<Err>(Json))!.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid> PostId(Guid tenantId, string url, object? body)
    {
        using var req = Tenant(HttpMethod.Post, url, tenantId);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task PostNoContent(Guid tenantId, string url, object? body)
    {
        using var req = Tenant(HttpMethod.Post, url, tenantId);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<T> GetAsync<T>(Guid tenantId, string url)
    {
        using var req = Tenant(HttpMethod.Get, url, tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo) =>
        await PostId(tenantId, "/api/bills", new
        {
            billNo,
            billType = "house",
            sourceSystem = "lcms_manual",
            externalId = billNo
        });

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new
        {
            code = "TN-" + Guid.NewGuid().ToString("N")[..8],
            name = "CloseG"
        });
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
    private sealed record Err(string Message);
    private sealed record CloseBody(string Status, List<SnapBody> Snapshots);
    private sealed record SnapBody(Guid Id, List<DetailBody> Details);
    private sealed record DetailBody(string MetricKey, Guid? SourceId);
    private sealed record AgingBody(List<ApRow>? PayableItems);
    private sealed record ApRow(decimal Outstanding);
    private sealed record CashBody(List<CashRow> Items);
    private sealed record CashRow(Guid Id, string Kind);
}
