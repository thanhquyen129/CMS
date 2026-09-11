using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint6FinancialDocumentTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint6FinancialDocumentTests(LcmsApiFactory factory)
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
    public async Task Received_Accepted_Matched_AreIndependent_AndReceiveDoesNotCreateCostOrRevenue()
    {
        var tenantId = await CreateTenantAsync("TN-DOC-STATE", "Doc State");
        var billId = await CreateBillAsync(tenantId, "BL-DOC-1", "freight");

        // Baseline counts — receive must not invent Cost/Revenue (C-003/C-004)
        Assert.Empty(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));

        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-001", 1000m);

        var afterReceive = await GetDocumentAsync(tenantId, docId);
        Assert.Equal("received", afterReceive.ReceiptStatus);
        Assert.Equal("not_accepted", afterReceive.AcceptanceStatus);
        Assert.Equal("unmatched", afterReceive.MatchingStatus);
        Assert.NotNull(afterReceive.ReceivedAt);
        Assert.Null(afterReceive.AcceptedAt);

        // Still no Cost/Revenue after receive
        Assert.Empty(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));

        // Accept changes only AcceptanceStatus
        using (var accept = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{docId}/accept"))
        {
            accept.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(accept)).StatusCode);
        }

        var afterAccept = await GetDocumentAsync(tenantId, docId);
        Assert.Equal("received", afterAccept.ReceiptStatus);
        Assert.Equal("accepted", afterAccept.AcceptanceStatus);
        Assert.Equal("unmatched", afterAccept.MatchingStatus);
        Assert.NotNull(afterAccept.AcceptedAt);

        var lineId = await AddLineAsync(tenantId, docId, 1000m, "Cước vận chuyển");
        var costId = await CreateDirectCostAsync(tenantId, billId, 1000m, "FREIGHT");

        var matchId = await StartMatchAsync(tenantId, docId);
        using (var detail = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceLineId = lineId,
                targetCostId = costId,
                matchedAmount = 600m
            })
        })
        {
            detail.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(detail)).StatusCode);
        }

        var afterPartial = await GetDocumentAsync(tenantId, docId);
        Assert.Equal("received", afterPartial.ReceiptStatus);
        Assert.Equal("accepted", afterPartial.AcceptanceStatus);
        Assert.Equal("partially_matched", afterPartial.MatchingStatus);
        Assert.Single(afterPartial.Lines);
        Assert.Equal(600m, afterPartial.Lines[0].MatchedAmount);
        Assert.Equal(400m, afterPartial.Lines[0].OpenAmount);

        // Matching to cost is link-only — still exactly one Cost, zero Revenue invented from document
        Assert.Single(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));

        using (var detail2 = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceLineId = lineId,
                targetCostId = costId,
                matchedAmount = 400m
            })
        })
        {
            detail2.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(detail2)).StatusCode);
        }

        var afterFull = await GetDocumentAsync(tenantId, docId);
        Assert.Equal("matched", afterFull.MatchingStatus);
        Assert.Equal("accepted", afterFull.AcceptanceStatus);
        Assert.Equal("received", afterFull.ReceiptStatus);
    }

    [Fact]
    public async Task OverMatch_IsRejected_C007_ToleranceZero()
    {
        var tenantId = await CreateTenantAsync("TN-DOC-OM", "Doc OverMatch");
        var billId = await CreateBillAsync(tenantId, "BL-OM", "freight");
        var docA = await ReceiveDocumentAsync(tenantId, billId, "DN-A", 500m);
        var docB = await ReceiveDocumentAsync(tenantId, billId, "INV-B", 500m);
        var lineA = await AddLineAsync(tenantId, docA, 500m, "DN line");
        var lineB = await AddLineAsync(tenantId, docB, 500m, "INV line");

        var matchId = await StartMatchAsync(tenantId, docA);

        using (var ok = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceLineId = lineA,
                targetLineId = lineB,
                matchedAmount = 500m
            })
        })
        {
            ok.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(ok)).StatusCode);
        }

        using (var over = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceLineId = lineA,
                targetLineId = lineB,
                matchedAmount = 0.01m
            })
        })
        {
            over.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(over);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Equal("concurrency_conflict", err!.Code);
            Assert.Contains("C-007", err.Message, StringComparison.OrdinalIgnoreCase);
        }

        var doc = await GetDocumentAsync(tenantId, docA);
        Assert.Equal(500m, doc.Lines[0].MatchedAmount);
        Assert.Equal(0m, doc.Lines[0].OpenAmount);
        Assert.Equal("matched", doc.MatchingStatus);
    }

    [Fact]
    public async Task FinancialDocument_CrossTenant_Returns404()
    {
        var tenantA = await CreateTenantAsync("TN-DOC-A", "Doc A");
        var tenantB = await CreateTenantAsync("TN-DOC-B", "Doc B");
        var billId = await CreateBillAsync(tenantA, "BL-ISO-DOC", "freight");
        var docId = await ReceiveDocumentAsync(tenantA, billId, "INV-ISO", 100m);
        var lineId = await AddLineAsync(tenantA, docId, 100m, "line");
        var matchId = await StartMatchAsync(tenantA, docId);

        using var getDoc = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-documents/{docId}");
        getDoc.Headers.Add("X-Tenant-Id", tenantB.ToString());
        var leakedDoc = await _client.SendAsync(getDoc);
        Assert.Equal(HttpStatusCode.NotFound, leakedDoc.StatusCode);

        using var getMatch = new HttpRequestMessage(HttpMethod.Get, $"/api/document-matches/{matchId}");
        getMatch.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(getMatch)).StatusCode);

        using var acceptAsB = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{docId}/accept");
        acceptAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(acceptAsB)).StatusCode);

        using var detailAsB = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceLineId = lineId,
                targetCostId = Guid.NewGuid(),
                matchedAmount = 10m
            })
        };
        detailAsB.Headers.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(detailAsB)).StatusCode);

        var listB = await ListDocumentsAsync(tenantB);
        Assert.Empty(listB);
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

    private async Task<Guid> ReceiveDocumentAsync(Guid tenantId, Guid billId, string documentNo, decimal total)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/financial-documents")
        {
            Content = JsonContent.Create(new
            {
                documentType = "invoice",
                documentNo,
                direction = "payable",
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

    private async Task<Guid> StartMatchAsync(Guid tenantId, Guid primaryDocumentId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/document-matches")
        {
            Content = JsonContent.Create(new { primaryDocumentId, matchMethod = "manual" })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
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

    private async Task<DocumentResponse> GetDocumentAsync(Guid tenantId, Guid documentId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-documents/{documentId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions))!;
    }

    private async Task<List<DocumentListItem>> ListDocumentsAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/financial-documents");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<DocumentListItem>>(JsonOptions))!;
    }

    private async Task<List<object>> ListCostsAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/costs");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<object>>(JsonOptions))!;
    }

    private async Task<List<object>> ListRevenuesAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/revenues");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<object>>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record DocumentLineResponse(
        Guid Id,
        int LineNo,
        string? Description,
        decimal Amount,
        decimal MatchedAmount,
        decimal OpenAmount,
        string CurrencyCode);

    private sealed record DocumentResponse(
        Guid Id,
        string DocumentType,
        string DocumentNo,
        string Direction,
        decimal TotalAmount,
        string CurrencyCode,
        string ReceiptStatus,
        string AcceptanceStatus,
        string MatchingStatus,
        DateTimeOffset? ReceivedAt,
        DateTimeOffset? AcceptedAt,
        List<DocumentLineResponse> Lines);

    private sealed record DocumentListItem(
        Guid Id,
        string DocumentType,
        string DocumentNo,
        string ReceiptStatus,
        string AcceptanceStatus,
        string MatchingStatus);

    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
