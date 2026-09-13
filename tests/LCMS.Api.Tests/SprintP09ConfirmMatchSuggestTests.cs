using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class SprintP09ConfirmMatchSuggestTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public SprintP09ConfirmMatchSuggestTests(LcmsApiFactory factory)
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
    public async Task Confirm_DraftWithActiveDetail_ThenBlocksAdd()
    {
        var tenantId = await CreateTenantAsync("TN-P09-CF", "P09 Confirm");
        var billId = await CreateBillAsync(tenantId, "BL-P09-CF", "freight");
        var costId = await CreateCostAsync(tenantId, billId, 1_000m);
        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-P09-CF", 1_000m, "payable");
        var lineId = await AddLineAsync(tenantId, docId, 1_000m, "Freight");
        await AcceptDocumentAsync(tenantId, docId);

        var matchId = await StartMatchAsync(tenantId, docId, "line_to_cost", toleranceAmount: 0m);
        await AddMatchDetailAsync(tenantId, matchId, new
        {
            sourceLineId = lineId,
            targetCostId = costId,
            matchedAmount = 1_000m
        });

        using (var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/confirm"))
        {
            confirm.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(confirm);
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        }

        using (var get = new HttpRequestMessage(HttpMethod.Get, $"/api/document-matches/{matchId}"))
        {
            get.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(get);
            res.EnsureSuccessStatusCode();
            var match = await res.Content.ReadFromJsonAsync<MatchDto>(JsonOptions);
            Assert.Equal("confirmed", match!.MatchStatus);
            Assert.NotNull(match.ConfirmedAt);
        }

        using (var again = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/confirm"))
        {
            again.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(again);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }

        using (var add = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceLineId = lineId,
                targetCostId = costId,
                matchedAmount = 1m
            })
        })
        {
            add.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(add);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }
    }

    [Fact]
    public async Task Confirm_WithoutActiveDetail_Conflicts()
    {
        var tenantId = await CreateTenantAsync("TN-P09-EMPTY", "P09 Empty");
        var billId = await CreateBillAsync(tenantId, "BL-P09-E", "freight");
        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-P09-E", 100m, "payable");
        var lineId = await AddLineAsync(tenantId, docId, 100m, "X");
        await AcceptDocumentAsync(tenantId, docId);
        _ = lineId;

        var matchId = await StartMatchAsync(tenantId, docId, "line_to_cost");
        using var confirm = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/confirm");
        confirm.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(confirm);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Suggestions_WithinTolerance_IncludesCost_ExcludesBeyond()
    {
        var tenantId = await CreateTenantAsync("TN-P09-SG", "P09 Suggest");
        var billId = await CreateBillAsync(tenantId, "BL-P09-SG", "freight");
        var costNear = await CreateCostAsync(tenantId, billId, 1_000m);
        var costFar = await CreateCostAsync(tenantId, billId, 2_000m);
        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-P09-SG", 1_005m, "payable");
        var lineId = await AddLineAsync(tenantId, docId, 1_005m, "Freight");
        await AcceptDocumentAsync(tenantId, docId);

        var matchId = await StartMatchAsync(
            tenantId, docId, "line_to_cost", toleranceAmount: 10m, tolerancePercent: 0m);

        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/document-matches/{matchId}/suggestions");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var list = await res.Content.ReadFromJsonAsync<List<SuggestionDto>>(JsonOptions);
        Assert.NotNull(list);
        Assert.Contains(list!, s => s.TargetId == costNear && s.TargetKind == "cost");
        Assert.DoesNotContain(list!, s => s.TargetId == costFar);
        var near = Assert.Single(list!, s => s.TargetId == costNear);
        Assert.Equal(lineId, near.SourceLineId);
        Assert.True(near.AmountDelta <= 10m);
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

    private async Task<Guid> CreateCostAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount,
                currencyCode = "VND",
                costTypeCode = "P09"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task AcceptDocumentAsync(Guid tenantId, Guid documentId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{documentId}/accept");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> ReceiveDocumentAsync(
        Guid tenantId,
        Guid billId,
        string documentNo,
        decimal total,
        string direction)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/financial-documents")
        {
            Content = JsonContent.Create(new
            {
                documentType = "invoice",
                documentNo,
                direction,
                totalAmount = total,
                currencyCode = "VND",
                billId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> AddLineAsync(Guid tenantId, Guid documentId, decimal amount, string description)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{documentId}/lines")
        {
            Content = JsonContent.Create(new { amount, description })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> StartMatchAsync(
        Guid tenantId,
        Guid primaryDocumentId,
        string matchMethod,
        decimal? toleranceAmount = null,
        decimal? tolerancePercent = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/document-matches")
        {
            Content = JsonContent.Create(new
            {
                primaryDocumentId,
                matchMethod,
                toleranceAmount,
                tolerancePercent
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task AddMatchDetailAsync(Guid tenantId, Guid matchId, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/details")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private sealed record IdResponse(Guid Id);

    private sealed record MatchDto(string MatchStatus, DateTimeOffset? ConfirmedAt);

    private sealed record SuggestionDto(
        Guid SourceLineId,
        Guid TargetId,
        string TargetKind,
        decimal AmountDelta);
}
