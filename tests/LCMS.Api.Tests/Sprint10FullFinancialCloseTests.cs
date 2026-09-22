using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint10FullFinancialCloseTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint10FullFinancialCloseTests(LcmsApiFactory factory)
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
    public async Task EligibilityGates_BlockSnapshot_WithDistinctVietnameseReasons()
    {
        var tenantId = await CreateTenantAsync("TN-E12F-ELIG", "Close FULL Eligibility");
        var billId = await CreateBillAsync(tenantId, "BL-E12F-ELIG", "freight");

        // (a) Critical open exception
        var closeCrit = await StartCloseAsync(tenantId, "bill", billId, policy: "controlled");
        await OpenCriticalExceptionAsync(tenantId, billId, "Ngoại lệ chặn chốt FULL");
        await AssertSnapshotBlockedAsync(tenantId, closeCrit, "ngoại lệ mức nghiêm trọng");

        // Fresh bill for document gate (avoid leftover critical exception)
        var billDoc = await CreateBillAsync(tenantId, "BL-E12F-DOC", "freight");
        var closeDoc = await StartCloseAsync(tenantId, "bill", billDoc, policy: "controlled");
        var docId = await ReceiveDocumentAsync(tenantId, billDoc, "INV-E12F-1", 500m);
        await AddLineAsync(tenantId, docId, 500m, "gate line");
        await AcceptDocumentAsync(tenantId, docId);
        await AssertSnapshotBlockedAsync(tenantId, closeDoc, "chứng từ đã chấp nhận nhưng chưa khớp");

        // (c) Unsettled AP above threshold (default 0)
        var billAp = await CreateBillAsync(tenantId, "BL-E12F-AP", "freight");
        var closeAp = await StartCloseAsync(tenantId, "bill", billAp, policy: "controlled");
        await RecognizePayableAsync(
            tenantId,
            await CreatePayableExposureAsync(tenantId, billAp, 800m),
            800m);
        await AssertSnapshotBlockedAsync(tenantId, closeAp, "chưa tất toán với số dư mở");

        // Clean bill — eligibility passes
        var billOk = await CreateBillAsync(tenantId, "BL-E12F-OK", "freight");
        await CreateDirectCostAsync(tenantId, billOk, 100m, "FREIGHT");
        var closeOk = await StartCloseAsync(tenantId, "bill", billOk, policy: "strict");
        var snapId = await CreateSnapshotAsync(tenantId, closeOk);
        var close = await GetCloseAsync(tenantId, closeOk);
        Assert.Equal("locked", close.Status);
        Assert.Equal("strict", close.PolicyVersion);
        Assert.Equal("strict", (await GetSnapshotAsync(tenantId, snapId)).PolicyVersion);
    }

    [Fact]
    public async Task PeriodLock_BlocksConfirmAndAllocate_UntilReopen_SnapshotsImmutable()
    {
        var tenantId = await CreateTenantAsync("TN-E12F-LOCK", "Close FULL Period Lock");
        var billId = await CreateBillAsync(tenantId, "BL-E12F-LOCK", "freight");
        var costId = await CreateDirectCostAsync(tenantId, billId, 1_000m, "FREIGHT");
        var revenueId = await CreateRevenueAsync(tenantId, billId, 1_500m, "FREIGHT");

        var closeId = await StartCloseAsync(
            tenantId,
            "bill",
            billId,
            policy: "controlled",
            periodFrom: new DateOnly(2026, 9, 1),
            periodTo: new DateOnly(2026, 9, 30));
        var snap1 = await CreateSnapshotAsync(tenantId, closeId);
        var hash1 = (await GetSnapshotAsync(tenantId, snap1)).ImmutableHash;
        Assert.Equal("locked", (await GetCloseAsync(tenantId, closeId)).Status);

        // Confirm blocked while locked
        using (var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 1_000m })
        })
        {
            confirm.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(confirm);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Equal("period_locked", err!.Code);
            Assert.Contains("đã khóa chốt", err.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("xác nhận chi phí", err.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var confirmRev = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 1_500m })
        })
        {
            confirmRev.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(confirmRev);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("đã khóa chốt", err!.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Allocate blocked while locked (create AP/payment after lock — create allowed, allocate not)
        var apId = await RecognizePayableAsync(
            tenantId,
            await CreatePayableExposureAsync(tenantId, billId, 400m),
            400m);
        var paymentId = await CreatePaymentAsync(tenantId, billId, 400m);
        using (var alloc = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/{paymentId}/allocations")
        {
            Content = JsonContent.Create(new { accountsPayableId = apId, amount = 400m })
        })
        {
            alloc.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(alloc);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("đã khóa chốt", err!.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("phân bổ thanh toán", err.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Reopen — snapshots untouched; mutations allowed again
        await ReopenCloseAsync(tenantId, closeId, "Điều chỉnh sau khóa kỳ");
        Assert.Equal("reopened", (await GetCloseAsync(tenantId, closeId)).Status);
        Assert.Equal(hash1, (await GetSnapshotAsync(tenantId, snap1)).ImmutableHash);

        using (var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 1_000m })
        })
        {
            confirm.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(confirm)).StatusCode);
        }

        Guid allocationId;
        using (var alloc = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/{paymentId}/allocations")
        {
            Content = JsonContent.Create(new { accountsPayableId = apId, amount = 400m })
        })
        {
            alloc.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var allocRes = await _client.SendAsync(alloc);
            allocRes.EnsureSuccessStatusCode();
            allocationId = (await allocRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        // Settle AP so eligibility unsettled gate passes on re-snapshot
        await FinalizePaymentAllocationAsync(tenantId, allocationId);

        // Reclose appends snapshot v2 — v1 immutable
        var snap2 = await CreateSnapshotAsync(tenantId, closeId);
        Assert.NotEqual(snap1, snap2);
        Assert.Equal(1, (await GetSnapshotAsync(tenantId, snap1)).SnapshotVersion);
        Assert.Equal(hash1, (await GetSnapshotAsync(tenantId, snap1)).ImmutableHash);
        Assert.Equal(2, (await GetSnapshotAsync(tenantId, snap2)).SnapshotVersion);
    }

    [Fact]
    public async Task StrictPolicy_AndTenantIsolation_RecloseAppendsVersion()
    {
        var tenantA = await CreateTenantAsync("TN-E12F-A", "Close FULL A");
        var tenantB = await CreateTenantAsync("TN-E12F-B", "Close FULL B");
        var billA = await CreateBillAsync(tenantA, "BL-E12F-A", "freight");
        var billB = await CreateBillAsync(tenantB, "BL-E12F-B", "freight");

        var closeA = await StartCloseAsync(tenantA, "bill", billA, policy: "strict");
        Assert.Equal("strict", (await GetCloseAsync(tenantA, closeA)).PolicyVersion);
        var snapA = await CreateSnapshotAsync(tenantA, closeA);

        var closeB = await StartCloseAsync(tenantB, "bill", billB, policy: "controlled");
        await CreateSnapshotAsync(tenantB, closeB);

        using (var req = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-closes/{closeA}"))
        {
            req.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(req)).StatusCode);
        }

        using (var req = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-close-snapshots/{snapA}"))
        {
            req.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(req)).StatusCode);
        }

        // Strict locked close blocks confirm even conceptually (period lock forced)
        var costA = await CreateDirectCostAsync(tenantA, billA, 50m, "THC");
        using (var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costA}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 50m })
        })
        {
            confirm.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var res = await _client.SendAsync(confirm);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("đã khóa chốt", err!.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Reclose → new VersionNo; old snapshot preserved
        var closeA2 = await StartCloseAsync(
            tenantA,
            "bill",
            billA,
            policy: "strict",
            supersedesCloseId: closeA);
        var dto = await GetCloseAsync(tenantA, closeA2);
        Assert.Equal(2, dto.VersionNo);
        Assert.Equal(closeA, dto.SupersedesCloseId);
        Assert.Equal("strict", dto.PolicyVersion);
        Assert.Equal("reopened", (await GetCloseAsync(tenantA, closeA)).Status);
        Assert.Single(await ListSnapshotsAsync(tenantA, closeA));
        Assert.Equal(1, (await GetSnapshotAsync(tenantA, snapA)).SnapshotVersion);
    }

    private async Task AssertSnapshotBlockedAsync(Guid tenantId, Guid closeId, string expectedViFragment)
    {
        using var snap = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-closes/{closeId}/snapshot");
        snap.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(snap);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var err = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("concurrency_conflict", err!.Code);
        Assert.Contains(expectedViFragment, err.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("open", (await GetCloseAsync(tenantId, closeId)).Status);
        Assert.Empty(await ListSnapshotsAsync(tenantId, closeId));
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
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateDirectCostAsync(Guid tenantId, Guid billId, decimal amount, string costTypeCode)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount,
                currencyCode = "VND",
                costTypeCode
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateRevenueAsync(Guid tenantId, Guid billId, decimal amount, string revenueTypeCode)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount,
                currencyCode = "VND",
                revenueTypeCode
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task OpenCriticalExceptionAsync(Guid tenantId, Guid billId, string title)
    {
        using var exReq = new HttpRequestMessage(HttpMethod.Post, "/api/exceptions")
        {
            Content = JsonContent.Create(new
            {
                ruleCode = "CLOSE_BLOCK_FULL",
                severity = "critical",
                title,
                billId
            })
        };
        exReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(exReq)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> ReceiveDocumentAsync(Guid tenantId, Guid billId, string documentNo, decimal totalAmount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/financial-documents")
        {
            Content = JsonContent.Create(new
            {
                documentType = "invoice",
                documentNo,
                direction = "payable",
                totalAmount,
                currencyCode = "VND",
                billId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> AddLineAsync(Guid tenantId, Guid documentId, decimal amount, string description)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{documentId}/lines")
        {
            Content = JsonContent.Create(new { amount, description })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task AcceptDocumentAsync(Guid tenantId, Guid documentId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{documentId}/accept");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> CreatePayableExposureAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payable-exposures")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount,
                currencyCode = "VND"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
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

    private async Task FinalizePaymentAllocationAsync(Guid tenantId, Guid allocationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payment-allocations/{allocationId}/finalize");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> StartCloseAsync(
        Guid tenantId,
        string scopeType,
        Guid? scopeId,
        string policy = "controlled",
        DateOnly? periodFrom = null,
        DateOnly? periodTo = null,
        Guid? supersedesCloseId = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/financial-closes")
        {
            Content = JsonContent.Create(new
            {
                scopeType,
                scopeId,
                periodFrom,
                periodTo,
                policyVersion = policy,
                baseCurrency = "VND",
                supersedesCloseId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateSnapshotAsync(Guid tenantId, Guid closeId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-closes/{closeId}/snapshot");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task ReopenCloseAsync(Guid tenantId, Guid closeId, string reason)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-closes/{closeId}/reopen")
        {
            Content = JsonContent.Create(new { reason })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<FinancialCloseDto> GetCloseAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-closes/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FinancialCloseDto>(JsonOptions))!;
    }

    private async Task<List<FinancialCloseSnapshotDto>> ListSnapshotsAsync(Guid tenantId, Guid closeId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-closes/{closeId}/snapshots");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<FinancialCloseSnapshotDto>>(JsonOptions))!;
    }

    private async Task<FinancialCloseSnapshotDto> GetSnapshotAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-close-snapshots/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FinancialCloseSnapshotDto>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);

    private sealed record FinancialCloseSnapshotDetailDto(
        Guid Id,
        Guid SnapshotId,
        int LineNo,
        string MetricKey,
        decimal MetricValue,
        string? CurrencyCode,
        string? SourceType,
        Guid? SourceId,
        string? Notes);

    private sealed record FinancialCloseSnapshotDto(
        Guid Id,
        Guid FinancialCloseId,
        string ScopeType,
        Guid? ScopeId,
        int SnapshotVersion,
        DateTimeOffset ClosedAt,
        Guid? ClosedBy,
        string PolicyVersion,
        string BaseCurrency,
        string ImmutableHash,
        List<FinancialCloseSnapshotDetailDto> Details);

    private sealed record FinancialCloseDto(
        Guid Id,
        string ScopeType,
        Guid? ScopeId,
        DateOnly? PeriodFrom,
        DateOnly? PeriodTo,
        int VersionNo,
        string Status,
        string PolicyVersion,
        string BaseCurrency,
        string? Notes,
        DateTimeOffset? StartedAt,
        Guid? StartedBy,
        DateTimeOffset? LockedAt,
        Guid? LockedBy,
        DateTimeOffset? ReopenedAt,
        Guid? ReopenedBy,
        string? ReopenReason,
        Guid? SupersedesCloseId,
        List<FinancialCloseSnapshotDto> Snapshots);
}
