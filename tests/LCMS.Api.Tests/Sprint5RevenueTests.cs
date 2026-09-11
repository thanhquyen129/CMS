using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint5RevenueTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint5RevenueTests(LcmsApiFactory factory)
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
    public async Task Maturity_ConfirmActualize_DoesNotOverwritePriorLayerAmounts_AndAdjustCreatesHistory()
    {
        var tenantId = await CreateTenantAsync("TN-REV-MAT", "Revenue Maturity");
        var billId = await CreateBillAsync(tenantId, "BL-REV-1", "freight");

        var revenueId = await CreateRevenueAsync(tenantId, billId, 5000m, "FREIGHT", "VND");

        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 5200m })
        };
        confirm.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(confirm)).StatusCode);

        var afterConfirm = await GetRevenueAsync(tenantId, revenueId);
        Assert.Equal("confirmed", afterConfirm.FinancialMaturity);
        Assert.Equal(5000m, afterConfirm.ExpectedAmount);
        Assert.Equal(5200m, afterConfirm.ConfirmedAmount);
        Assert.Null(afterConfirm.ActualAmount);
        Assert.Equal(5200m, afterConfirm.Amount);
        Assert.NotNull(afterConfirm.ConfirmedAt);

        using var actualize = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueId}/actualize")
        {
            Content = JsonContent.Create(new { actualAmount = 5100m })
        };
        actualize.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(actualize)).StatusCode);

        var afterActual = await GetRevenueAsync(tenantId, revenueId);
        Assert.Equal("actual", afterActual.FinancialMaturity);
        Assert.Equal(5000m, afterActual.ExpectedAmount);
        Assert.Equal(5200m, afterActual.ConfirmedAmount);
        Assert.Equal(5100m, afterActual.ActualAmount);
        Assert.Equal(5100m, afterActual.Amount);
        Assert.NotNull(afterActual.ActualizedAt);

        // Cannot re-confirm after already confirmed/actualized (C-009)
        using var confirmAgain = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 999m })
        };
        confirmAgain.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(confirmAgain)).StatusCode);

        // Adjustment creates history — no silent overwrite
        using var adj = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revenueId}/adjustments")
        {
            Content = JsonContent.Create(new
            {
                adjustmentType = "adjustment",
                deltaAmount = 100m,
                reason = "Bổ sung phụ thu thực tế"
            })
        };
        adj.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(adj)).StatusCode);

        var afterAdj = await GetRevenueAsync(tenantId, revenueId);
        Assert.Equal(5000m, afterAdj.ExpectedAmount);
        Assert.Equal(5200m, afterAdj.ConfirmedAmount);
        Assert.Equal(5200m, afterAdj.ActualAmount);
        Assert.Equal(5200m, afterAdj.Amount);
        Assert.Single(afterAdj.Adjustments);
        Assert.Equal(5100m, afterAdj.Adjustments[0].AmountBefore);
        Assert.Equal(5200m, afterAdj.Adjustments[0].AmountAfter);
        Assert.Equal("actual", afterAdj.Adjustments[0].AppliedToMaturity);

        // C-004: reject inventing economic revenue from document/AR
        using var bad = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount = 1m,
                currencyCode = "VND",
                sourceType = "document",
                sourceId = Guid.NewGuid()
            })
        };
        bad.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(bad)).StatusCode);
    }

    [Fact]
    public async Task FinancialProfile_BestAvailable_ProfitPerCurrency_DoesNotMixCurrencies()
    {
        var tenantId = await CreateTenantAsync("TN-REV-PROF", "Revenue Profile");
        var billId = await CreateBillAsync(tenantId, "BL-PROF", "freight");

        var revVnd = await CreateRevenueAsync(tenantId, billId, 10000m, "FREIGHT", "VND");
        await CreateRevenueAsync(tenantId, billId, 100m, "SURCHARGE", "USD");

        using (var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/revenues/{revVnd}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 11000m })
        })
        {
            confirm.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(confirm)).StatusCode);
        }

        // Direct cost Expected 3000 VND (Best Available = Expected until confirmed)
        await CreateDirectCostAsync(tenantId, billId, 3000m, "FREIGHT");

        var profile = await GetFinancialProfileAsync(tenantId, billId);
        Assert.Equal("best_available", profile.ViewKind);
        Assert.True(profile.HasMixedCurrencies);
        Assert.Equal(2, profile.ByCurrency.Count);

        var vnd = profile.ByCurrency.Single(b => b.CurrencyCode == "VND");
        Assert.Equal(11000m, vnd.RevenueBestAvailable);
        Assert.Equal(3000m, vnd.CostBestAvailable);
        Assert.Equal(8000m, vnd.ProfitBestAvailable);
        Assert.Equal(3000m, vnd.DirectCostBestAvailable);
        Assert.Equal(0m, vnd.AllocatedCostAmount);

        var usd = profile.ByCurrency.Single(b => b.CurrencyCode == "USD");
        Assert.Equal(100m, usd.RevenueBestAvailable);
        Assert.Equal(0m, usd.CostBestAvailable);
        Assert.Equal(100m, usd.ProfitBestAvailable);

        // Note documents no cross-currency sum
        Assert.Contains("currency_code", profile.Note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Revenue_CrossTenant_Returns404_AndIdempotentSource_DoesNotDuplicate()
    {
        var tenantA = await CreateTenantAsync("TN-REV-A", "Revenue A");
        var tenantB = await CreateTenantAsync("TN-REV-B", "Revenue B");
        var billId = await CreateBillAsync(tenantA, "BL-ISO", "freight");
        var sourceId = Guid.NewGuid();

        Guid revenueId;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount = 2000m,
                currencyCode = "VND",
                revenueTypeCode = "FREIGHT",
                sourceType = "manual",
                sourceId
            })
        })
        {
            create.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var res = await _client.SendAsync(create);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            revenueId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        // Same source → same economic revenue (C-004 / idempotent)
        using (var again = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount = 9999m,
                currencyCode = "VND",
                revenueTypeCode = "FREIGHT",
                sourceType = "manual",
                sourceId
            })
        })
        {
            again.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var res = await _client.SendAsync(again);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            var id2 = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
            Assert.Equal(revenueId, id2);
        }

        var list = await ListRevenuesAsync(tenantA);
        Assert.Single(list);

        using var getAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/revenues/{revenueId}");
        getAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        var leaked = await _client.SendAsync(getAsB);
        Assert.Equal(HttpStatusCode.NotFound, leaked.StatusCode);
        var err = await leaked.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("not_found", err!.Code);
        Assert.DoesNotContain(revenueId.ToString(), err.Message, StringComparison.OrdinalIgnoreCase);

        using var profileAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billId}/financial-profile");
        profileAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(profileAsB)).StatusCode);
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
        string currencyCode)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount,
                currencyCode,
                revenueTypeCode
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
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

    private async Task<RevenueResponse> GetRevenueAsync(Guid tenantId, Guid revenueId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/revenues/{revenueId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<RevenueResponse>(JsonOptions))!;
    }

    private async Task<List<RevenueListItem>> ListRevenuesAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/revenues");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<RevenueListItem>>(JsonOptions))!;
    }

    private async Task<FinancialProfileResponse> GetFinancialProfileAsync(Guid tenantId, Guid billId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/bills/{billId}/financial-profile");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<FinancialProfileResponse>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record RevenueListItem(
        Guid Id,
        Guid BillId,
        string FinancialMaturity,
        decimal Amount,
        string CurrencyCode,
        string? RevenueTypeCode,
        string RecordStatus,
        DateOnly EffectiveDate);

    private sealed record AdjustmentResponse(
        Guid Id,
        string AdjustmentType,
        decimal DeltaAmount,
        string CurrencyCode,
        string Reason,
        DateOnly EffectiveDate,
        string AppliedToMaturity,
        decimal AmountBefore,
        decimal AmountAfter,
        DateTimeOffset CreatedAt);

    private sealed record RevenueResponse(
        Guid Id,
        Guid BillId,
        string FinancialMaturity,
        decimal ExpectedAmount,
        decimal? ConfirmedAmount,
        decimal? ActualAmount,
        decimal Amount,
        string CurrencyCode,
        string? RevenueTypeCode,
        Guid? CustomerPartyId,
        string? SourceType,
        Guid? SourceId,
        string? RecognitionPolicyVersion,
        string RecordStatus,
        string ApprovalStatus,
        DateOnly EffectiveDate,
        DateTimeOffset? ConfirmedAt,
        DateTimeOffset? ActualizedAt,
        List<AdjustmentResponse> Adjustments);

    private sealed record CurrencyBucket(
        string CurrencyCode,
        decimal RevenueBestAvailable,
        decimal CostBestAvailable,
        decimal ProfitBestAvailable,
        decimal DirectCostBestAvailable,
        decimal AllocatedCostAmount,
        int RevenueLineCount,
        int DirectCostLineCount,
        int AllocatedCostLineCount);

    private sealed record FinancialProfileResponse(
        Guid BillId,
        string BillNo,
        string ViewKind,
        List<CurrencyBucket> ByCurrency,
        bool HasMixedCurrencies,
        string Note);

    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
