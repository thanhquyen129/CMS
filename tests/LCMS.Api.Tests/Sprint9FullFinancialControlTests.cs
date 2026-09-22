using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint9FullFinancialControlTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint9FullFinancialControlTests(LcmsApiFactory factory)
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
    public async Task BatchRecon_AutoVarianceSeverity_DoesNotOpenException()
    {
        var tenantId = await CreateTenantAsync("TN-E11F-BAT", "FC FULL Batch");
        var billId = await CreateBillAsync(tenantId, "BL-E11F-1", "freight");
        var p1 = await CreatePaymentAsync(tenantId, billId, 50m);
        var p2 = await CreatePaymentAsync(tenantId, billId, 500m);
        var p3 = await CreatePaymentAsync(tenantId, billId, 5_000m);
        var p4 = await CreatePaymentAsync(tenantId, billId, 20_000m);

        var reconId = await StartReconciliationAsync(tenantId, billId, "manual");
        var ids = await AddReconciliationDetailBatchAsync(tenantId, reconId, new[]
        {
            new DetailLine("payment", p1, 50m, 0m, 0m),
            new DetailLine("payment", p2, 500m, 0m, 0m),
            new DetailLine("payment", p3, 5_000m, 0m, 0m),
            new DetailLine("payment", p4, 20_000m, 0m, 0m)
        });

        Assert.Equal(4, ids.Count);
        var recon = await GetReconciliationAsync(tenantId, reconId);
        Assert.Equal(4, recon.Details.Count);
        Assert.All(recon.Details, d => Assert.NotNull(d.VarianceId));

        var variances = await ListVariancesAsync(tenantId);
        Assert.Equal(4, variances.Count);
        Assert.Contains(variances, v => v.Amount == 50m && v.Severity == "low");
        Assert.Contains(variances, v => v.Amount == 500m && v.Severity == "medium");
        Assert.Contains(variances, v => v.Amount == 5_000m && v.Severity == "high");
        Assert.Contains(variances, v => v.Amount == 20_000m && v.Severity == "critical");

        // Variance ≠ Exception — batch never invents inbox items
        Assert.Empty(await ListExceptionsAsync(tenantId));
    }

    [Fact]
    public async Task ExceptionSla_Escalate_MultiStepApproval_IndependentOfPermission()
    {
        var tenantId = await CreateTenantAsync("TN-E11F-SLA", "FC FULL SLA");
        var billId = await CreateBillAsync(tenantId, "BL-E11F-SLA", "freight");
        var costId = await CreateDirectCostAsync(tenantId, billId, 800m, "FREIGHT");

        var roleId = await CreateRoleAsync(tenantId, "viewer", "Viewer");
        var userId = await CreateUserAsync(tenantId, "approver-full@example.com", "Approver Full");
        var deciderId = await CreateUserAsync(tenantId, "decider-full@example.com", "Decider Full");
        await AssignUserRoleAsync(tenantId, userId, roleId);
        await AssignUserRoleAsync(tenantId, deciderId, roleId);

        // SLA: omit dueAt → default hours for critical (8h)
        var exceptionId = await OpenExceptionAsync(
            tenantId,
            ruleCode: "CRIT-OBJ",
            severity: "critical",
            title: "Ngoại lệ gắn chi phí",
            objectType: "cost",
            objectId: costId,
            dueAt: null,
            ownerId: userId);

        var ex = await GetExceptionAsync(tenantId, exceptionId);
        Assert.Equal("open", ex.Status);
        Assert.Equal("critical", ex.Severity);
        Assert.Equal("cost", ex.ObjectType);
        Assert.Equal(costId, ex.ObjectId);
        Assert.Equal(userId, ex.OwnerId);
        Assert.NotNull(ex.DueAt);
        Assert.True(ex.DueAt > DateTimeOffset.UtcNow.AddHours(6));
        Assert.True(ex.DueAt < DateTimeOffset.UtcNow.AddHours(10));

        // Inbox filters
        var criticalInbox = await ListExceptionsAsync(tenantId, status: "open", severity: "critical");
        Assert.Contains(criticalInbox, e => e.Id == exceptionId);
        Assert.Empty(await ListExceptionsAsync(tenantId, status: "open", severity: "low"));
        Assert.Contains(
            await ListExceptionsAsync(tenantId, objectType: "cost"),
            e => e.Id == exceptionId);

        await EscalateExceptionAsync(tenantId, userId, exceptionId, "Quá hạn SLA — leo thang");
        ex = await GetExceptionAsync(tenantId, exceptionId);
        Assert.Equal("escalated", ex.Status);
        Assert.Equal("critical", ex.Severity); // already critical, stays
        Assert.NotNull(ex.EscalatedAt);
        Assert.Equal(userId, ex.EscalatedBy);

        // Multi-step approval (level 2) — Permission ≠ Approval
        var approvalId = await RequestApprovalAsync(tenantId, userId, "cost", costId, "Cần 2 cấp", requiredLevel: 2);
        var pending = await GetApprovalAsync(tenantId, approvalId);
        Assert.Equal("pending", pending.Status);
        Assert.Equal(2, pending.RequiredLevel);
        Assert.Equal(0, pending.CurrentLevel);
        Assert.Equal("pending", (await GetCostAsync(tenantId, costId)).ApprovalStatus);

        await ApproveAsync(tenantId, deciderId, approvalId, "Cấp 1 OK");
        var mid = await GetApprovalAsync(tenantId, approvalId);
        Assert.Equal("pending", mid.Status);
        Assert.Equal(1, mid.CurrentLevel);
        Assert.Equal("pending", (await GetCostAsync(tenantId, costId)).ApprovalStatus);

        await ApproveAsync(tenantId, deciderId, approvalId, "Cấp 2 OK");
        var done = await GetApprovalAsync(tenantId, approvalId);
        Assert.Equal("approved", done.Status);
        Assert.Equal(2, done.CurrentLevel);
        Assert.Equal("approved", (await GetCostAsync(tenantId, costId)).ApprovalStatus);

        // Reject requires reason
        var cost2 = await CreateDirectCostAsync(tenantId, billId, 100m, "THC");
        var approval2 = await RequestApprovalAsync(tenantId, userId, "cost", cost2, null, requiredLevel: 1);
        using (var badReject = new HttpRequestMessage(HttpMethod.Post, $"/api/approvals/{approval2}/reject")
        {
            Content = JsonContent.Create(new { decisionReason = (string?)null })
        })
        {
            badReject.Headers.Add("X-Tenant-Id", tenantId.ToString());
            badReject.Headers.Add("X-User-Id", userId.ToString());
            var res = await _client.SendAsync(badReject);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        }

        await RejectAsync(tenantId, userId, approval2, "Sai số tiền");
        Assert.Equal("rejected", (await GetApprovalAsync(tenantId, approval2)).Status);
    }

    [Fact]
    public async Task BlockConfirmOnCriticalException_And_TenantIsolation()
    {
        var tenantA = await CreateTenantAsync("TN-E11F-A", "FC FULL A");
        var tenantB = await CreateTenantAsync("TN-E11F-B", "FC FULL B");
        var billA = await CreateBillAsync(tenantA, "BL-E11F-A", "freight");
        var billB = await CreateBillAsync(tenantB, "BL-E11F-B", "freight");

        var costA = await CreateDirectCostAsync(tenantA, billA, 1_000m, "FREIGHT");
        var revenueA = await CreateRevenueAsync(tenantA, billA, 2_000m, "FREIGHT");

        // Critical open exception on cost → confirm blocked (default config on)
        var exCost = await OpenExceptionAsync(
            tenantA, "BLOCK-COST", "critical", "Chặn xác nhận CP", "cost", costA, DateTimeOffset.UtcNow.AddHours(4), null);

        using (var blocked = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costA}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 1_000m })
        })
        {
            blocked.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var res = await _client.SendAsync(blocked);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("ngoại lệ nghiêm trọng", err!.Message, StringComparison.OrdinalIgnoreCase);
        }

        // Medium severity does not block
        var costOk = await CreateDirectCostAsync(tenantA, billA, 200m, "THC");
        await OpenExceptionAsync(
            tenantA, "MED", "medium", "Không chặn", "cost", costOk, DateTimeOffset.UtcNow.AddDays(1), null);
        using (var ok = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costOk}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 200m })
        })
        {
            ok.Headers.Add("X-Tenant-Id", tenantA.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(ok)).StatusCode);
        }

        // Resolve critical → confirm allowed
        await ResolveExceptionAsync(tenantA, exCost, "Đã xử lý");
        using (var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costA}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 1_000m })
        })
        {
            confirm.Headers.Add("X-Tenant-Id", tenantA.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(confirm)).StatusCode);
        }

        // Revenue critical block
        var exRev = await OpenExceptionAsync(
            tenantA, "BLOCK-REV", "critical", "Chặn DT", "revenue", revenueA, DateTimeOffset.UtcNow.AddHours(2), null);
        using (var blockedRev = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueA}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 2_000m })
        })
        {
            blockedRev.Headers.Add("X-Tenant-Id", tenantA.ToString());
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(blockedRev)).StatusCode);
        }

        await CloseExceptionAsync(tenantA, exRev);
        using (var confirmRev = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueA}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 2_000m })
        })
        {
            confirmRev.Headers.Add("X-Tenant-Id", tenantA.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(confirmRev)).StatusCode);
        }

        // Tenant isolation on exception + batch recon
        var paymentB = await CreatePaymentAsync(tenantB, billB, 300m);
        var reconB = await StartReconciliationAsync(tenantB, billB, "manual");
        await AddReconciliationDetailBatchAsync(tenantB, reconB, new[]
        {
            new DetailLine("payment", paymentB, 300m, 0m, 0m)
        });

        using (var cross = new HttpRequestMessage(HttpMethod.Get, $"/api/exceptions/{exCost}"))
        {
            cross.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(cross)).StatusCode);
        }

        Assert.Empty(await ListExceptionsAsync(tenantB));
        Assert.Single(await ListVariancesAsync(tenantB));
        Assert.DoesNotContain(await ListVariancesAsync(tenantA), v => v.Amount == 300m);
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

    private async Task<CostDto> GetCostAsync(Guid tenantId, Guid costId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/costs/{costId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CostDto>(JsonOptions))!;
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

    private async Task<Guid> CreateUserAsync(Guid tenantId, string email, string displayName)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email, displayName })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateRoleAsync(Guid tenantId, string code, string name)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/roles")
        {
            Content = JsonContent.Create(new { code, name })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task AssignUserRoleAsync(Guid tenantId, Guid userId, Guid roleId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/roles/{roleId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
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

    private async Task<List<Guid>> AddReconciliationDetailBatchAsync(
        Guid tenantId,
        Guid reconId,
        IReadOnlyList<DetailLine> lines)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/reconciliations/{reconId}/details/batch")
        {
            Content = JsonContent.Create(new
            {
                details = lines.Select(l => new
                {
                    sourceType = l.SourceType,
                    sourceId = l.SourceId,
                    sourceAmount = l.SourceAmount,
                    targetAmount = l.TargetAmount,
                    matchedAmount = l.MatchedAmount,
                    currencyCode = "VND"
                }).ToList()
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BatchIdsResponse>(JsonOptions);
        return body!.Ids;
    }

    private async Task<ReconciliationDto> GetReconciliationAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/reconciliations/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReconciliationDto>(JsonOptions))!;
    }

    private async Task<List<VarianceDto>> ListVariancesAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/variances");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<VarianceDto>>(JsonOptions))!;
    }

    private async Task<Guid> OpenExceptionAsync(
        Guid tenantId,
        string ruleCode,
        string severity,
        string title,
        string? objectType,
        Guid? objectId,
        DateTimeOffset? dueAt,
        Guid? ownerId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/exceptions")
        {
            Content = JsonContent.Create(new
            {
                ruleCode,
                severity,
                title,
                objectType,
                objectId,
                dueAt,
                ownerId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<List<ExceptionDto>> ListExceptionsAsync(
        Guid tenantId,
        string? status = null,
        string? severity = null,
        string? objectType = null)
    {
        var qs = new List<string>();
        if (status is not null) qs.Add($"status={Uri.EscapeDataString(status)}");
        if (severity is not null) qs.Add($"severity={Uri.EscapeDataString(severity)}");
        if (objectType is not null) qs.Add($"objectType={Uri.EscapeDataString(objectType)}");
        var url = "/api/exceptions" + (qs.Count == 0 ? "" : "?" + string.Join("&", qs));
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<ExceptionDto>>(JsonOptions))!;
    }

    private async Task<ExceptionDto> GetExceptionAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/exceptions/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExceptionDto>(JsonOptions))!;
    }

    private async Task EscalateExceptionAsync(Guid tenantId, Guid userId, Guid id, string reason)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/exceptions/{id}/escalate")
        {
            Content = JsonContent.Create(new { escalationReason = reason })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task ResolveExceptionAsync(Guid tenantId, Guid id, string notes)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/exceptions/{id}/resolve")
        {
            Content = JsonContent.Create(new { resolutionNotes = notes })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task CloseExceptionAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/exceptions/{id}/close");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> RequestApprovalAsync(
        Guid tenantId,
        Guid? userId,
        string objectType,
        Guid objectId,
        string? reason,
        int? requiredLevel = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/approvals")
        {
            Content = JsonContent.Create(new
            {
                objectType,
                objectId,
                requestReason = reason,
                requiredLevel
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (userId.HasValue)
        {
            req.Headers.Add("X-User-Id", userId.Value.ToString());
        }

        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task ApproveAsync(Guid tenantId, Guid userId, Guid approvalId, string reason)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/approvals/{approvalId}/approve")
        {
            Content = JsonContent.Create(new { decisionReason = reason })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task RejectAsync(Guid tenantId, Guid userId, Guid approvalId, string reason)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/approvals/{approvalId}/reject")
        {
            Content = JsonContent.Create(new { decisionReason = reason })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<ApprovalDto> GetApprovalAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/approvals/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApprovalDto>(JsonOptions))!;
    }

    private sealed record DetailLine(
        string SourceType,
        Guid SourceId,
        decimal SourceAmount,
        decimal TargetAmount,
        decimal MatchedAmount);

    private sealed record IdResponse(Guid Id);
    private sealed record BatchIdsResponse(List<Guid> Ids);
    private sealed record ErrorResponse(string Message);

    private sealed record ReconciliationDetailDto(
        Guid Id,
        Guid ReconciliationId,
        string SourceType,
        Guid SourceId,
        decimal VarianceAmount,
        string LineStatus,
        Guid? VarianceId);

    private sealed record ReconciliationDto(
        Guid Id,
        string Status,
        List<ReconciliationDetailDto> Details);

    private sealed record VarianceDto(
        Guid Id,
        decimal Amount,
        string Severity,
        string Status,
        Guid? ExceptionId);

    private sealed record ExceptionDto(
        Guid Id,
        string RuleCode,
        string Severity,
        Guid? OwnerId,
        string Status,
        DateTimeOffset? DueAt,
        string Title,
        string? ObjectType,
        Guid? ObjectId,
        DateTimeOffset? EscalatedAt,
        Guid? EscalatedBy,
        string? EscalationReason);

    private sealed record ApprovalDto(
        Guid Id,
        string ObjectType,
        Guid ObjectId,
        string Status,
        int RequiredLevel,
        int CurrentLevel);

    private sealed record CostDto(Guid Id, string ApprovalStatus);
}
