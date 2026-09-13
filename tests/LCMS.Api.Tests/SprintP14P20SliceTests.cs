using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class SprintP14P20SliceTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public SprintP14P20SliceTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task BankFeed_ImportCsv_CreatesLines()
    {
        var tenantId = await CreateTenantAsync("TN-P15", "P15 CSV");
        var csv =
            "valueDate,amount,currencyCode,direction,bankReference,counterpartyName,description\n" +
            "2026-09-01,1000,VND,credit,REF-P15-1,Vendor A,Thu cước\n" +
            "2026-09-02,500,VND,debit,REF-P15-2,Vendor B,Chi phí\n";

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bank-feed/lines/import-csv")
        {
            Content = JsonContent.Create(new { csv })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var result = await res.Content.ReadFromJsonAsync<ImportResult>(JsonOptions);
        Assert.Equal(2, result!.Imported);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public async Task TenantSettings_RecognitionPolicy_BlocksRecognizeWithoutDoc()
    {
        var tenantId = await CreateTenantAsync("TN-P16", "P16 Policy");
        using (var put = new HttpRequestMessage(HttpMethod.Put, "/api/tenant-settings")
        {
            Content = JsonContent.Create(new
            {
                financialJson = """{"recognitionPolicyMode":"require_document_link","recognitionPolicyVersion":"p16-test"}"""
            })
        })
        {
            put.Headers.Add("X-Tenant-Id", tenantId.ToString());
            (await _client.SendAsync(put)).EnsureSuccessStatusCode();
        }

        var billId = await CreateBillAsync(tenantId, "BL-P16", "freight");
        Guid expId;
        using (var exp = new HttpRequestMessage(HttpMethod.Post, "/api/payable-exposures")
        {
            Content = JsonContent.Create(new { amount = 100m, currencyCode = "VND", billId })
        })
        {
            exp.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(exp);
            res.EnsureSuccessStatusCode();
            expId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        using var recognize = new HttpRequestMessage(
            HttpMethod.Post, $"/api/payable-exposures/{expId}/recognize")
        {
            Content = JsonContent.Create(new { amount = 100m })
        };
        recognize.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var recRes = await _client.SendAsync(recognize);
        Assert.Equal(HttpStatusCode.Conflict, recRes.StatusCode);
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

    private sealed record IdResponse(Guid Id);
    private sealed record ImportResult(int Imported, int Skipped);
}
