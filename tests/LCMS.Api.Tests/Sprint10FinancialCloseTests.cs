using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint10FinancialCloseTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint10FinancialCloseTests(LcmsApiFactory factory)
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
    public async Task Snapshot_IsImmutable_AfterClose_AndReopenKeepsHistory()
    {
        var tenantId = await CreateTenantAsync("TN-E12-IMM", "Financial Close Immutability");
        var billId = await CreateBillAsync(tenantId, "BL-E12-1", "freight");
        await CreateDirectCostAsync(tenantId, billId, 1000m, "FREIGHT");
        await CreateRevenueAsync(tenantId, billId, 1500m, "FREIGHT");

        var closeId = await StartCloseAsync(
            tenantId,
            scopeType: "bill",
            scopeId: billId,
            periodFrom: new DateOnly(2026, 9, 1),
            periodTo: new DateOnly(2026, 9, 30));

        var close = await GetCloseAsync(tenantId, closeId);
        Assert.Equal("open", close.Status);
        Assert.Equal(1, close.VersionNo);
        Assert.Empty(close.Snapshots);

        var snapshot1Id = await CreateSnapshotAsync(tenantId, closeId);
        close = await GetCloseAsync(tenantId, closeId);
        Assert.Equal("locked", close.Status);
        Assert.NotNull(close.LockedAt);
        Assert.Single(close.Snapshots);

        var snap1 = await GetSnapshotAsync(tenantId, snapshot1Id);
        Assert.Equal(1, snap1.SnapshotVersion);
        Assert.False(string.IsNullOrWhiteSpace(snap1.ImmutableHash));
        Assert.Contains(snap1.Details, d => d.MetricKey == "cost_total" && d.MetricValue == 1000m);
        Assert.Contains(snap1.Details, d => d.MetricKey == "revenue_total" && d.MetricValue == 1500m);
        var hash1 = snap1.ImmutableHash;
        var closedAt1 = snap1.ClosedAt;
        var detailCount1 = snap1.Details.Count;

        // Second snapshot while locked is rejected (must reopen) — never mutates v1
        using (var lockedSnap = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-closes/{closeId}/snapshot"))
        {
            lockedSnap.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var response = await _client.SendAsync(lockedSnap);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("C-010", body);
        }

        // Reopen does not rewrite snapshot history
        await ReopenCloseAsync(tenantId, closeId, "Điều chỉnh sau chốt");
        close = await GetCloseAsync(tenantId, closeId);
        Assert.Equal("reopened", close.Status);
        Assert.NotNull(close.ReopenedAt);
        Assert.Equal("Điều chỉnh sau chốt", close.ReopenReason);

        var snapshotsAfterReopen = await ListSnapshotsAsync(tenantId, closeId);
        Assert.Single(snapshotsAfterReopen);
        Assert.Equal(snapshot1Id, snapshotsAfterReopen[0].Id);
        Assert.Equal(hash1, snapshotsAfterReopen[0].ImmutableHash);
        Assert.Equal(closedAt1, snapshotsAfterReopen[0].ClosedAt);
        Assert.Equal(detailCount1, snapshotsAfterReopen[0].Details.Count);

        // Add more economic activity then reclose → new snapshot version
        await CreateDirectCostAsync(tenantId, billId, 200m, "THC");
        var snapshot2Id = await CreateSnapshotAsync(tenantId, closeId);
        Assert.NotEqual(snapshot1Id, snapshot2Id);

        var snap2 = await GetSnapshotAsync(tenantId, snapshot2Id);
        Assert.Equal(2, snap2.SnapshotVersion);
        Assert.NotEqual(hash1, snap2.ImmutableHash);
        Assert.Contains(snap2.Details, d => d.MetricKey == "cost_total" && d.MetricValue == 1200m);

        // v1 unchanged (C-010 / AC-008)
        snap1 = await GetSnapshotAsync(tenantId, snapshot1Id);
        Assert.Equal(1, snap1.SnapshotVersion);
        Assert.Equal(hash1, snap1.ImmutableHash);
        Assert.Equal(closedAt1, snap1.ClosedAt);
        Assert.Equal(detailCount1, snap1.Details.Count);
        Assert.Contains(snap1.Details, d => d.MetricKey == "cost_total" && d.MetricValue == 1000m);

        var all = await ListSnapshotsAsync(tenantId, closeId);
        Assert.Equal(2, all.Count);
        Assert.Equal([1, 2], all.Select(s => s.SnapshotVersion).ToArray());

        close = await GetCloseAsync(tenantId, closeId);
        Assert.Equal("locked", close.Status);
    }

    [Fact]
    public async Task CriticalOpenException_BlocksSnapshot_EligibilityStub()
    {
        var tenantId = await CreateTenantAsync("TN-E12-ELIG", "Close Eligibility");
        var billId = await CreateBillAsync(tenantId, "BL-E12-ELIG", "freight");
        var closeId = await StartCloseAsync(tenantId, "bill", billId, null, null);

        using (var exReq = new HttpRequestMessage(HttpMethod.Post, "/api/exceptions")
        {
            Content = JsonContent.Create(new
            {
                ruleCode = "CLOSE_BLOCK",
                severity = "critical",
                title = "Ngoại lệ chặn chốt",
                billId
            })
        })
        {
            exReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
            (await _client.SendAsync(exReq)).EnsureSuccessStatusCode();
        }

        using (var snap = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-closes/{closeId}/snapshot"))
        {
            snap.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var response = await _client.SendAsync(snap);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("concurrency_conflict", body);
        }

        Assert.Equal("open", (await GetCloseAsync(tenantId, closeId)).Status);
        Assert.Empty(await ListSnapshotsAsync(tenantId, closeId));
    }

    [Fact]
    public async Task FinancialClose_CrossTenantIsolated_AndRecloseCreatesNewVersion()
    {
        var tenantA = await CreateTenantAsync("TN-E12-A", "Close A");
        var tenantB = await CreateTenantAsync("TN-E12-B", "Close B");
        var billA = await CreateBillAsync(tenantA, "BL-E12-A", "freight");
        var billB = await CreateBillAsync(tenantB, "BL-E12-B", "freight");

        var closeA = await StartCloseAsync(tenantA, "bill", billA, null, null);
        var snapA = await CreateSnapshotAsync(tenantA, closeA);

        var closeB = await StartCloseAsync(tenantB, "bill", billB, null, null);
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

        using (var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-closes/{closeA}/reopen")
        {
            Content = JsonContent.Create(new { reason = "cross" })
        })
        {
            req.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(req)).StatusCode);
        }

        // Reclose via superseding close → new VersionNo; old snapshot preserved
        var closeA2 = await StartCloseAsync(
            tenantA,
            scopeType: "bill",
            scopeId: billA,
            periodFrom: null,
            periodTo: null,
            supersedesCloseId: closeA);
        var closeA2Dto = await GetCloseAsync(tenantA, closeA2);
        Assert.Equal(2, closeA2Dto.VersionNo);
        Assert.Equal(closeA, closeA2Dto.SupersedesCloseId);
        Assert.Equal("open", closeA2Dto.Status);

        var prior = await GetCloseAsync(tenantA, closeA);
        Assert.Equal("reopened", prior.Status);
        Assert.Single(await ListSnapshotsAsync(tenantA, closeA));

        var snapA2 = await CreateSnapshotAsync(tenantA, closeA2);
        Assert.NotEqual(snapA, snapA2);
        Assert.Equal(1, (await GetSnapshotAsync(tenantA, snapA2)).SnapshotVersion);
        Assert.Equal(1, (await GetSnapshotAsync(tenantA, snapA)).SnapshotVersion);
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

    private async Task CreateDirectCostAsync(Guid tenantId, Guid billId, decimal amount, string costTypeCode)
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
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task CreateRevenueAsync(Guid tenantId, Guid billId, decimal amount, string revenueTypeCode)
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
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> StartCloseAsync(
        Guid tenantId,
        string scopeType,
        Guid? scopeId,
        DateOnly? periodFrom,
        DateOnly? periodTo,
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
                policyVersion = "controlled",
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
