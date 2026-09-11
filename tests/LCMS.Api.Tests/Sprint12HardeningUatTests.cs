using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Domain.Terminology;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint12HardeningUatTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint12HardeningUatTests(LcmsApiFactory factory)
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
    public async Task MoneyMutations_WriteAuditEvents_WithActorActionObjectCorrelation()
    {
        var tenantId = await CreateTenantAsync("TN-E14-AUD", "Audit Tenant");
        var billId = await CreateBillAsync(tenantId, "BL-E14-1", "freight");
        var userId = Guid.NewGuid();
        var correlationId = $"corr-audit-{Guid.NewGuid():N}";

        Guid costId;
        using (var createCost = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 1000m,
                currencyCode = "VND",
                costTypeCode = "FREIGHT"
            })
        })
        {
            createCost.Headers.Add("X-Tenant-Id", tenantId.ToString());
            createCost.Headers.Add("X-User-Id", userId.ToString());
            createCost.Headers.Add("X-Correlation-Id", correlationId);
            var response = await _client.SendAsync(createCost);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
            costId = body!.Id;
        }

        using (var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount = 1100m })
        })
        {
            confirm.Headers.Add("X-Tenant-Id", tenantId.ToString());
            confirm.Headers.Add("X-User-Id", userId.ToString());
            confirm.Headers.Add("X-Correlation-Id", correlationId);
            var response = await _client.SendAsync(confirm);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        using (var createRevenue = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount = 5000m,
                currencyCode = "VND",
                revenueTypeCode = "FREIGHT"
            })
        })
        {
            createRevenue.Headers.Add("X-Tenant-Id", tenantId.ToString());
            createRevenue.Headers.Add("X-User-Id", userId.ToString());
            createRevenue.Headers.Add("X-Correlation-Id", correlationId);
            var response = await _client.SendAsync(createRevenue);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var events = await ListAuditEventsAsync(tenantId, correlationId: correlationId);
        Assert.Contains(events, e => e.Action == "cost.create" && e.ObjectType == "cost" && e.ObjectId == costId);
        Assert.Contains(events, e => e.Action == "cost.confirm" && e.ObjectType == "cost" && e.ObjectId == costId);
        Assert.Contains(events, e => e.Action == "revenue.create" && e.ObjectType == "revenue");
        Assert.All(events.Where(e => e.CorrelationId == correlationId), e =>
        {
            Assert.Equal(userId, e.ActorId);
            Assert.Equal(correlationId, e.CorrelationId);
            Assert.False(string.IsNullOrWhiteSpace(e.Action));
            Assert.False(string.IsNullOrWhiteSpace(e.ObjectType));
            Assert.NotEqual(Guid.Empty, e.ObjectId);
        });

        // Tenant isolation on audit list
        var otherTenant = await CreateTenantAsync("TN-E14-AUD2", "Audit Other");
        var otherEvents = await ListAuditEventsAsync(otherTenant);
        Assert.DoesNotContain(otherEvents, e => e.ObjectId == costId);
    }

    [Fact]
    public async Task IntegrationRecord_Duplicate_IsRejected_AndTenantIsolated()
    {
        var tenantA = await CreateTenantAsync("TN-E14-INT-A", "Integration A");
        var tenantB = await CreateTenantAsync("TN-E14-INT-B", "Integration B");

        var idA = await UpsertIntegrationAsync(tenantA, "ops", "invoice", "EXT-001");
        Assert.NotEqual(Guid.Empty, idA);

        using (var dup = new HttpRequestMessage(HttpMethod.Post, "/api/integration-records")
        {
            Content = JsonContent.Create(new
            {
                sourceSystem = "ops",
                externalObjectType = "invoice",
                externalId = "EXT-001",
                status = "received"
            })
        })
        {
            dup.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var response = await _client.SendAsync(dup);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var err = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("C-002", err!.Message);
        }

        // Same external key allowed for other tenant
        var idB = await UpsertIntegrationAsync(tenantB, "ops", "invoice", "EXT-001");
        Assert.NotEqual(idA, idB);

        var listA = await ListIntegrationsAsync(tenantA);
        Assert.Contains(listA, r => r.Id == idA);
        Assert.DoesNotContain(listA, r => r.Id == idB);
    }

    [Fact]
    public async Task SecurityHeaders_Present_HealthReady_AndTerminologyCoverage_Pass1()
    {
        var health = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.True(health.Headers.TryGetValues("X-Correlation-Id", out _));
        Assert.Equal("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", health.Headers.GetValues("X-Frame-Options").Single());

        var ready = await _client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("nosniff", ready.Headers.GetValues("X-Content-Type-Options").Single());

        // API path also gets security headers + correlation
        var terminology = await _client.GetAsync("/api/terminology");
        Assert.Equal(HttpStatusCode.OK, terminology.StatusCode);
        Assert.Equal("nosniff", terminology.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(terminology.Headers.TryGetValues("X-Correlation-Id", out _));

        var dict = await terminology.Content.ReadFromJsonAsync<Dictionary<string, string>>(JsonOptions);
        Assert.NotNull(dict);

        // Required keys used across Sprints 1–11 + Sprint 12 hardening
        string[] required =
        [
            "BILL", "COST", "REVENUE", "EXPECTED", "CONFIRMED", "ACTUAL",
            "PAYMENT", "COLLECTION", "RECONCILIATION", "EXCEPTION", "APPROVAL",
            "FINANCIAL_CLOSE", "FINANCIAL_CLOSE_SNAPSHOT", "DASHBOARD",
            "AUDIT_EVENT", "INTEGRATION_RECORD", "CORRELATION_ID", "RATE_LIMIT"
        ];
        foreach (var key in required)
        {
            Assert.True(dict!.ContainsKey(key), $"Missing terminology key: {key}");
            Assert.False(string.IsNullOrWhiteSpace(dict[key]));
        }

        // Static dictionary also contains every required key (source of truth)
        foreach (var key in required)
        {
            Assert.True(VietnameseUiTerms.All.ContainsKey(key), $"VietnameseUiTerms missing: {key}");
        }
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
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo, billType })
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<Guid> UpsertIntegrationAsync(
        Guid tenantId,
        string sourceSystem,
        string externalObjectType,
        string externalId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/integration-records")
        {
            Content = JsonContent.Create(new { sourceSystem, externalObjectType, externalId })
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private async Task<List<AuditEventDto>> ListAuditEventsAsync(
        Guid tenantId,
        string? correlationId = null)
    {
        var url = "/api/audit-events";
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            url += $"?correlationId={Uri.EscapeDataString(correlationId)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AuditEventDto>>(JsonOptions))!;
    }

    private async Task<List<IntegrationRecordDto>> ListIntegrationsAsync(Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/integration-records");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<IntegrationRecordDto>>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);

    private sealed record AuditEventDto(
        Guid Id,
        Guid? ActorId,
        string Action,
        string ObjectType,
        Guid ObjectId,
        string? BeforeJson,
        string? AfterJson,
        string? Reason,
        string? CorrelationId,
        DateTimeOffset OccurredAt);

    private sealed record IntegrationRecordDto(
        Guid Id,
        string SourceSystem,
        string ExternalObjectType,
        string ExternalId,
        string Status);
}
