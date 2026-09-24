using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class SprintP10ReverseRecognizeTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public SprintP10ReverseRecognizeTests(LcmsApiFactory factory)
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
    public async Task ReverseRecognize_Ap_RestoresExposure_WritesLedger()
    {
        var tenantId = await CreateTenantAsync("TN-P10-AP", "P10 AP");
        var billId = await CreateBillAsync(tenantId, "BL-P10", "freight");
        var expId = await CreatePayableExposureAsync(tenantId, billId, 1_000m);
        var apId = await RecognizePayableAsync(tenantId, expId, 1_000m);

        using (var rev = new HttpRequestMessage(
            HttpMethod.Post, $"/api/accounts-payable/{apId}/reverse-recognize")
        {
            Content = JsonContent.Create(new { reason = "Sai số ghi nhận" })
        })
        {
            rev.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(rev);
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        }

        using (var getAp = new HttpRequestMessage(HttpMethod.Get, $"/api/accounts-payable/{apId}"))
        {
            getAp.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var ap = await (await _client.SendAsync(getAp)).Content
                .ReadFromJsonAsync<ApDto>(JsonOptions);
            Assert.Equal("reversed", ap!.RecordStatus);
            Assert.Equal(0m, ap.Outstanding);
        }

        using (var getExp = new HttpRequestMessage(HttpMethod.Get, $"/api/payable-exposures/{expId}"))
        {
            getExp.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var exp = await (await _client.SendAsync(getExp)).Content
                .ReadFromJsonAsync<ExposureDto>(JsonOptions);
            Assert.Equal(0m, exp!.RecognizedAmount);
            Assert.Equal("open", exp.Status);
        }

        using (var hist = new HttpRequestMessage(
            HttpMethod.Get, $"/api/accounts-payable/{apId}/adjustments"))
        {
            hist.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var list = await (await _client.SendAsync(hist)).Content
                .ReadFromJsonAsync<List<AdjDto>>(JsonOptions);
            Assert.Contains(list!, a => a.AdjustmentType == "reverse_recognize");
        }

        using (var again = new HttpRequestMessage(
            HttpMethod.Post, $"/api/accounts-payable/{apId}/reverse-recognize")
        {
            Content = JsonContent.Create(new { reason = "lần 2" })
        })
        {
            again.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(again);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        }
    }

    [Fact]
    public async Task WriteOff_OverMatrixBand_RequiresLevel2()
    {
        var tenantId = await CreateTenantAsync("TN-P10-MX", "P10 Matrix");
        var billId = await CreateBillAsync(tenantId, "BL-P10-MX", "freight");
        var expId = await CreatePayableExposureAsync(tenantId, billId, 15_000m);
        var apId = await RecognizePayableAsync(tenantId, expId, 15_000m);

        using var wo = new HttpRequestMessage(
            HttpMethod.Post, $"/api/accounts-payable/{apId}/write-off")
        {
            Content = JsonContent.Create(new { amount = 12_000m, reason = "Xóa lớn" })
        };
        wo.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(wo);
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<WriteOffAccepted>(JsonOptions);
        Assert.True(body!.RequiresApproval);
        Assert.Equal(2, body.RequiredLevel);
    }

    [Fact]
    public async Task StaleIfMatch_DoesNotAdjustOrWriteOffPayable()
    {
        var tenantId = await CreateTenantAsync("TN-P10-VER", "P10 Version");
        var billId = await CreateBillAsync(tenantId, "BL-P10-VER", "freight");
        var expId = await CreatePayableExposureAsync(tenantId, billId, 1_000m);
        var apId = await RecognizePayableAsync(tenantId, expId, 1_000m);

        using (var adj = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-payable/{apId}/adjust")
        {
            Content = JsonContent.Create(new { deltaAmount = 10m, reason = "phiên cũ" })
        })
        {
            adj.Headers.Add("X-Tenant-Id", tenantId.ToString());
            adj.Headers.TryAddWithoutValidation("If-Match", "not-a-version");
            var blocked = await _client.SendAsync(adj);
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            var err = await blocked.Content.ReadFromJsonAsync<CodeBody>(JsonOptions);
            Assert.Equal("concurrency_conflict", err!.Code);
        }

        using (var wo = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-payable/{apId}/write-off")
        {
            Content = JsonContent.Create(new { amount = 10m, reason = "phiên cũ" })
        })
        {
            wo.Headers.Add("X-Tenant-Id", tenantId.ToString());
            wo.Headers.TryAddWithoutValidation("If-Match", "not-a-version");
            var blocked = await _client.SendAsync(wo);
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        }

        using var getAp = new HttpRequestMessage(HttpMethod.Get, $"/api/accounts-payable/{apId}");
        getAp.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var ap = await (await _client.SendAsync(getAp)).Content.ReadFromJsonAsync<ApMoney>(JsonOptions);
        Assert.Equal(1_000m, ap!.Outstanding);
        Assert.Equal(0m, ap.AdjustmentAmount);
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

    private async Task<Guid> CreatePayableExposureAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payable-exposures")
        {
            Content = JsonContent.Create(new
            {
                amount,
                currencyCode = "VND",
                billId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> RecognizePayableAsync(Guid tenantId, Guid exposureId, decimal amount)
    {
        using var req = new HttpRequestMessage(
            HttpMethod.Post, $"/api/payable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record ApDto(string RecordStatus, decimal Outstanding);
    private sealed record ApMoney(decimal Outstanding, decimal AdjustmentAmount);
    private sealed record CodeBody(string Code);
    private sealed record ExposureDto(decimal RecognizedAmount, string Status);
    private sealed record AdjDto(string AdjustmentType);
    private sealed record WriteOffAccepted(bool RequiresApproval, int? RequiredLevel);
}
