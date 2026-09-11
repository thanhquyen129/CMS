using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint9FinancialControlTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint9FinancialControlTests(LcmsApiFactory factory)
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
    public async Task Variance_IsSeparateFrom_Exception_AndReconcileDoesNotAutoOpenException()
    {
        var tenantId = await CreateTenantAsync("TN-E11-VAR", "Financial Control Variance");
        var billId = await CreateBillAsync(tenantId, "BL-E11-1", "freight");
        var paymentId = await CreatePaymentAsync(tenantId, billId, 1000m);
        var apExposureId = await CreatePayableExposureAsync(tenantId, billId, 1000m);
        var apId = await RecognizePayableAsync(tenantId, apExposureId, 1000m);

        var reconId = await StartReconciliationAsync(tenantId, billId, "payment_ap");

        // Matched 700 of 1000 → variance 300 control fact; NO exception
        var detailId = await AddReconciliationDetailAsync(
            tenantId,
            reconId,
            sourceType: "payment",
            sourceId: paymentId,
            targetType: "accounts_payable",
            targetId: apId,
            sourceAmount: 1000m,
            targetAmount: 1000m,
            matchedAmount: 700m);

        var recon = await GetReconciliationAsync(tenantId, reconId);
        Assert.Equal("in_progress", recon.Status);
        var detail = Assert.Single(recon.Details);
        Assert.Equal(detailId, detail.Id);
        Assert.Equal(300m, detail.VarianceAmount);
        Assert.Equal("variance", detail.LineStatus);
        Assert.NotNull(detail.VarianceId);

        var variances = await ListVariancesAsync(tenantId);
        var variance = Assert.Single(variances);
        Assert.Equal(detail.VarianceId, variance.Id);
        Assert.Equal(300m, variance.Amount);
        Assert.Equal("open", variance.Status);
        Assert.Null(variance.ExceptionId);

        // Variance ≠ Exception: reconcile did not invent exception inbox item
        Assert.Empty(await ListExceptionsAsync(tenantId));

        // Open exception separately (optional link to variance)
        var exceptionId = await OpenExceptionAsync(
            tenantId,
            ruleCode: "RECON_VARIANCE",
            severity: "high",
            title: "Chênh lệch thanh toán–AP",
            varianceId: variance.Id,
            reconciliationId: reconId,
            billId: billId);

        var exception = await GetExceptionAsync(tenantId, exceptionId);
        Assert.Equal("open", exception.Status);
        Assert.Equal("high", exception.Severity);
        Assert.Equal(variance.Id, exception.VarianceId);

        variance = await GetVarianceAsync(tenantId, variance.Id);
        Assert.Equal(exceptionId, variance.ExceptionId);
        Assert.Equal("open", variance.Status); // variance lifecycle independent

        await ResolveExceptionAsync(tenantId, exceptionId, "Đã giải trình");
        await CloseExceptionAsync(tenantId, exceptionId);

        exception = await GetExceptionAsync(tenantId, exceptionId);
        Assert.Equal("closed", exception.Status);
        Assert.NotNull(exception.ResolvedAt);
        Assert.NotNull(exception.ClosedAt);

        // Closing exception does not clear/delete variance
        variance = await GetVarianceAsync(tenantId, variance.Id);
        Assert.Equal("open", variance.Status);
        Assert.Equal(300m, variance.Amount);
    }

    [Fact]
    public async Task Approval_IsIndependentOf_Permission_AndUpdatesCostApprovalStatus()
    {
        var tenantId = await CreateTenantAsync("TN-E11-APR", "Financial Control Approval");
        var billId = await CreateBillAsync(tenantId, "BL-E11-APR", "freight");
        var costId = await CreateDirectCostAsync(tenantId, billId, 500m, "FREIGHT");

        // Seed role + user with NO permissions — PermissionService would deny action checks
        var roleId = await CreateRoleAsync(tenantId, "viewer", "Viewer");
        var userId = await CreateUserAsync(tenantId, "approver@example.com", "Approver");
        await AssignUserRoleAsync(tenantId, userId, roleId);

        var costBefore = await GetCostAsync(tenantId, costId);
        Assert.Equal("not_required", costBefore.ApprovalStatus);

        // Request + approve with X-User-Id — Approval workflow does not call Permission
        var approvalId = await RequestApprovalAsync(tenantId, userId, "cost", costId, "Cần duyệt chi phí");
        var pending = await GetApprovalAsync(tenantId, approvalId);
        Assert.Equal("pending", pending.Status);
        Assert.Equal(userId, pending.RequestedBy);

        var costPending = await GetCostAsync(tenantId, costId);
        Assert.Equal("pending", costPending.ApprovalStatus);

        await ApproveAsync(tenantId, userId, approvalId, "OK");
        var approved = await GetApprovalAsync(tenantId, approvalId);
        Assert.Equal("approved", approved.Status);
        Assert.Equal(userId, approved.DecidedBy);
        Assert.NotNull(approved.DecidedAt);

        var costApproved = await GetCostAsync(tenantId, costId);
        Assert.Equal("approved", costApproved.ApprovalStatus);

        // Reject path on another cost — still independent of permission
        var cost2 = await CreateDirectCostAsync(tenantId, billId, 200m, "THC");
        var approval2 = await RequestApprovalAsync(tenantId, userId, "cost", cost2, null);
        await RejectAsync(tenantId, userId, approval2, "Sai số");
        var rejected = await GetApprovalAsync(tenantId, approval2);
        Assert.Equal("rejected", rejected.Status);
        Assert.Equal("rejected", (await GetCostAsync(tenantId, cost2)).ApprovalStatus);
    }

    [Fact]
    public async Task FinancialControl_CrossTenantIsolated()
    {
        var tenantA = await CreateTenantAsync("TN-E11-A", "FC A");
        var tenantB = await CreateTenantAsync("TN-E11-B", "FC B");
        var billA = await CreateBillAsync(tenantA, "BL-E11-A", "freight");
        var billB = await CreateBillAsync(tenantB, "BL-E11-B", "freight");
        var paymentA = await CreatePaymentAsync(tenantA, billA, 400m);
        var paymentB = await CreatePaymentAsync(tenantB, billB, 400m);

        var reconA = await StartReconciliationAsync(tenantA, billA, "manual");
        await AddReconciliationDetailAsync(
            tenantA, reconA, "payment", paymentA, null, null, 400m, 0m, 0m);

        var reconB = await StartReconciliationAsync(tenantB, billB, "manual");
        await AddReconciliationDetailAsync(
            tenantB, reconB, "payment", paymentB, null, null, 400m, 0m, 0m);

        // Tenant B cannot see tenant A reconciliation / variances / exceptions / approvals
        using (var req = new HttpRequestMessage(HttpMethod.Get, $"/api/reconciliations/{reconA}"))
        {
            req.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(req)).StatusCode);
        }

        var variancesA = await ListVariancesAsync(tenantA);
        Assert.Single(variancesA); // unmatched 400 → variance
        using (var req = new HttpRequestMessage(HttpMethod.Get, $"/api/variances/{variancesA[0].Id}"))
        {
            req.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(req)).StatusCode);
        }

        var exceptionA = await OpenExceptionAsync(
            tenantA, "X-CROSS", "medium", "Ngoại lệ A", null, reconA, billA);
        using (var req = new HttpRequestMessage(HttpMethod.Get, $"/api/exceptions/{exceptionA}"))
        {
            req.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(req)).StatusCode);
        }

        var costA = await CreateDirectCostAsync(tenantA, billA, 100m, "FREIGHT");
        var approvalA = await RequestApprovalAsync(tenantA, null, "cost", costA, null);
        using (var req = new HttpRequestMessage(HttpMethod.Get, $"/api/approvals/{approvalA}"))
        {
            req.Headers.Add("X-Tenant-Id", tenantB.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(req)).StatusCode);
        }

        // Tenant A cannot add detail with tenant B payment
        using (var cross = new HttpRequestMessage(HttpMethod.Post, $"/api/reconciliations/{reconA}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceType = "payment",
                sourceId = paymentB,
                sourceAmount = 10m,
                targetAmount = 0m,
                matchedAmount = 0m,
                currencyCode = "VND"
            })
        })
        {
            cross.Headers.Add("X-Tenant-Id", tenantA.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(cross)).StatusCode);
        }

        Assert.Empty(await ListExceptionsAsync(tenantB));
        Assert.Single(await ListExceptionsAsync(tenantA));
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
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
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
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

    private async Task<Guid> CreatePayableExposureAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payable-exposures")
        {
            Content = JsonContent.Create(new { amount, currencyCode = "VND", billId })
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

    private async Task<Guid> AddReconciliationDetailAsync(
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
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
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

    private async Task<VarianceDto> GetVarianceAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/variances/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VarianceDto>(JsonOptions))!;
    }

    private async Task<Guid> OpenExceptionAsync(
        Guid tenantId,
        string ruleCode,
        string severity,
        string title,
        Guid? varianceId,
        Guid? reconciliationId,
        Guid? billId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/exceptions")
        {
            Content = JsonContent.Create(new
            {
                ruleCode,
                severity,
                title,
                varianceId,
                reconciliationId,
                billId,
                dueAt = DateTimeOffset.UtcNow.AddDays(3)
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<List<ExceptionDto>> ListExceptionsAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/exceptions");
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
        string? reason)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/approvals")
        {
            Content = JsonContent.Create(new { objectType, objectId, requestReason = reason })
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

    private sealed record IdResponse(Guid Id);

    private sealed record ReconciliationDetailDto(
        Guid Id,
        Guid ReconciliationId,
        string SourceType,
        Guid SourceId,
        string? TargetType,
        Guid? TargetId,
        decimal SourceAmount,
        decimal TargetAmount,
        decimal MatchedAmount,
        decimal VarianceAmount,
        string CurrencyCode,
        string LineStatus,
        Guid? VarianceId,
        string? Notes);

    private sealed record ReconciliationDto(
        Guid Id,
        string ReconciliationType,
        string? RuleCode,
        int VersionNo,
        string Status,
        Guid? BillId,
        string? Notes,
        DateTimeOffset? StartedAt,
        Guid? StartedBy,
        DateTimeOffset? CompletedAt,
        Guid? CompletedBy,
        List<ReconciliationDetailDto> Details);

    private sealed record VarianceDto(
        Guid Id,
        Guid? ReconciliationId,
        Guid? ReconciliationDetailId,
        string VarianceType,
        decimal Amount,
        string CurrencyCode,
        string SourceType,
        Guid SourceId,
        string? TargetType,
        Guid? TargetId,
        string Status,
        string? Explanation,
        Guid? ExceptionId);

    private sealed record ExceptionDto(
        Guid Id,
        string RuleCode,
        string Severity,
        Guid? OwnerId,
        string Status,
        DateTimeOffset? DueAt,
        string Title,
        string? Description,
        Guid? BillId,
        Guid? ReconciliationId,
        Guid? VarianceId,
        DateTimeOffset? ResolvedAt,
        Guid? ResolvedBy,
        string? ResolutionNotes,
        DateTimeOffset? ClosedAt,
        Guid? ClosedBy);

    private sealed record ApprovalDto(
        Guid Id,
        string ObjectType,
        Guid ObjectId,
        string Status,
        Guid? RequestedBy,
        DateTimeOffset RequestedAt,
        string? RequestReason,
        Guid? DecidedBy,
        DateTimeOffset? DecidedAt,
        string? DecisionReason,
        string? Notes);

    private sealed record CostDto(
        Guid Id,
        string ApprovalStatus);
}
