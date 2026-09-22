using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class OperationalCargoTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public OperationalCargoTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task ConfirmedChargeable_RequiresReasonBeforeADifferentQuantity()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-CW-1");

        Assert.Equal(HttpStatusCode.NoContent, await PatchContext(tenantId, billId, new
        {
            context = new { chargeableWeightKg = 10m, chargeableConfirmed = true }
        }));

        Assert.Equal(HttpStatusCode.Conflict, await PatchContext(tenantId, billId, new
        {
            context = new { chargeableWeightKg = 12m, chargeableConfirmed = true }
        }));

        Assert.Equal(HttpStatusCode.NoContent, await PatchContext(tenantId, billId, new
        {
            context = new { chargeableWeightKg = 12m, chargeableConfirmed = true, chargeableOverrideReason = "Cân lại tại kho" }
        }));
    }

    [Fact]
    public async Task ImportPreview_BadRow_DoesNotCommit()
    {
        var tenantId = await CreateTenantAsync();
        var body = new
        {
            sourceSystem = "tms-b",
            rows = new object[]
            {
                new { objectType = "bill", businessNo = "BL-IMP-OK", externalId = "ext-ok", originCode = "SGN", destinationCode = "LAX", grossWeightKg = 10m },
                new { objectType = "bill", businessNo = "BL-IMP-BAD", externalId = "", originCode = "SGN", destinationCode = "SGN" }
            }
        };

        using var preview = WithTenant(HttpMethod.Post, "/api/operational-import/preview", tenantId);
        preview.Content = JsonContent.Create(body);
        var previewRes = await _client.SendAsync(preview);
        Assert.Equal(HttpStatusCode.OK, previewRes.StatusCode);
        var previewBody = await previewRes.Content.ReadFromJsonAsync<PreviewBody>(Json);
        Assert.False(previewBody!.CanCommit);
        Assert.Contains(previewBody.Issues, i => i.Field == "externalId");
        Assert.DoesNotContain(await ListBillNos(tenantId, "BL-IMP-"), n => n.StartsWith("BL-IMP-"));

        using var commit = WithTenant(HttpMethod.Post, "/api/operational-import/commit", tenantId);
        commit.Content = JsonContent.Create(body);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(commit)).StatusCode);
        Assert.DoesNotContain(await ListBillNos(tenantId, "BL-IMP-"), n => n.StartsWith("BL-IMP-"));
    }

    [Fact]
    public async Task ImportCommit_ThenManualOriginChange_RequiresOverride_AndLinkIsAudited()
    {
        var tenantId = await CreateTenantAsync();
        using var commit = WithTenant(HttpMethod.Post, "/api/operational-import/commit", tenantId);
        commit.Content = JsonContent.Create(new
        {
            sourceSystem = "tms-b",
            rows = new[]
            {
                new { objectType = "bill", businessNo = "BL-OWN-1", externalId = "ext-own", originCode = "SGN", destinationCode = "LAX", chargeableWeightKg = 8m }
            }
        });
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(commit)).StatusCode);

        var billId = (await ListBills(tenantId, "BL-OWN-1")).Single(b => b.BillNo == "BL-OWN-1").Id;
        Assert.Equal(HttpStatusCode.Conflict, await PatchContext(tenantId, billId, new { originCode = "HAN" }));
        Assert.Equal(HttpStatusCode.NoContent, await PatchContext(tenantId, billId, new
        {
            originCode = "HAN",
            context = new { chargeableOverrideReason = "Nguồn TMS ghi sai điểm đi" }
        }));

        var orderId = await UpsertOrderAsync(tenantId);
        using var link = WithTenant(HttpMethod.Post, $"/api/orders/{orderId}/bills/{billId}", tenantId);
        var linkRes = await _client.SendAsync(link);
        Assert.Equal(HttpStatusCode.OK, linkRes.StatusCode);
        var linkId = (await linkRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        using var audit = WithTenant(HttpMethod.Get, $"/api/audit-events?objectId={linkId}&action=link.create", tenantId);
        var auditRes = await _client.SendAsync(audit);
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);
        var events = await auditRes.Content.ReadFromJsonAsync<AuditBody[]>(Json);
        Assert.Contains(events!, e => e.Action == "link.create" && e.ObjectId == linkId);
    }

    private async Task<HttpStatusCode> PatchContext(Guid tenantId, Guid billId, object body)
    {
        using var req = WithTenant(HttpMethod.Patch, $"/api/bills/{billId}/context", tenantId);
        req.Content = JsonContent.Create(body);
        return (await _client.SendAsync(req)).StatusCode;
    }

    private async Task<IReadOnlyList<string>> ListBillNos(Guid tenantId, string q)
    {
        var bills = await ListBills(tenantId, q);
        return bills.Select(b => b.BillNo).ToArray();
    }

    private async Task<List<BillBody>> ListBills(Guid tenantId, string q)
    {
        using var req = WithTenant(HttpMethod.Get, "/api/bills?q=" + Uri.EscapeDataString(q), tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<BillBody>>(Json)) ?? [];
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> UpsertOrderAsync(Guid tenantId)
    {
        using var req = WithTenant(HttpMethod.Put, "/api/orders", tenantId);
        req.Content = JsonContent.Create(new { orderNo = "OR-OWN-1", sourceSystem = "lcms_manual", externalId = "or-own-1", isActive = true });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "Cargo" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private static HttpRequestMessage WithTenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record BillBody(Guid Id, string BillNo);
    private sealed record PreviewBody(bool CanCommit, List<IssueBody> Issues);
    private sealed record IssueBody(int Row, string Field, string Message);
    private sealed record AuditBody(string Action, Guid ObjectId);
}
