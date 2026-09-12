using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class DocumentLineIntegrityTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public DocumentLineIntegrityTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Accept_Requires_LinesSum_Equal_Header_ADR0012()
    {
        var tenantId = await CreateTenantAsync("TN-LINE-SUM", "Line Sum");
        var billId = await CreateBillAsync(tenantId, "BL-LINE-SUM", "freight");
        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-SUM", 1000m);

        using (var acceptEmpty = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{docId}/accept"))
        {
            acceptEmpty.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(acceptEmpty);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }

        await AddLineAsync(tenantId, docId, 400m, "partial");
        using (var acceptPartial = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{docId}/accept"))
        {
            acceptPartial.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(acceptPartial)).StatusCode);
        }

        await AddLineAsync(tenantId, docId, 600m, "rest");
        using (var acceptOk = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{docId}/accept"))
        {
            acceptOk.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(acceptOk)).StatusCode);
        }
    }

    [Fact]
    public async Task AddLine_Rejects_Overshoot_And_UpdateDelete_Work_When_Draft()
    {
        var tenantId = await CreateTenantAsync("TN-LINE-CRUD", "Line CRUD");
        var billId = await CreateBillAsync(tenantId, "BL-LINE-CRUD", "freight");
        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-CRUD", 1000m);
        var lineId = await AddLineAsync(tenantId, docId, 700m, "a");

        using (var over = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{docId}/lines")
        {
            Content = JsonContent.Create(new { amount = 400m, description = "over" })
        })
        {
            over.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(over)).StatusCode);
        }

        using (var update = new HttpRequestMessage(HttpMethod.Put, $"/api/financial-documents/{docId}/lines/{lineId}")
        {
            Content = JsonContent.Create(new { amount = 1000m, description = "full" })
        })
        {
            update.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(update)).StatusCode);
        }

        using (var del = new HttpRequestMessage(HttpMethod.Delete, $"/api/financial-documents/{docId}/lines/{lineId}"))
        {
            del.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(del)).StatusCode);
        }

        var after = await GetDocumentAsync(tenantId, docId);
        Assert.Empty(after.Lines);
    }

    [Fact]
    public async Task AfterAccept_Blocks_Add_And_Invalid_Party_On_Receive()
    {
        var tenantId = await CreateTenantAsync("TN-LINE-ACC", "Line Accept Lock");
        var billId = await CreateBillAsync(tenantId, "BL-LINE-ACC", "freight");
        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-ACC", 500m);
        await AddLineAsync(tenantId, docId, 500m, "one");
        await AcceptDocumentAsync(tenantId, docId);

        using (var add = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{docId}/lines")
        {
            Content = JsonContent.Create(new { amount = 1m, description = "nope" })
        })
        {
            add.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(add)).StatusCode);
        }

        using var badParty = new HttpRequestMessage(HttpMethod.Post, "/api/financial-documents")
        {
            Content = JsonContent.Create(new
            {
                documentType = "invoice",
                documentNo = "INV-BAD-PARTY",
                direction = "payable",
                totalAmount = 10m,
                currencyCode = "VND",
                billId,
                counterpartyId = Guid.NewGuid()
            })
        };
        badParty.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(badParty)).StatusCode);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(new { code, name })
        };
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
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

    private async Task AcceptDocumentAsync(Guid tenantId, Guid documentId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{documentId}/accept");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<DocumentResponse> GetDocumentAsync(Guid tenantId, Guid documentId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-documents/{documentId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record DocumentResponse(Guid Id, IReadOnlyList<LineResponse> Lines);
    private sealed record LineResponse(Guid Id, decimal Amount);
}
