using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class BusinessPartyMasterFullTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public BusinessPartyMasterFullTests(LcmsApiFactory factory)
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
    public async Task Create_List_Filter_Update_Bank_Contact_And_Roles()
    {
        var tenantId = await CreateTenantAsync("TN-PARTY-FULL", "Party Full");

        using var create = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        create.Content = JsonContent.Create(new
        {
            code = "VND-FULL",
            name = "Nhà xe Full",
            legalName = "Công ty TNHH Nhà xe Full",
            taxId = "0311111111",
            phone = "0281111111",
            email = "full@example.com",
            addressLine1 = "123 Đường Demo",
            city = "HCM",
            province = "HCM",
            countryCode = "VN",
            defaultCurrencyCode = "VND",
            paymentTermDays = 15,
            creditLimit = 100000000m,
            creditLimitCurrencyCode = "VND",
            notes = "Đối tác UAT",
            roleCodes = new[] { "vendor", "payee" }
        });
        var createRes = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var partyId = (await createRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var dupTax = WithTenant(HttpMethod.Post, "/api/business-parties", tenantId);
        dupTax.Content = JsonContent.Create(new
        {
            code = "OTHER",
            name = "Other",
            taxId = "0311111111"
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(dupTax)).StatusCode);

        using var list = WithTenant(
            HttpMethod.Get,
            "/api/business-parties?search=0311111111&roleCode=vendor&isActive=true",
            tenantId);
        var items = await (await _client.SendAsync(list)).Content
            .ReadFromJsonAsync<List<PartyListItem>>(JsonOptions);
        var row = Assert.Single(items!);
        Assert.Equal(partyId, row.Id);
        Assert.Equal("0311111111", row.TaxId);
        Assert.Contains("vendor", row.RoleCodes);
        Assert.Contains("payee", row.RoleCodes);

        using var get = WithTenant(HttpMethod.Get, $"/api/business-parties/{partyId}", tenantId);
        var detail = await (await _client.SendAsync(get)).Content
            .ReadFromJsonAsync<PartyDetail>(JsonOptions);
        Assert.Equal("Công ty TNHH Nhà xe Full", detail!.LegalName);
        Assert.Equal(15, detail.PaymentTermDays);
        Assert.Equal(100000000m, detail.CreditLimit);

        using var update = WithTenant(HttpMethod.Put, $"/api/business-parties/{partyId}", tenantId);
        update.Content = JsonContent.Create(new
        {
            name = "Nhà xe Full (sửa)",
            isActive = true,
            legalName = detail.LegalName,
            taxId = detail.TaxId,
            phone = detail.Phone,
            email = detail.Email,
            defaultCurrencyCode = "VND",
            paymentTermDays = 30,
            creditLimit = 200000000m,
            creditLimitCurrencyCode = "VND",
            countryCode = "VN"
        });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(update)).StatusCode);

        using var bank = WithTenant(HttpMethod.Post, $"/api/business-parties/{partyId}/bank-accounts", tenantId);
        bank.Content = JsonContent.Create(new
        {
            bankName = "Vietcombank",
            bankBranch = "HCM",
            accountNumber = "0011223344",
            accountName = "NHA XE FULL",
            currencyCode = "VND",
            isDefault = true,
            isActive = true,
            note = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(bank)).StatusCode);

        using var banks = WithTenant(HttpMethod.Get, $"/api/business-parties/{partyId}/bank-accounts", tenantId);
        var bankList = await (await _client.SendAsync(banks)).Content
            .ReadFromJsonAsync<List<BankItem>>(JsonOptions);
        Assert.Single(bankList!);
        Assert.True(bankList[0].IsDefault);

        using var contact = WithTenant(HttpMethod.Post, $"/api/business-parties/{partyId}/contacts", tenantId);
        contact.Content = JsonContent.Create(new
        {
            fullName = "Nguyễn Văn C",
            title = "Công nợ",
            phone = "0903000003",
            email = "c@example.com",
            isPrimary = true,
            isActive = true,
            note = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(contact)).StatusCode);

        using var contacts = WithTenant(HttpMethod.Get, $"/api/business-parties/{partyId}/contacts", tenantId);
        var contactList = await (await _client.SendAsync(contacts)).Content
            .ReadFromJsonAsync<List<ContactItem>>(JsonOptions);
        Assert.Single(contactList!);
        Assert.True(contactList[0].IsPrimary);
    }

    [Fact]
    public async Task PartyManage_WithoutPermission_Returns403()
    {
        var tenantId = await CreateTenantAsync("TN-PARTY-PERM", "Party Perm");

        using var createUser = WithTenant(HttpMethod.Post, "/api/users", tenantId);
        createUser.Content = JsonContent.Create(new { email = "noparty@example.com", displayName = "No Party" });
        var userId = (await (await _client.SendAsync(createUser)).Content
            .ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var createRole = WithTenant(HttpMethod.Post, "/api/roles", tenantId);
        createRole.Content = JsonContent.Create(new { code = "ReaderOnly", name = "Reader" });
        var roleId = (await (await _client.SendAsync(createRole)).Content
            .ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var perm = WithTenant(HttpMethod.Post, $"/api/roles/{roleId}/permissions", tenantId);
        perm.Content = JsonContent.Create(new { actionCode = "bill.read", dataScope = "all" });
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(perm)).StatusCode);

        using var assign = WithTenant(HttpMethod.Post, $"/api/users/{userId}/roles/{roleId}", tenantId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(assign)).StatusCode);

        using var createParty = WithUser(HttpMethod.Post, "/api/business-parties", tenantId, userId);
        createParty.Content = JsonContent.Create(new { code = "X", name = "X" });
        var res = await _client.SendAsync(createParty);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
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

    private static HttpRequestMessage WithUser(HttpMethod method, string url, Guid tenantId, Guid userId)
    {
        var req = WithTenant(method, url, tenantId);
        req.Headers.Add("X-User-Id", userId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record PartyListItem(
        Guid Id,
        string Code,
        string Name,
        string? TaxId,
        IReadOnlyList<string> RoleCodes);

    private sealed record PartyDetail(
        Guid Id,
        string Name,
        string? LegalName,
        string? TaxId,
        string? Phone,
        string? Email,
        int? PaymentTermDays,
        decimal? CreditLimit);

    private sealed record BankItem(Guid Id, bool IsDefault, string AccountNumber);

    private sealed record ContactItem(Guid Id, bool IsPrimary, string FullName);
}
