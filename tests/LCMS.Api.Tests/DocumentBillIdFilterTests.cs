using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class DocumentBillIdFilterTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public DocumentBillIdFilterTests(LcmsApiFactory factory)
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
    public async Task ListFinancialDocuments_FiltersByHeaderOrLineBillId()
    {
        var tenantId = await CreateTenantAsync("TN-DOC-BILL", "Doc Bill Filter");
        var billA = await CreateBillAsync(tenantId, "BL-A", "freight");
        var billB = await CreateBillAsync(tenantId, "BL-B", "freight");

        var headerOnA = await ReceiveDocumentAsync(tenantId, billA, "INV-A", 1000m);
        var headerOnB = await ReceiveDocumentAsync(tenantId, billB, "INV-B", 2000m);

        // Header null; line attaches Bill A
        var lineOnlyA = await ReceiveDocumentAsync(tenantId, billId: null, "INV-LINE-A", 500m);
        await AddLineAsync(tenantId, lineOnlyA, 500m, "line A", billA);

        var all = await ListDocumentsAsync(tenantId);
        Assert.Equal(3, all.Count);

        var forA = await ListDocumentsAsync(tenantId, billA);
        Assert.Equal(2, forA.Count);
        Assert.Contains(forA, d => d.Id == headerOnA);
        Assert.Contains(forA, d => d.Id == lineOnlyA);
        Assert.DoesNotContain(forA, d => d.Id == headerOnB);

        var forB = await ListDocumentsAsync(tenantId, billB);
        Assert.Single(forB);
        Assert.Equal(headerOnB, forB[0].Id);
        Assert.Equal(billB, forB[0].BillId);
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

    private async Task<Guid> ReceiveDocumentAsync(
        Guid tenantId,
        Guid? billId,
        string documentNo,
        decimal total)
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

    private async Task<Guid> AddLineAsync(
        Guid tenantId,
        Guid documentId,
        decimal amount,
        string description,
        Guid? billId = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{documentId}/lines")
        {
            Content = JsonContent.Create(new { amount, description, billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<List<DocumentListItem>> ListDocumentsAsync(Guid tenantId, Guid? billId = null)
    {
        var path = billId.HasValue
            ? $"/api/financial-documents?billId={billId.Value}"
            : "/api/financial-documents";
        using var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<DocumentListItem>>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record DocumentListItem(
        Guid Id,
        string DocumentNo,
        Guid? BillId);
}
