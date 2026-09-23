using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Domain.Entities;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class BusinessPartyDirectoryFullTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public BusinessPartyDirectoryFullTests(LcmsApiFactory factory)
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
    public async Task Lookup_ByPhone_And_Block_Rejects_Document()
    {
        var tenantId = await CreateTenantAsync("TN-PARTY-DIR", "Party Dir");

        using var create = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        create.Content = JsonContent.Create(new
        {
            code = "",
            name = "Khách SĐT",
            phone = "0909111222",
            taxId = "0319999999",
            countryCode = "VN",
            creditControlMode = "block",
            creditLimit = 1000m,
            creditLimitCurrencyCode = "VND",
            roleCodes = new[] { PartyRoleCodes.Customer, PartyRoleCodes.Payer }
        });
        var createRes = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var partyId = (await createRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var get = WithTenant(HttpMethod.Get, $"/api/business-parties/{partyId}", tenantId);
        var detail = await (await _client.SendAsync(get)).Content.ReadFromJsonAsync<PartyDetail>(JsonOptions);
        Assert.StartsWith("DT-", detail!.Code);
        Assert.Equal("block", detail.CreditControlMode);

        using var lookup = WithTenant(HttpMethod.Get, "/api/business-parties/lookup?q=0909111222", tenantId);
        var hits = await (await _client.SendAsync(lookup)).Content.ReadFromJsonAsync<List<LookupItem>>(JsonOptions);
        Assert.Contains(hits!, h => h.Id == partyId);

        using var dup = WithTenant(HttpMethod.Get, "/api/business-parties/duplicates?phone=0909111222", tenantId);
        var dups = await (await _client.SendAsync(dup)).Content.ReadFromJsonAsync<List<DupHit>>(JsonOptions);
        Assert.Contains(dups!, d => d.Id == partyId && d.MatchOn == "phone");

        using var block = WithTenant(HttpMethod.Post, $"/api/business-parties/{partyId}/block", tenantId);
        block.Content = JsonContent.Create(new { reason = "Vượt hạn mức UAT" });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(block)).StatusCode);

        using var bill = WithTenant(HttpMethod.Post, "/api/bills", tenantId);
        bill.Content = JsonContent.Create(new { billNo = "BL-PARTY-1", billType = "freight", customerPartyId = partyId });
        var billRes = await _client.SendAsync(bill);
        Assert.Equal(HttpStatusCode.Conflict, billRes.StatusCode);

        using var doc = WithTenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        doc.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-BLOCK-1",
            direction = "receivable",
            totalAmount = 10m,
            currencyCode = "VND",
            counterpartyId = partyId
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(doc)).StatusCode);

        using var directory = WithTenant(HttpMethod.Get, "/api/business-parties/directory?status=blocked&page=1&pageSize=20", tenantId);
        var page = await (await _client.SendAsync(directory)).Content.ReadFromJsonAsync<DirectoryPage>(JsonOptions);
        Assert.Contains(page!.Items, i => i.Id == partyId && i.StatusCode == "blocked");

        using var financial = WithTenant(HttpMethod.Get, $"/api/business-parties/{partyId}/financial", tenantId);
        var finRes = await _client.SendAsync(financial);
        var finBody = await finRes.Content.ReadAsStringAsync();
        Assert.True(finRes.IsSuccessStatusCode, finBody);
        var fin = JsonSerializer.Deserialize<FinView>(finBody, JsonOptions);
        Assert.Equal(partyId, fin!.PartyId);
    }

    [Fact]
    public async Task CarrierOnly_AppearsOnVendorDirectory_NotCustomer()
    {
        var tenantId = await CreateTenantAsync("TN-PARTY-CARR", "Party Carrier");
        using var create = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        create.Content = JsonContent.Create(new
        {
            code = "VEN-UAT-AIR",
            name = "Singapore Airlines Cargo UAT",
            roleCodes = new[] { PartyRoleCodes.Carrier }
        });
        var partyId = (await (await _client.SendAsync(create)).Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var vendors = WithTenant(HttpMethod.Get, "/api/business-parties/directory?roleCode=vendor&page=1&pageSize=50", tenantId);
        var vendorPage = await (await _client.SendAsync(vendors)).Content.ReadFromJsonAsync<DirectoryPage>(JsonOptions);
        Assert.Contains(vendorPage!.Items, i => i.Id == partyId);

        using var customers = WithTenant(HttpMethod.Get, "/api/business-parties/directory?roleCode=customer&page=1&pageSize=50", tenantId);
        var customerPage = await (await _client.SendAsync(customers)).Content.ReadFromJsonAsync<DirectoryPage>(JsonOptions);
        Assert.DoesNotContain(customerPage!.Items, i => i.Id == partyId);
    }

    [Fact]
    public async Task Inactive_Vendor_Cannot_Receive_Payable_Document()
    {
        var tenantId = await CreateTenantAsync("TN-PARTY-INACT", "Party Inact");

        using var create = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        create.Content = JsonContent.Create(new
        {
            code = "V-OFF",
            name = "NCC ngừng",
            roleCodes = new[] { PartyRoleCodes.Vendor, PartyRoleCodes.Payee }
        });
        var partyId = (await (await _client.SendAsync(create)).Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var update = WithTenant(HttpMethod.Put, $"/api/business-parties/{partyId}", tenantId);
        update.Content = JsonContent.Create(new { name = "NCC ngừng", isActive = false });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(update)).StatusCode);

        using var doc = WithTenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        doc.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-OFF-1",
            direction = "payable",
            totalAmount = 1m,
            currencyCode = "VND",
            counterpartyId = partyId
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(doc)).StatusCode);
    }

    [Fact]
    public async Task CreditBlock_RoleGate_SoftDeleteOpenAr_And_Export()
    {
        var tenantId = await CreateTenantAsync("TN-PARTY-CREDIT", "Party Credit");

        using var customerOnly = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        customerOnly.Content = JsonContent.Create(new
        {
            code = "C-ONLY",
            name = "Chỉ khách",
            roleCodes = new[] { PartyRoleCodes.Customer }
        });
        var customerId = (await (await _client.SendAsync(customerOnly)).Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var cost = WithTenant(HttpMethod.Post, "/api/costs", tenantId);
        cost.Content = JsonContent.Create(new
        {
            attributionType = "shared",
            amount = 10m,
            currencyCode = "VND",
            vendorPartyId = customerId
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(cost)).StatusCode);

        using var payer = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        payer.Content = JsonContent.Create(new
        {
            code = "AR-BLOCK",
            name = "Khách hạn mức",
            creditControlMode = "block",
            creditLimit = 1000m,
            creditLimitCurrencyCode = "VND",
            roleCodes = new[] { PartyRoleCodes.Customer, PartyRoleCodes.Payer }
        });
        var arPartyId = (await (await _client.SendAsync(payer)).Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var exposure = WithTenant(HttpMethod.Post, "/api/receivable-exposures", tenantId);
        exposure.Content = JsonContent.Create(new
        {
            amount = 5000m,
            currencyCode = "VND",
            counterpartyId = arPartyId
        });
        var expRes = await _client.SendAsync(exposure);
        expRes.EnsureSuccessStatusCode();
        var exposureId = (await expRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var recognizeOver = WithTenant(
            HttpMethod.Post,
            $"/api/receivable-exposures/{exposureId}/recognize",
            tenantId);
        recognizeOver.Content = JsonContent.Create(new { amount = 1500m });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(recognizeOver)).StatusCode);

        using var recognizeOk = WithTenant(
            HttpMethod.Post,
            $"/api/receivable-exposures/{exposureId}/recognize",
            tenantId);
        recognizeOk.Content = JsonContent.Create(new { amount = 400m });
        var recRes = await _client.SendAsync(recognizeOk);
        Assert.Equal(HttpStatusCode.Created, recRes.StatusCode);

        using var del = WithTenant(HttpMethod.Delete, $"/api/business-parties/{arPartyId}", tenantId);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(del)).StatusCode);

        using var export = WithTenant(HttpMethod.Get, "/api/business-parties/export?search=AR-BLOCK", tenantId);
        var exportRes = await _client.SendAsync(export);
        exportRes.EnsureSuccessStatusCode();
        var csv = await exportRes.Content.ReadAsStringAsync();
        Assert.Contains("AR-BLOCK", csv);
        Assert.Contains("ma,ten", csv);
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private static HttpRequestMessage WithTenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record PartyDetail(string Code, string CreditControlMode);
    private sealed record LookupItem(Guid Id);
    private sealed record DupHit(Guid Id, string MatchOn);
    private sealed record DirectoryPage(IReadOnlyList<DirectoryItem> Items, int TotalCount);
    private sealed record DirectoryItem(Guid Id, string StatusCode);
    private sealed record FinView(Guid PartyId);
}
