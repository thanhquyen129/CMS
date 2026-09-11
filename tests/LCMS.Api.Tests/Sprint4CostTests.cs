using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint4CostTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint4CostTests(LcmsApiFactory factory)
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
    public async Task Maturity_ConfirmActualize_DoesNotOverwritePriorLayerAmounts()
    {
        var tenantId = await CreateTenantAsync("TN-COST-MAT", "Cost Maturity");
        var billId = await CreateBillAsync(tenantId, "BL-COST-1", "freight");

        var costId = await CreateDirectCostAsync(tenantId, billId, 1000m, "FREIGHT");

        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 1200m })
        };
        confirm.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(confirm)).StatusCode);

        var afterConfirm = await GetCostAsync(tenantId, costId);
        Assert.Equal("confirmed", afterConfirm.FinancialMaturity);
        Assert.Equal(1000m, afterConfirm.ExpectedAmount);
        Assert.Equal(1200m, afterConfirm.ConfirmedAmount);
        Assert.Null(afterConfirm.ActualAmount);
        Assert.Equal(1200m, afterConfirm.Amount);
        Assert.NotNull(afterConfirm.ConfirmedAt);

        using var actualize = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/actualize")
        {
            Content = JsonContent.Create(new { actualAmount = 1150m })
        };
        actualize.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(actualize)).StatusCode);

        var afterActual = await GetCostAsync(tenantId, costId);
        Assert.Equal("actual", afterActual.FinancialMaturity);
        Assert.Equal(1000m, afterActual.ExpectedAmount);
        Assert.Equal(1200m, afterActual.ConfirmedAmount);
        Assert.Equal(1150m, afterActual.ActualAmount);
        Assert.Equal(1150m, afterActual.Amount);
        Assert.NotNull(afterActual.ActualizedAt);

        // Cannot re-confirm after already confirmed/actualized
        using var confirmAgain = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 999m })
        };
        confirmAgain.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var blocked = await _client.SendAsync(confirmAgain);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        // Adjustment creates history row — no silent overwrite
        using var adj = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/adjustments")
        {
            Content = JsonContent.Create(new
            {
                adjustmentType = "adjustment",
                deltaAmount = 50m,
                reason = "Bổ sung phụ phí thực tế"
            })
        };
        adj.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(adj)).StatusCode);

        var afterAdj = await GetCostAsync(tenantId, costId);
        Assert.Equal(1000m, afterAdj.ExpectedAmount);
        Assert.Equal(1200m, afterAdj.ConfirmedAmount);
        Assert.Equal(1200m, afterAdj.ActualAmount);
        Assert.Equal(1200m, afterAdj.Amount);
        Assert.Single(afterAdj.Adjustments);
        Assert.Equal(1150m, afterAdj.Adjustments[0].AmountBefore);
        Assert.Equal(1200m, afterAdj.Adjustments[0].AmountAfter);
        Assert.Equal("actual", afterAdj.Adjustments[0].AppliedToMaturity);
    }

    [Fact]
    public async Task Allocation_Finalize_ConservesAmount_DoesNotCreateNewCost()
    {
        var tenantId = await CreateTenantAsync("TN-COST-ALLOC", "Cost Alloc");
        var billA = await CreateBillAsync(tenantId, "BL-A", "freight");
        var billB = await CreateBillAsync(tenantId, "BL-B", "freight");
        var billC = await CreateBillAsync(tenantId, "BL-C", "freight");

        // Shared cost — no bill_id
        Guid sharedCostId;
        using (var create = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                attributionType = "shared",
                amount = 100m,
                currencyCode = "VND",
                costTypeCode = "TERMINAL"
            })
        })
        {
            create.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(create);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            sharedCostId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        var costsBefore = await ListCostsAsync(tenantId);
        Assert.Single(costsBefore);

        Guid allocationId;
        using (var alloc = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{sharedCostId}/allocations")
        {
            Content = JsonContent.Create(new
            {
                allocationBasis = "weight",
                details = new[]
                {
                    new { billId = billA, basisValue = 1m },
                    new { billId = billB, basisValue = 1m },
                    new { billId = billC, basisValue = 1m }
                }
            })
        })
        {
            alloc.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(alloc);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            allocationId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        // C-006: zero basis blocked on create (validation) / finalize
        using (var badAlloc = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{sharedCostId}/allocations")
        {
            Content = JsonContent.Create(new
            {
                allocationBasis = "weight",
                details = new[]
                {
                    new { billId = billA, basisValue = 0m },
                    new { billId = billB, basisValue = 1m }
                }
            })
        })
        {
            badAlloc.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var bad = await _client.SendAsync(badAlloc);
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }

        using (var finalize = new HttpRequestMessage(HttpMethod.Post, $"/api/cost-allocations/{allocationId}/finalize"))
        {
            finalize.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(finalize)).StatusCode);
        }

        var cost = await GetCostAsync(tenantId, sharedCostId);
        Assert.Single(cost.Allocations);
        var finalized = cost.Allocations[0];
        Assert.Equal("finalized", finalized.AllocationStatus);
        Assert.Equal(100m, finalized.AllocatableAmount);
        Assert.Equal(100m, finalized.AllocatedAmount);
        Assert.Equal(3, finalized.Details.Count);
        Assert.Equal(100m, finalized.Details.Sum(d => d.AllocatedAmount));

        // Single Economic Cost: allocation did not create new cost rows
        var costsAfter = await ListCostsAsync(tenantId);
        Assert.Single(costsAfter);
        Assert.Equal(sharedCostId, costsAfter[0].Id);
        Assert.Null(costsAfter[0].BillId);
        Assert.Equal("shared", costsAfter[0].AttributionType);
    }

    [Fact]
    public async Task Cost_CrossTenant_Returns404_AndSeedFromRating_IsIdempotent()
    {
        var tenantA = await CreateTenantAsync("TN-COST-A", "Cost A");
        var tenantB = await CreateTenantAsync("TN-COST-B", "Cost B");
        var billId = await CreateBillAsync(tenantA, "BL-SEED", "freight");

        // Build rating via Sprint 3 APIs
        var cardId = await CreateRateCardAsync(tenantA, "RC-COST", "Bảng giá cost");
        var versionId = await CreateVersionAsync(tenantA, cardId);
        await AddRuleAsync(tenantA, versionId, "FREIGHT", "Cước", "fixed", 500m);
        using (var publish = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish"))
        {
            publish.Headers.Add("X-Tenant-Id", tenantA.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(publish)).StatusCode);
        }

        Guid ratingId;
        using (var rate = new HttpRequestMessage(HttpMethod.Post, "/api/ratings")
        {
            Content = JsonContent.Create(new { billId, rateVersionId = versionId, quantity = 1 })
        })
        {
            rate.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var res = await _client.SendAsync(rate);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            ratingId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        SeedResult seed1;
        using (var seed = new HttpRequestMessage(HttpMethod.Post, $"/api/ratings/{ratingId}/seed-expected-costs"))
        {
            seed.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var res = await _client.SendAsync(seed);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            seed1 = (await res.Content.ReadFromJsonAsync<SeedResult>(JsonOptions))!;
            Assert.True(seed1.CreatedCount >= 1);
            Assert.Equal(0, seed1.ExistingCount);
        }

        // Idempotent re-seed
        using (var seedAgain = new HttpRequestMessage(HttpMethod.Post, $"/api/ratings/{ratingId}/seed-expected-costs"))
        {
            seedAgain.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var res = await _client.SendAsync(seedAgain);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var seed2 = (await res.Content.ReadFromJsonAsync<SeedResult>(JsonOptions))!;
            Assert.Equal(0, seed2.CreatedCount);
            Assert.Equal(seed1.CreatedCount, seed2.ExistingCount);
            Assert.Equal(seed1.CostIds.Count, seed2.CostIds.Count);
        }

        var costId = seed1.CostIds[0];
        using var getAsB = new HttpRequestMessage(HttpMethod.Get, $"/api/costs/{costId}");
        getAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        var leaked = await _client.SendAsync(getAsB);
        Assert.Equal(HttpStatusCode.NotFound, leaked.StatusCode);
        var err = await leaked.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Equal("not_found", err!.Code);
        Assert.DoesNotContain(costId.ToString(), err.Message, StringComparison.OrdinalIgnoreCase);

        var cost = await GetCostAsync(tenantA, costId);
        Assert.Equal("expected", cost.FinancialMaturity);
        Assert.Equal(billId, cost.BillId);
        Assert.Equal("rating_detail", cost.SourceType);
        Assert.Equal(cost.ExpectedAmount, cost.Amount);
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
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
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
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<CostResponse> GetCostAsync(Guid tenantId, Guid costId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/costs/{costId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CostResponse>(JsonOptions))!;
    }

    private async Task<List<CostListItem>> ListCostsAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/costs");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<CostListItem>>(JsonOptions))!;
    }

    private async Task<Guid> CreateRateCardAsync(Guid tenantId, string code, string name)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/rate-cards")
        {
            Content = JsonContent.Create(new
            {
                code,
                name,
                partyType = "vendor",
                currencyCode = "VND",
                description = (string?)null
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateVersionAsync(Guid tenantId, Guid rateCardId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-cards/{rateCardId}/versions")
        {
            Content = JsonContent.Create(new { note = "draft" })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> AddRuleAsync(
        Guid tenantId,
        Guid versionId,
        string code,
        string name,
        string calcMethod,
        decimal unitAmount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules")
        {
            Content = JsonContent.Create(new
            {
                code,
                name,
                calcMethod,
                unitAmount,
                currencyCode = "VND",
                sortOrder = 1
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record CostListItem(
        Guid Id,
        Guid? BillId,
        string AttributionType,
        string FinancialMaturity,
        decimal Amount,
        string CurrencyCode,
        string? CostTypeCode,
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

    private sealed record AllocationDetailResponse(
        Guid Id,
        Guid BillId,
        decimal BasisValue,
        decimal BasisRatio,
        decimal AllocatedAmount,
        decimal RoundingAdjustment);

    private sealed record AllocationResponse(
        Guid Id,
        int VersionNo,
        string AllocationBasis,
        string AllocationStatus,
        decimal AllocatableAmount,
        decimal AllocatedAmount,
        DateTimeOffset? FinalizedAt,
        List<AllocationDetailResponse> Details);

    private sealed record CostResponse(
        Guid Id,
        Guid? BillId,
        string AttributionType,
        string FinancialMaturity,
        decimal ExpectedAmount,
        decimal? ConfirmedAmount,
        decimal? ActualAmount,
        decimal Amount,
        string CurrencyCode,
        string? CostTypeCode,
        Guid? VendorPartyId,
        string? SourceType,
        Guid? SourceId,
        string RecordStatus,
        string ApprovalStatus,
        DateOnly EffectiveDate,
        DateTimeOffset? ConfirmedAt,
        DateTimeOffset? ActualizedAt,
        List<AdjustmentResponse> Adjustments,
        List<AllocationResponse> Allocations);

    private sealed record SeedResult(Guid RatingId, int CreatedCount, int ExistingCount, List<Guid> CostIds);

    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
