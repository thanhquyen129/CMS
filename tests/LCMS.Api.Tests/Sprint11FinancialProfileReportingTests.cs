using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint11FinancialProfileReportingTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint11FinancialProfileReportingTests(LcmsApiFactory factory)
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
    public async Task FinancialProfile_MaturityAllocatedSettlement_AndAsOfFilter()
    {
        var tenantId = await CreateTenantAsync("TN-E13-PROF", "Profile Reporting");
        var billId = await CreateBillAsync(tenantId, "BL-E13-1", "freight");
        var otherBill = await CreateBillAsync(tenantId, "BL-E13-2", "freight");

        // Revenue: Expected 10000 → Confirmed 11000 (maturity layers preserved)
        var revId = await CreateRevenueAsync(tenantId, billId, 10000m, "FREIGHT", "VND", new DateOnly(2026, 8, 1));
        await ConfirmRevenueAsync(tenantId, revId, 11000m);

        // Future-dated revenue excluded by asOf
        await CreateRevenueAsync(tenantId, billId, 500m, "SURCHARGE", "VND", new DateOnly(2026, 10, 1));

        // Direct cost Expected 3000
        await CreateDirectCostAsync(tenantId, billId, 3000m, "FREIGHT", new DateOnly(2026, 8, 15));

        // Shared → allocate 60 to this bill, 40 to other
        var sharedId = await CreateSharedCostAsync(tenantId, 100m, "TERMINAL");
        var allocationId = await CreateAllocationAsync(tenantId, sharedId, billId, 60m, otherBill, 40m);
        await FinalizeAllocationAsync(tenantId, allocationId);

        // Settlement outstanding via AP/AR linked to Bill
        var apExp = await CreatePayableExposureAsync(tenantId, billId, 2000m);
        await RecognizePayableAsync(tenantId, apExp, 2000m);
        var arExp = await CreateReceivableExposureAsync(tenantId, billId, 5000m);
        await RecognizeReceivableAsync(tenantId, arExp, 5000m);

        var profile = await GetFinancialProfileAsync(tenantId, billId);
        Assert.Equal("best_available", profile.ViewKind);
        Assert.True(profile.AsOfTimestamp > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.Null(profile.AsOfFilter);
        Assert.Null(profile.AsOfLimitationNote);
        Assert.Contains("Bill", profile.Note, StringComparison.OrdinalIgnoreCase);

        var vnd = Assert.Single(profile.ByCurrency);
        Assert.Equal("VND", vnd.CurrencyCode);
        // Best Available revenue = confirmed 11000 + expected 500
        Assert.Equal(11500m, vnd.RevenueBestAvailable);
        Assert.Equal(10500m, vnd.RevenueMaturity.ExpectedTotal);
        Assert.Equal(11000m, vnd.RevenueMaturity.ConfirmedTotal);
        Assert.Equal(0m, vnd.RevenueMaturity.ActualTotal);
        Assert.Equal(2, vnd.RevenueLineCount);
        Assert.Equal(3000m, vnd.DirectCostBestAvailable);
        Assert.Equal(3000m, vnd.DirectCostMaturity.ExpectedTotal);
        Assert.Equal(60m, vnd.AllocatedCostAmount);
        Assert.Equal(3060m, vnd.CostBestAvailable);
        Assert.Equal(8440m, vnd.ProfitBestAvailable); // 11500 - 3060

        var settlement = Assert.Single(profile.SettlementOutstanding);
        Assert.Equal("VND", settlement.CurrencyCode);
        Assert.Equal(2000m, settlement.AccountsPayableOutstanding);
        Assert.Equal(5000m, settlement.AccountsReceivableOutstanding);

        // asOf mid-period: drops Oct revenue; reconstruct maturity (confirm "now" after asOf → Expected only)
        var asOfProfile = await GetFinancialProfileAsync(tenantId, billId, new DateOnly(2026, 9, 1));
        Assert.Equal(new DateOnly(2026, 9, 1), asOfProfile.AsOfFilter);
        Assert.NotNull(asOfProfile.AsOfLimitationNote);
        Assert.Contains("asOf", asOfProfile.AsOfLimitationNote, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ConfirmedAt", asOfProfile.AsOfLimitationNote, StringComparison.OrdinalIgnoreCase);

        var asOfVnd = Assert.Single(asOfProfile.ByCurrency);
        Assert.Equal(10000m, asOfVnd.RevenueBestAvailable);
        Assert.Equal(10000m, asOfVnd.RevenueMaturity.ExpectedTotal);
        Assert.Equal(0m, asOfVnd.RevenueMaturity.ConfirmedTotal);
        Assert.Equal(1, asOfVnd.RevenueLineCount);
        Assert.Equal(3000m, asOfVnd.DirectCostBestAvailable);
        // Allocation finalized "now" is after asOf → excluded by FinalizedAt filter
        Assert.Equal(0m, asOfVnd.AllocatedCostAmount);
        Assert.Equal(3000m, asOfVnd.CostBestAvailable);
    }

    [Fact]
    public async Task Dashboard_Summary_TenantIsolated_AndCountsTotals()
    {
        var tenantA = await CreateTenantAsync("TN-E13-A", "Dash A");
        var tenantB = await CreateTenantAsync("TN-E13-B", "Dash B");

        var billA1 = await CreateBillAsync(tenantA, "BL-A1", "freight");
        var billA2 = await CreateBillAsync(tenantA, "BL-A2", "freight");
        var billB = await CreateBillAsync(tenantB, "BL-B1", "freight");

        await CreateRevenueAsync(tenantA, billA1, 1000m, "FREIGHT", "VND", null);
        await CreateDirectCostAsync(tenantA, billA1, 400m, "FREIGHT", null);
        await CreateRevenueAsync(tenantB, billB, 9999m, "FREIGHT", "VND", null);

        var openExA = await OpenExceptionAsync(tenantA, "RULE-A", "high", "Ngoại lệ mở A", billA1);
        var closedExA = await OpenExceptionAsync(tenantA, "RULE-A2", "low", "Ngoại lệ đóng A", billA2);
        await ResolveExceptionAsync(tenantA, closedExA);
        await CloseExceptionAsync(tenantA, closedExA);

        var costA = await CreateDirectCostAsync(tenantA, billA1, 50m, "THC", null);
        var pendingApproval = await RequestApprovalAsync(tenantA, "cost", costA);
        var costA2 = await CreateDirectCostAsync(tenantA, billA2, 25m, "DOC", null);
        var approvedId = await RequestApprovalAsync(tenantA, "cost", costA2);
        await ApproveAsync(tenantA, approvedId);

        await StartCloseAsync(tenantA, billA1); // open
        await StartCloseAsync(tenantB, billB); // other tenant

        var summaryA = await GetDashboardSummaryAsync(tenantA);
        Assert.Equal(2, summaryA.BillCount);
        Assert.Equal(1, summaryA.OpenExceptionCount);
        Assert.Equal(1, summaryA.PendingApprovalCount);
        Assert.Equal(1, summaryA.OpenCloseCount);
        Assert.Equal(0, summaryA.OpenVarianceCount);
        Assert.Equal(0, summaryA.OverdueExceptionCount);
        Assert.NotNull(summaryA.BaseCurrencyRollUp);
        Assert.Equal("VND", summaryA.BaseCurrencyRollUp.BaseCurrency);
        Assert.False(summaryA.HasMixedCurrencies);

        var totals = Assert.Single(summaryA.TotalsByCurrency);
        Assert.Equal("VND", totals.CurrencyCode);
        // Costs: 400 + 50 + 25 = 475; Revenue 1000; Profit 525
        Assert.Equal(475m, totals.CostBestAvailable);
        Assert.Equal(1000m, totals.RevenueBestAvailable);
        Assert.Equal(525m, totals.ProfitBestAvailable);
        Assert.Contains("điều khiển", summaryA.Note, StringComparison.OrdinalIgnoreCase);

        var summaryB = await GetDashboardSummaryAsync(tenantB);
        Assert.Equal(1, summaryB.BillCount);
        Assert.Equal(0, summaryB.OpenExceptionCount);
        Assert.Equal(0, summaryB.PendingApprovalCount);
        Assert.Equal(1, summaryB.OpenCloseCount);
        var totalsB = Assert.Single(summaryB.TotalsByCurrency);
        Assert.Equal(9999m, totalsB.RevenueBestAvailable);
        Assert.Equal(0m, totalsB.CostBestAvailable);

        // Profile cross-tenant
        using var profileAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billA1}/financial-profile");
        profileAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(profileAsB)).StatusCode);

        // Dashboard without tenant rejected
        using var noTenant = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/summary");
        var missing = await _client.SendAsync(noTenant);
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);

        // Ensure closed exception / approved approval not counted for A
        Assert.NotEqual(openExA, closedExA);
        Assert.NotEqual(pendingApproval, approvedId);
    }

    [Fact]
    public async Task ControlQueues_FilterOpenExceptions_AndPendingApprovals_Only()
    {
        var tenantId = await CreateTenantAsync("TN-E13-Q", "Queues");
        var billId = await CreateBillAsync(tenantId, "BL-Q1", "freight");

        var openId = await OpenExceptionAsync(tenantId, "Q-OPEN", "critical", "Mở", billId);
        var closedId = await OpenExceptionAsync(tenantId, "Q-CLOSED", "medium", "Đóng", billId);
        await ResolveExceptionAsync(tenantId, closedId);
        await CloseExceptionAsync(tenantId, closedId);

        var cost1 = await CreateDirectCostAsync(tenantId, billId, 10m, "FREIGHT", null);
        var cost2 = await CreateDirectCostAsync(tenantId, billId, 20m, "THC", null);
        var pendingId = await RequestApprovalAsync(tenantId, "cost", cost1);
        var approvedId = await RequestApprovalAsync(tenantId, "cost", cost2);
        await ApproveAsync(tenantId, approvedId);

        var exceptionQueue = await GetExceptionQueueAsync(tenantId);
        Assert.Single(exceptionQueue);
        Assert.Equal(openId, exceptionQueue[0].Id);
        Assert.Equal("open", exceptionQueue[0].Status);
        Assert.DoesNotContain(exceptionQueue, e => e.Id == closedId);

        var criticalOnly = await GetExceptionQueueAsync(tenantId, severity: "critical");
        Assert.Single(criticalOnly);
        Assert.Equal(openId, criticalOnly[0].Id);

        var mediumOnly = await GetExceptionQueueAsync(tenantId, severity: "medium");
        Assert.Empty(mediumOnly);

        var approvalQueue = await GetApprovalQueueAsync(tenantId);
        Assert.Single(approvalQueue);
        Assert.Equal(pendingId, approvalQueue[0].Id);
        Assert.Equal("pending", approvalQueue[0].Status);
        Assert.DoesNotContain(approvalQueue, a => a.Id == approvedId);

        // Cross-tenant isolation on queues
        var tenantB = await CreateTenantAsync("TN-E13-QB", "Queues B");
        Assert.Empty(await GetExceptionQueueAsync(tenantB));
        Assert.Empty(await GetApprovalQueueAsync(tenantB));
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

    private async Task<Guid> CreateDirectCostAsync(
        Guid tenantId,
        Guid billId,
        decimal amount,
        string costTypeCode,
        DateOnly? effectiveDate)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount,
                currencyCode = "VND",
                costTypeCode,
                effectiveDate
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateSharedCostAsync(Guid tenantId, decimal amount, string costTypeCode)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                attributionType = "shared",
                amount,
                currencyCode = "VND",
                costTypeCode
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateAllocationAsync(
        Guid tenantId,
        Guid costId,
        Guid billA,
        decimal basisA,
        Guid billB,
        decimal basisB)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/allocations")
        {
            Content = JsonContent.Create(new
            {
                allocationBasis = "quantity",
                details = new[]
                {
                    new { billId = billA, basisValue = basisA },
                    new { billId = billB, basisValue = basisB }
                }
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task FinalizeAllocationAsync(Guid tenantId, Guid allocationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/finalize");
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

    private async Task<Guid> CreateReceivableExposureAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/receivable-exposures")
        {
            Content = JsonContent.Create(new { amount, currencyCode = "VND", billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task RecognizePayableAsync(Guid tenantId, Guid exposureId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task RecognizeReceivableAsync(Guid tenantId, Guid exposureId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/receivable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> OpenExceptionAsync(
        Guid tenantId,
        string ruleCode,
        string severity,
        string title,
        Guid billId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/exceptions")
        {
            Content = JsonContent.Create(new
            {
                ruleCode,
                severity,
                title,
                billId,
                dueAt = DateTimeOffset.UtcNow.AddDays(2)
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

    private async Task<Guid> RequestApprovalAsync(Guid tenantId, string objectType, Guid objectId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/approvals")
        {
            Content = JsonContent.Create(new { objectType, objectId, requestReason = "Duyệt" })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task ApproveAsync(Guid tenantId, Guid approvalId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/approvals/{approvalId}/approve")
        {
            Content = JsonContent.Create(new { decisionReason = "OK" })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task StartCloseAsync(Guid tenantId, Guid billId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/financial-closes")
        {
            Content = JsonContent.Create(new
            {
                scopeType = "bill",
                scopeId = billId,
                periodFrom = new DateOnly(2026, 9, 1),
                periodTo = new DateOnly(2026, 9, 30)
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
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
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{(int)res.StatusCode} {res.ReasonPhrase}: {body}");
        }

        return (await res.Content.ReadFromJsonAsync<FinancialProfileResponse>(JsonOptions))!;
    }

    private async Task<DashboardSummaryResponse> GetDashboardSummaryAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard/summary");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        }
        return (await res.Content.ReadFromJsonAsync<DashboardSummaryResponse>(JsonOptions))!;
    }

    private async Task<List<ExceptionQueueItem>> GetExceptionQueueAsync(Guid tenantId, string? severity = null)
    {
        var url = severity is null
            ? "/api/queues/exceptions"
            : $"/api/queues/exceptions?severity={severity}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{(int)res.StatusCode} {res.ReasonPhrase}: {body}");
        }

        return (await res.Content.ReadFromJsonAsync<List<ExceptionQueueItem>>(JsonOptions))!;
    }

    private async Task<List<ApprovalQueueItem>> GetApprovalQueueAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/queues/approvals");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<ApprovalQueueItem>>(JsonOptions))!;
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
        MaturityBreakdown DirectCostMaturity,
        int RevenueLineCount,
        int DirectCostLineCount,
        int AllocatedCostLineCount);

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

    private sealed record DashboardCurrencyTotals(
        string CurrencyCode,
        decimal CostBestAvailable,
        decimal RevenueBestAvailable,
        decimal ProfitBestAvailable);

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
        List<DashboardCurrencyTotals> TotalsByCurrency,
        DashboardBaseCurrencyRollUp? BaseCurrencyRollUp,
        bool HasMixedCurrencies,
        string Note);

    private sealed record ExceptionQueueItem(
        Guid Id,
        string RuleCode,
        string Severity,
        string Status,
        string Title,
        Guid? BillId);

    private sealed record ApprovalQueueItem(
        Guid Id,
        string ObjectType,
        Guid ObjectId,
        string Status);
}
