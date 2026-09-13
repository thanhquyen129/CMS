using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class SprintP08AutoExposureFromMatchTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public SprintP08AutoExposureFromMatchTests(LcmsApiFactory factory)
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
    public async Task CreateExposures_FromLineToCost_Idempotent_NoInventCost()
    {
        var tenantId = await CreateTenantAsync("TN-P08-AP", "P08 AP");
        var billId = await CreateBillAsync(tenantId, "BL-P08-AP", "freight");
        var costId = await CreateCostAsync(tenantId, billId, 1_000m);
        var costsBefore = await CountCostsAsync(tenantId);

        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-P08-AP", 1_000m, "payable");
        var lineId = await AddLineAsync(tenantId, docId, 1_000m, "Freight");
        await AcceptDocumentAsync(tenantId, docId);

        var matchId = await StartMatchAsync(tenantId, docId, "line_to_cost");
        await AddMatchDetailAsync(tenantId, matchId, new
        {
            sourceLineId = lineId,
            targetCostId = costId,
            matchedAmount = 1_000m
        });

        using (var propose = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/document-matches/{matchId}/exposure-proposals"))
        {
            propose.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(propose);
            res.EnsureSuccessStatusCode();
            var list = await res.Content.ReadFromJsonAsync<List<ProposalDto>>(JsonOptions);
            Assert.NotNull(list);
            var p = Assert.Single(list!);
            Assert.Equal("payable", p.Kind);
            Assert.False(p.AlreadyExists);
            Assert.Equal(costId, p.CostId);
        }

        Guid exposureId;
        using (var create = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/document-matches/{matchId}/create-exposures"))
        {
            create.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(create);
            res.EnsureSuccessStatusCode();
            var result = await res.Content.ReadFromJsonAsync<CreateResultDto>(JsonOptions);
            Assert.NotNull(result);
            Assert.Equal(1, result!.CreatedCount);
            exposureId = Assert.Single(result.CreatedPayableExposureIds);
        }

        Assert.Equal(costsBefore, await CountCostsAsync(tenantId));

        using (var again = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/document-matches/{matchId}/create-exposures"))
        {
            again.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(again);
            res.EnsureSuccessStatusCode();
            var result = await res.Content.ReadFromJsonAsync<CreateResultDto>(JsonOptions);
            Assert.Equal(0, result!.CreatedCount);
            Assert.Equal(1, result.SkippedExistingCount);
        }

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/api/payable-exposures/{exposureId}");
        get.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var expRes = await _client.SendAsync(get);
        expRes.EnsureSuccessStatusCode();
        var exp = await expRes.Content.ReadFromJsonAsync<ExposureDto>(JsonOptions);
        Assert.Equal(costId, exp!.CostId);
        Assert.Equal(docId, exp.FinancialDocumentId);
        Assert.Equal(1_000m, exp.Amount);
    }

    [Fact]
    public async Task LineToLine_Skipped_DoesNotCreateExposure()
    {
        var tenantId = await CreateTenantAsync("TN-P08-L2L", "P08 L2L");
        var billId = await CreateBillAsync(tenantId, "BL-P08-L2L", "freight");
        var docA = await ReceiveDocumentAsync(tenantId, billId, "INV-A", 100m, "payable");
        var docB = await ReceiveDocumentAsync(tenantId, billId, "INV-B", 100m, "payable");
        var lineA = await AddLineAsync(tenantId, docA, 100m, "A");
        var lineB = await AddLineAsync(tenantId, docB, 100m, "B");
        await AcceptDocumentAsync(tenantId, docA);
        await AcceptDocumentAsync(tenantId, docB);

        var matchId = await StartMatchAsync(tenantId, docA, "line_to_line");
        await AddMatchDetailAsync(tenantId, matchId, new
        {
            sourceLineId = lineA,
            targetLineId = lineB,
            matchedAmount = 100m
        });

        using var create = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/document-matches/{matchId}/create-exposures");
        create.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(create);
        res.EnsureSuccessStatusCode();
        var result = await res.Content.ReadFromJsonAsync<CreateResultDto>(JsonOptions);
        Assert.Equal(0, result!.CreatedCount);
        Assert.True(result.SkippedIneligibleCount >= 1);
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
                costTypeCode = "P08"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<int> CountCostsAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/costs");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var list = await (await _client.SendAsync(req)).Content
            .ReadFromJsonAsync<List<object>>(JsonOptions);
        return list?.Count ?? 0;
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

    private async Task<Guid> StartMatchAsync(Guid tenantId, Guid primaryDocumentId, string matchMethod)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/document-matches")
        {
            Content = JsonContent.Create(new { primaryDocumentId, matchMethod })
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

    private sealed record ProposalDto(
        string Kind,
        Guid? CostId,
        bool AlreadyExists);

    private sealed record CreateResultDto(
        int CreatedCount,
        int SkippedExistingCount,
        int SkippedIneligibleCount,
        List<Guid> CreatedPayableExposureIds);

    private sealed record ExposureDto(
        Guid? CostId,
        Guid? FinancialDocumentId,
        decimal Amount);
}
