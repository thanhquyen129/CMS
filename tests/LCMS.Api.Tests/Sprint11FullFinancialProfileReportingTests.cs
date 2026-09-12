using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint11FullFinancialProfileReportingTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint11FullFinancialProfileReportingTests(LcmsApiFactory factory)
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
    public async Task AsOf_ReconstructsMaturity_AndSettlementOutstandingFromAllocations()
    {
        var tenantId = await CreateTenantAsync("TN-E13F-PROF", "Profile FULL");
        var billId = await CreateBillAsync(tenantId, "BL-E13F-1", "freight");

        var revId = await CreateRevenueAsync(tenantId, billId, 10000m, "FREIGHT", "VND", new DateOnly(2026, 8, 1));
        await ConfirmRevenueAsync(tenantId, revId, 12000m);
        await ActualizeRevenueAsync(tenantId, revId, 11500m);

        var costId = await CreateDirectCostAsync(tenantId, billId, 4000m, "FREIGHT", "VND", new DateOnly(2026, 8, 10));
        await ConfirmCostAsync(tenantId, costId, 4200m);

        var apId = await RecognizePayableAsync(
            tenantId,
            await CreatePayableExposureAsync(tenantId, billId, 2000m),
            2000m);
        var paymentId = await CreatePaymentAsync(tenantId, billId, 800m, "VND");
        var allocId = await AllocatePaymentAsync(tenantId, paymentId, apId, 800m);
        await FinalizePaymentAllocationAsync(tenantId, allocId);

        var live = await GetFinancialProfileAsync(tenantId, billId);
        var liveVnd = Assert.Single(live.ByCurrency);
        Assert.Equal(11500m, liveVnd.RevenueBestAvailable);
        Assert.Equal(10000m, liveVnd.RevenueMaturity.ExpectedTotal);
        Assert.Equal(12000m, liveVnd.RevenueMaturity.ConfirmedTotal);
        Assert.Equal(11500m, liveVnd.RevenueMaturity.ActualTotal);
        Assert.Equal(4200m, liveVnd.DirectCostBestAvailable);
        Assert.Equal(4200m, liveVnd.DirectCostMaturity.ConfirmedTotal);
        var liveSettle = Assert.Single(live.SettlementOutstanding);
        Assert.Equal(1200m, liveSettle.AccountsPayableOutstanding);

        // asOf before confirm/actualize timestamps → Expected only
        var asOfPast = await GetFinancialProfileAsync(tenantId, billId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        Assert.NotNull(asOfPast.AsOfLimitationNote);
        Assert.Contains("ConfirmedAt", asOfPast.AsOfLimitationNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Giới hạn", asOfPast.AsOfLimitationNote, StringComparison.OrdinalIgnoreCase);

        var asOfVnd = Assert.Single(asOfPast.ByCurrency);
        Assert.Equal(10000m, asOfVnd.RevenueBestAvailable);
        Assert.Equal(10000m, asOfVnd.RevenueMaturity.ExpectedTotal);
        Assert.Equal(0m, asOfVnd.RevenueMaturity.ConfirmedTotal);
        Assert.Equal(0m, asOfVnd.RevenueMaturity.ActualTotal);
        Assert.Equal(4000m, asOfVnd.DirectCostBestAvailable);
        Assert.Equal(0m, asOfVnd.DirectCostMaturity.ConfirmedTotal);

        // Backdate recognition + keep finalize "now" so asOf mid-window sees full outstanding
        await BackdatePayableRecognitionAsync(tenantId, apId, DateTimeOffset.UtcNow.AddDays(-5));
        var asOfMid = await GetFinancialProfileAsync(
            tenantId,
            billId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)));
        var asOfSettle = Assert.Single(asOfMid.SettlementOutstanding);
        Assert.Equal(2000m, asOfSettle.AccountsPayableOutstanding);
    }

    [Fact]
    public async Task Dashboard_CountsVariancesOverdue_AndBaseCurrencyFxStubRollUp()
    {
        var tenantId = await CreateTenantAsync("TN-E13F-DASH", "Dash FULL");
        var billId = await CreateBillAsync(tenantId, "BL-E13F-D", "freight");

        await CreateRevenueAsync(tenantId, billId, 100m, "FREIGHT", "USD", null);
        await CreateDirectCostAsync(tenantId, billId, 40m, "FREIGHT", "USD", null);
        await CreateDirectCostAsync(tenantId, billId, 10000m, "THC", "VND", null);

        await OpenExceptionAsync(
            tenantId,
            "OV-1",
            "high",
            "Quá hạn",
            billId,
            dueAt: DateTimeOffset.UtcNow.AddHours(-2));
        await OpenExceptionAsync(
            tenantId,
            "OK-1",
            "low",
            "Còn hạn",
            billId,
            dueAt: DateTimeOffset.UtcNow.AddDays(2));

        var reconId = await StartReconciliationAsync(tenantId, billId);
        await AddReconciliationDetailAsync(tenantId, reconId, "payment", await CreatePaymentAsync(tenantId, billId, 10m, "VND"), 10m, 0m);

        var summary = await GetDashboardSummaryAsync(tenantId);
        Assert.Equal(1, summary.BillCount);
        Assert.Equal(2, summary.OpenExceptionCount);
        Assert.Equal(1, summary.OverdueExceptionCount);
        Assert.True(summary.OpenVarianceCount >= 1);
        Assert.True(summary.HasMixedCurrencies);
        Assert.NotNull(summary.BaseCurrencyRollUp);
        Assert.Equal("VND", summary.BaseCurrencyRollUp!.BaseCurrency);
        // USD 100 × 25000 + VND costs: revenue base = 2_500_000; cost = 40×25000 + 10000 = 1_010_000
        Assert.Equal(2_500_000m, summary.BaseCurrencyRollUp.RevenueBestAvailableBase);
        Assert.Equal(1_010_000m, summary.BaseCurrencyRollUp.CostBestAvailableBase);
        Assert.Equal(1_490_000m, summary.BaseCurrencyRollUp.ProfitBestAvailableBase);
        Assert.Contains("stub", summary.BaseCurrencyRollUp.FxStubNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("điều khiển", summary.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Queues_FilterStatusSeverityOverdueObjectType_RequiredLevel_AndReconciliationsStub()
    {
        var tenantId = await CreateTenantAsync("TN-E13F-Q", "Queues FULL");
        var billId = await CreateBillAsync(tenantId, "BL-E13F-Q", "freight");
        var costId = await CreateDirectCostAsync(tenantId, billId, 50m, "FREIGHT", "VND", null);

        var overdueId = await OpenExceptionAsync(
            tenantId, "Q-OV", "critical", "Quá hạn", billId,
            objectType: "cost", objectId: costId,
            dueAt: DateTimeOffset.UtcNow.AddHours(-1));
        var openId = await OpenExceptionAsync(
            tenantId, "Q-OK", "medium", "Còn hạn", billId,
            objectType: "other", objectId: billId,
            dueAt: DateTimeOffset.UtcNow.AddDays(1));
        var closedId = await OpenExceptionAsync(tenantId, "Q-CL", "low", "Đóng", billId, dueAt: null);
        await ResolveExceptionAsync(tenantId, closedId);
        await CloseExceptionAsync(tenantId, closedId);

        var revId = await CreateRevenueAsync(tenantId, billId, 10m, "FREIGHT", "VND", null);
        var pendingL2 = await RequestApprovalAsync(tenantId, "cost", costId, requiredLevel: 2);
        var pendingL1 = await RequestApprovalAsync(tenantId, "revenue", revId, requiredLevel: 1);
        Assert.NotEqual(Guid.Empty, pendingL1);
        Assert.NotEqual(openId, overdueId);

        var exAll = await GetExceptionQueueAsync(tenantId);
        Assert.Equal(2, exAll.Count);
        Assert.DoesNotContain(exAll, e => e.Id == closedId);

        var overdueOnly = await GetExceptionQueueAsync(tenantId, overdueOnly: true);
        Assert.Single(overdueOnly);
        Assert.Equal(overdueId, overdueOnly[0].Id);

        var byObject = await GetExceptionQueueAsync(tenantId, objectType: "cost");
        Assert.Single(byObject);
        Assert.Equal(overdueId, byObject[0].Id);

        var critical = await GetExceptionQueueAsync(tenantId, severity: "critical");
        Assert.Single(critical);

        var closedQueue = await GetExceptionQueueAsync(tenantId, status: "closed");
        Assert.Single(closedQueue);
        Assert.Equal(closedId, closedQueue[0].Id);

        var approvalsDefault = await GetApprovalQueueAsync(tenantId);
        Assert.Equal(2, approvalsDefault.Count);

        var level2 = await GetApprovalQueueAsync(tenantId, requiredLevel: 2);
        Assert.Single(level2);
        Assert.Equal(pendingL2, level2[0].Id);
        Assert.Equal(2, level2[0].RequiredLevel);

        var byType = await GetApprovalQueueAsync(tenantId, objectType: "cost");
        Assert.Single(byType);

        var reconId = await StartReconciliationAsync(tenantId, billId);
        var reconQueue = await GetReconciliationQueueAsync(tenantId);
        Assert.Contains(reconQueue, r => r.Id == reconId);
        Assert.Contains(reconQueue[0].QueueLabel, "Đối soát");

        var tenantB = await CreateTenantAsync("TN-E13F-QB", "Queues B");
        Assert.Empty(await GetExceptionQueueAsync(tenantB));
        Assert.Empty(await GetApprovalQueueAsync(tenantB));
        Assert.Empty(await GetReconciliationQueueAsync(tenantB));
    }

    [Fact]
    public async Task CloseSnapshotPnl_DerivedReadOnly_FromImmutableMetrics()
    {
        var tenantId = await CreateTenantAsync("TN-E13F-PNL", "PNL FULL");
        var billId = await CreateBillAsync(tenantId, "BL-E13F-PNL", "freight");
        await CreateRevenueAsync(tenantId, billId, 5000m, "FREIGHT", "VND", null);
        await CreateDirectCostAsync(tenantId, billId, 2000m, "FREIGHT", "VND", null);

        var closeId = await StartCloseAsync(tenantId, billId);
        var snapshotId = await CreateSnapshotAsync(tenantId, closeId);

        var pnl = await GetClosePnlAsync(tenantId, closeId);
        Assert.Equal(closeId, pnl.FinancialCloseId);
        Assert.Equal(snapshotId, pnl.SnapshotId);
        Assert.Equal(1, pnl.SnapshotVersion);
        Assert.Equal("VND", pnl.BaseCurrency);
        Assert.Equal(5000m, pnl.RevenueTotal);
        Assert.Equal(2000m, pnl.CostTotal);
        Assert.Equal(3000m, pnl.ProfitTotal);
        Assert.Contains("P&L", pnl.Note, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(pnl.Metrics, m => m.MetricKey == "revenue_total");

        // Explicit snapshot id
        var pnlSnap = await GetClosePnlAsync(tenantId, closeId, snapshotId);
        Assert.Equal(snapshotId, pnlSnap.SnapshotId);
        Assert.Equal(pnl.ImmutableHash, pnlSnap.ImmutableHash);

        // Cross-tenant
        var tenantB = await CreateTenantAsync("TN-E13F-PNLB", "PNL B");
        using var cross = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-closes/{closeId}/pnl");
        cross.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(cross)).StatusCode);

        // No snapshot yet
        var bill2 = await CreateBillAsync(tenantId, "BL-E13F-PNL2", "freight");
        var openClose = await StartCloseAsync(tenantId, bill2);
        using var noSnap = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-closes/{openClose}/pnl");
        noSnap.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var missing = await _client.SendAsync(noSnap);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var errJson = await missing.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Contains("bản chốt", errJson.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task BackdatePayableRecognitionAsync(Guid tenantId, Guid apId, DateTimeOffset recognizedAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var ap = await db.AccountsPayable.IgnoreQueryFilters()
            .FirstAsync(a => a.Id == apId && a.TenantId == tenantId);
        ap.RecognizedAt = recognizedAt;
        await db.SaveChangesAsync();
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

    private async Task<Guid> CreateRevenueAsync(
        Guid tenantId,
        Guid billId,
        decimal amount,
        string revenueTypeCode,
        string currencyCode,
        DateOnly? effectiveDate)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount,
                currencyCode,
                revenueTypeCode,
                effectiveDate
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task ConfirmRevenueAsync(Guid tenantId, Guid revenueId, decimal confirmedAmount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task ActualizeRevenueAsync(Guid tenantId, Guid revenueId, decimal actualAmount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueId}/actualize")
        {
            Content = JsonContent.Create(new { actualAmount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> CreateDirectCostAsync(
        Guid tenantId,
        Guid billId,
        decimal amount,
        string costTypeCode,
        string currencyCode,
        DateOnly? effectiveDate)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount,
                currencyCode,
                costTypeCode,
                effectiveDate
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        }

        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task ConfirmCostAsync(Guid tenantId, Guid costId, decimal confirmedAmount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> CreatePayableExposureAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payable-exposures")
        {
            Content = JsonContent.Create(new { amount, currencyCode = "VND", billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> RecognizePayableAsync(Guid tenantId, Guid exposureId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreatePaymentAsync(Guid tenantId, Guid billId, decimal amount, string currency)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new { amount, currencyCode = currency, billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> AllocatePaymentAsync(Guid tenantId, Guid paymentId, Guid apId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/{paymentId}/allocations")
        {
            Content = JsonContent.Create(new { accountsPayableId = apId, amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task FinalizePaymentAllocationAsync(Guid tenantId, Guid allocationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payment-allocations/{allocationId}/finalize");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> OpenExceptionAsync(
        Guid tenantId,
        string ruleCode,
        string severity,
        string title,
        Guid billId,
        DateTimeOffset? dueAt,
        string? objectType = null,
        Guid? objectId = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/exceptions")
        {
            Content = JsonContent.Create(new
            {
                ruleCode,
                severity,
                title,
                billId,
                dueAt,
                objectType,
                objectId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task ResolveExceptionAsync(Guid tenantId, Guid exceptionId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/exceptions/{exceptionId}/resolve")
        {
            Content = JsonContent.Create(new { resolutionNotes = "Xong" })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task CloseExceptionAsync(Guid tenantId, Guid exceptionId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/exceptions/{exceptionId}/close");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> RequestApprovalAsync(
        Guid tenantId,
        string objectType,
        Guid objectId,
        int? requiredLevel = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/approvals")
        {
            Content = JsonContent.Create(new
            {
                objectType,
                objectId,
                requestReason = "Duyệt",
                requiredLevel
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        }

        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> StartReconciliationAsync(Guid tenantId, Guid billId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/reconciliations")
        {
            Content = JsonContent.Create(new { reconciliationType = "manual", billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task AddReconciliationDetailAsync(
        Guid tenantId,
        Guid reconId,
        string sourceType,
        Guid sourceId,
        decimal sourceAmount,
        decimal targetAmount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/reconciliations/{reconId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceType,
                sourceId,
                sourceAmount,
                targetAmount,
                matchedAmount = 0m,
                currencyCode = "VND"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }

    private async Task<Guid> StartCloseAsync(Guid tenantId, Guid billId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/financial-closes")
        {
            Content = JsonContent.Create(new
            {
                scopeType = "bill",
                scopeId = billId,
                periodFrom = new DateOnly(2026, 9, 1),
                periodTo = new DateOnly(2026, 9, 30),
                policyVersion = "controlled",
                baseCurrency = "VND"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateSnapshotAsync(Guid tenantId, Guid closeId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-closes/{closeId}/snapshot");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        }

        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<FinancialProfileResponse> GetFinancialProfileAsync(
        Guid tenantId,
        Guid billId,
        DateOnly? asOf = null)
    {
        var url = asOf.HasValue
            ? $"/api/bills/{billId}/financial-profile?asOf={asOf.Value:yyyy-MM-dd}"
            : $"/api/bills/{billId}/financial-profile";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<FinancialProfileResponse>(JsonOptions))!;
    }

    private async Task<DashboardSummaryResponse> GetDashboardSummaryAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/summary");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<DashboardSummaryResponse>(JsonOptions))!;
    }

    private async Task<List<ExceptionQueueItem>> GetExceptionQueueAsync(
        Guid tenantId,
        string? status = null,
        string? severity = null,
        bool? overdueOnly = null,
        string? objectType = null)
    {
        var qs = new List<string>();
        if (status is not null) qs.Add($"status={status}");
        if (severity is not null) qs.Add($"severity={severity}");
        if (overdueOnly is not null) qs.Add($"overdueOnly={overdueOnly.Value.ToString().ToLowerInvariant()}");
        if (objectType is not null) qs.Add($"objectType={objectType}");
        var url = qs.Count == 0 ? "/api/queues/exceptions" : "/api/queues/exceptions?" + string.Join("&", qs);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<ExceptionQueueItem>>(JsonOptions))!;
    }

    private async Task<List<ApprovalQueueItem>> GetApprovalQueueAsync(
        Guid tenantId,
        string? status = null,
        string? objectType = null,
        int? requiredLevel = null)
    {
        var qs = new List<string>();
        if (status is not null) qs.Add($"status={status}");
        if (objectType is not null) qs.Add($"objectType={objectType}");
        if (requiredLevel is not null) qs.Add($"requiredLevel={requiredLevel}");
        var url = qs.Count == 0 ? "/api/queues/approvals" : "/api/queues/approvals?" + string.Join("&", qs);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<ApprovalQueueItem>>(JsonOptions))!;
    }

    private async Task<List<ReconciliationQueueItem>> GetReconciliationQueueAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/queues/reconciliations");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<ReconciliationQueueItem>>(JsonOptions))!;
    }

    private async Task<ClosePnlResponse> GetClosePnlAsync(
        Guid tenantId,
        Guid closeId,
        Guid? snapshotId = null)
    {
        var url = snapshotId.HasValue
            ? $"/api/financial-closes/{closeId}/pnl?snapshotId={snapshotId}"
            : $"/api/financial-closes/{closeId}/pnl";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ClosePnlResponse>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record MaturityBreakdown(
        decimal ExpectedTotal,
        decimal ConfirmedTotal,
        decimal ActualTotal);

    private sealed record CurrencyBucket(
        string CurrencyCode,
        decimal RevenueBestAvailable,
        decimal CostBestAvailable,
        decimal ProfitBestAvailable,
        decimal DirectCostBestAvailable,
        decimal AllocatedCostAmount,
        MaturityBreakdown RevenueMaturity,
        MaturityBreakdown DirectCostMaturity);

    private sealed record SettlementOutstandingBucket(
        string CurrencyCode,
        decimal AccountsPayableOutstanding,
        decimal AccountsReceivableOutstanding);

    private sealed record FinancialProfileResponse(
        Guid BillId,
        string BillNo,
        string ViewKind,
        DateTimeOffset AsOfTimestamp,
        DateOnly? AsOfFilter,
        List<CurrencyBucket> ByCurrency,
        List<SettlementOutstandingBucket> SettlementOutstanding,
        bool HasMixedCurrencies,
        string Note,
        string? AsOfLimitationNote);

    private sealed record DashboardBaseCurrencyRollUp(
        string BaseCurrency,
        decimal CostBestAvailableBase,
        decimal RevenueBestAvailableBase,
        decimal ProfitBestAvailableBase,
        string FxStubNote);

    private sealed record DashboardSummaryResponse(
        DateTimeOffset AsOfTimestamp,
        int BillCount,
        int OpenExceptionCount,
        int PendingApprovalCount,
        int OpenCloseCount,
        int OpenVarianceCount,
        int OverdueExceptionCount,
        List<object> TotalsByCurrency,
        DashboardBaseCurrencyRollUp? BaseCurrencyRollUp,
        bool HasMixedCurrencies,
        string Note);

    private sealed record ExceptionQueueItem(
        Guid Id,
        string RuleCode,
        string Severity,
        string Status,
        string Title,
        Guid? BillId,
        string? ObjectType);

    private sealed record ApprovalQueueItem(
        Guid Id,
        string ObjectType,
        Guid ObjectId,
        string Status,
        int RequiredLevel);

    private sealed record ReconciliationQueueItem(
        Guid Id,
        string Status,
        Guid? BillId,
        string QueueLabel);

    private sealed record ClosePnlMetric(string MetricKey, decimal MetricValue);

    private sealed record ClosePnlResponse(
        Guid FinancialCloseId,
        Guid SnapshotId,
        int SnapshotVersion,
        string BaseCurrency,
        DateTimeOffset ClosedAt,
        string ImmutableHash,
        decimal RevenueTotal,
        decimal CostTotal,
        decimal ProfitTotal,
        decimal ApOutstandingTotal,
        decimal ArOutstandingTotal,
        List<ClosePnlMetric> Metrics,
        string Note);
}
