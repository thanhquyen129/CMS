using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class PartyImportTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public PartyImportTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Preview_BadRow_CanCommitFalse_Then_CommitOk()
    {
        var tenantId = await CreateTenantAsync("TN-PARTY-IMP", "Party Import");

        var badBody = new
        {
            rows = new object[]
            {
                new
                {
                    code = "IMP-OK",
                    name = "Đối tác OK",
                    taxId = "0312222222",
                    countryCode = "VN",
                    isCustomer = true,
                    isPayer = true
                },
                new
                {
                    code = "",
                    name = "Thiếu mã",
                    taxId = "0313333333",
                    countryCode = "VN"
                }
            }
        };

        using var preview = WithTenant(HttpMethod.Post, "/api/party-imports/preview", tenantId);
        preview.Content = JsonContent.Create(badBody);
        var previewRes = await _client.SendAsync(preview);
        Assert.Equal(HttpStatusCode.OK, previewRes.StatusCode);
        var previewBody = await previewRes.Content.ReadFromJsonAsync<PreviewBody>(Json);
        Assert.False(previewBody!.CanCommit);
        Assert.Contains(previewBody.Issues, i => i.Field == "code");

        using var badCommit = WithTenant(HttpMethod.Post, "/api/party-imports/commit", tenantId);
        badCommit.Content = JsonContent.Create(badBody);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(badCommit)).StatusCode);
        Assert.Empty(await ListPartyCodes(tenantId, "IMP-"));

        var goodBody = new
        {
            rows = new object[]
            {
                new
                {
                    code = "IMP-C1",
                    name = "Khách nhập 1",
                    taxId = "0314444444",
                    countryCode = "VN",
                    roleCodes = new[] { "customer", "payer" },
                    defaultCurrencyCode = "VND",
                    paymentTermDays = 30
                },
                new
                {
                    code = "IMP-V1",
                    name = "NCC nhập 1",
                    taxId = "0315555555",
                    countryCode = "VN",
                    isVendor = true,
                    isPayee = true,
                    defaultCurrencyCode = "VND"
                }
            }
        };

        using var goodPreview = WithTenant(HttpMethod.Post, "/api/party-imports/preview", tenantId);
        goodPreview.Content = JsonContent.Create(goodBody);
        var goodPreviewRes = await _client.SendAsync(goodPreview);
        Assert.Equal(HttpStatusCode.OK, goodPreviewRes.StatusCode);
        var goodPreviewBody = await goodPreviewRes.Content.ReadFromJsonAsync<PreviewBody>(Json);
        Assert.True(goodPreviewBody!.CanCommit);
        Assert.Empty(goodPreviewBody.Issues);

        using var commit = WithTenant(HttpMethod.Post, "/api/party-imports/commit", tenantId);
        commit.Content = JsonContent.Create(goodBody);
        var commitRes = await _client.SendAsync(commit);
        Assert.Equal(HttpStatusCode.OK, commitRes.StatusCode);
        var commitBody = await commitRes.Content.ReadFromJsonAsync<CommitBody>(Json);
        Assert.Equal(2, commitBody!.Committed);

        var codes = await ListPartyCodes(tenantId, "IMP-");
        Assert.Contains("IMP-C1", codes);
        Assert.Contains("IMP-V1", codes);
        Assert.DoesNotContain("IMP-OK", codes);
    }

    [Fact]
    public async Task Commit_VietnameseRoles_WritesBankAndContact()
    {
        var tenantId = await CreateTenantAsync("TN-PARTY-BANK", "Party Bank Import");
        var incomplete = new
        {
            rows = new object[]
            {
                new { code = "IMP-BANK-BAD", name = "Thiếu STK", bankName = "Vietcombank" }
            }
        };
        using var bad = WithTenant(HttpMethod.Post, "/api/party-imports/preview", tenantId);
        bad.Content = JsonContent.Create(incomplete);
        var badBody = await (await _client.SendAsync(bad)).Content.ReadFromJsonAsync<PreviewBody>(Json);
        Assert.False(badBody!.CanCommit);
        Assert.Contains(badBody.Issues, i => i.Field == "bankAccountNumber");

        var good = new
        {
            rows = new object[]
            {
                new
                {
                    code = "KH-MAU",
                    name = "Công ty TNHH ABC, chi nhánh",
                    taxId = "0312345678",
                    countryCode = "VN",
                    roleCodes = new[] { "Khách hàng", "Bên trả tiền" },
                    partyKind = "Tổ chức",
                    addressLine1 = "12 Nguyễn Huệ, Quận 1",
                    city = "Hồ Chí Minh",
                    bankName = "Vietcombank",
                    bankAccountNumber = "0123456789",
                    bankAccountName = "CONG TY TNHH ABC",
                    contactName = "Nguyễn Văn An",
                    contactEmail = "an.nv@abc.example",
                    contactFunction = "Kế toán"
                }
            }
        };
        using var commit = WithTenant(HttpMethod.Post, "/api/party-imports/commit", tenantId);
        commit.Content = JsonContent.Create(good);
        var commitRes = await _client.SendAsync(commit);
        Assert.Equal(HttpStatusCode.OK, commitRes.StatusCode);

        var partyId = (await ListParties(tenantId, "KH-MAU")).Single(p => p.Code == "KH-MAU").Id;
        using var banks = WithTenant(HttpMethod.Get, $"/api/business-parties/{partyId}/bank-accounts", tenantId);
        var bank = Assert.Single((await (await _client.SendAsync(banks)).Content.ReadFromJsonAsync<List<BankBody>>(Json))!);
        Assert.Equal("0123456789", bank.AccountNumber);
        Assert.True(bank.IsDefault);

        using var contacts = WithTenant(HttpMethod.Get, $"/api/business-parties/{partyId}/contacts", tenantId);
        var contact = Assert.Single((await (await _client.SendAsync(contacts)).Content.ReadFromJsonAsync<List<ContactBody>>(Json))!);
        Assert.Equal("Nguyễn Văn An", contact.FullName);
        Assert.Equal("billing", contact.FunctionCode);
        Assert.True(contact.IsPrimary);
    }

    private async Task<List<string>> ListPartyCodes(Guid tenantId, string prefix)
    {
        var items = await ListParties(tenantId, prefix);
        return items.Where(p => p.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Code)
            .ToList();
    }

    private async Task<List<PartyListItem>> ListParties(Guid tenantId, string prefix)
    {
        using var list = WithTenant(HttpMethod.Get, $"/api/business-parties?search={prefix}", tenantId);
        return (await (await _client.SendAsync(list)).Content.ReadFromJsonAsync<List<PartyListItem>>(Json)) ?? [];
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(Json))!.Id;
    }

    private static HttpRequestMessage WithTenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record PreviewBody(bool CanCommit, List<IssueBody> Issues);

    private sealed record IssueBody(int Row, string Field, string Message);

    private sealed record CommitBody(int Committed);

    private sealed record PartyListItem(Guid Id, string Code, string Name);
    private sealed record BankBody(string AccountNumber, bool IsDefault);
    private sealed record ContactBody(string FullName, string? FunctionCode, bool IsPrimary);
}
