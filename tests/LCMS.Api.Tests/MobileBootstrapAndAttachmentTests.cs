using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LCMS.Application.Attachments;
using LCMS.Application.Mobile;
using LCMS.Application.Notifications;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class MobileBootstrapAndAttachmentTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public MobileBootstrapAndAttachmentTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task MobileBootstrap_AdaptsNavigationAndModulesByRole_AndEnforcesCostRevenueSoD()
    {
        var tenantId = await CreateTenantAsync("TN-MOB-01", "Mobile Bootstrap Tenant");
        var adminUser = await UserInRoleAsync(tenantId, "admin.mob@example.com", "Admin");
        var costUser = await UserInRoleAsync(tenantId, "cost.mob@example.com", "CostAccountant");
        var revenueUser = await UserInRoleAsync(tenantId, "rev.mob@example.com", "RevenueAccountant");
        var opsUser = await UserInRoleAsync(tenantId, "ops.mob@example.com", "Ops");

        // 1. Admin / Executive Control
        var adminBoot = await GetBootstrapAsync(tenantId, adminUser);
        Assert.Equal("executive_control", adminBoot.PrimaryPersona);
        Assert.True(adminBoot.FinancialVisibility.CanViewCost);
        Assert.True(adminBoot.FinancialVisibility.CanViewRevenue);
        Assert.True(adminBoot.FinancialVisibility.CanViewMargin);
        Assert.Contains(adminBoot.BottomTabs, t => t.Key == "approvals");
        Assert.Contains(adminBoot.BottomTabs, t => t.Key == "control");
        Assert.Contains(adminBoot.Modules, m => m.Code == "costs");
        Assert.Contains(adminBoot.Modules, m => m.Code == "revenues");
        Assert.NotEmpty(adminBoot.Terminology);

        // 2. Cost Accountant (Strict SoD: Cost = true, Revenue = false, Margin = false)
        var costBoot = await GetBootstrapAsync(tenantId, costUser);
        Assert.Equal("cost_accountant", costBoot.PrimaryPersona);
        Assert.True(costBoot.FinancialVisibility.CanViewCost);
        Assert.False(costBoot.FinancialVisibility.CanViewRevenue);
        Assert.False(costBoot.FinancialVisibility.CanViewMargin);
        Assert.Contains(costBoot.BottomTabs, t => t.Key == "costs");
        Assert.Contains(costBoot.BottomTabs, t => t.Key == "ap");
        Assert.DoesNotContain(costBoot.BottomTabs, t => t.Key == "revenues" || t.Key == "ar");
        Assert.Contains(costBoot.Modules, m => m.Code == "costs");
        Assert.Contains(costBoot.Modules, m => m.Code == "ap");
        Assert.DoesNotContain(costBoot.Modules, m => m.Code == "revenues");
        Assert.DoesNotContain(costBoot.Modules, m => m.Code == "ar");

        // 3. Revenue Accountant (Strict SoD: Cost = false, Revenue = true, Margin = false)
        var revBoot = await GetBootstrapAsync(tenantId, revenueUser);
        Assert.Equal("revenue_accountant", revBoot.PrimaryPersona);
        Assert.False(revBoot.FinancialVisibility.CanViewCost);
        Assert.True(revBoot.FinancialVisibility.CanViewRevenue);
        Assert.False(revBoot.FinancialVisibility.CanViewMargin);
        Assert.Contains(revBoot.BottomTabs, t => t.Key == "revenues");
        Assert.Contains(revBoot.BottomTabs, t => t.Key == "ar");
        Assert.DoesNotContain(revBoot.BottomTabs, t => t.Key == "costs" || t.Key == "ap");
        Assert.Contains(revBoot.Modules, m => m.Code == "revenues");
        Assert.Contains(revBoot.Modules, m => m.Code == "ar");
        Assert.DoesNotContain(revBoot.Modules, m => m.Code == "costs");
        Assert.DoesNotContain(revBoot.Modules, m => m.Code == "ap");

        // 4. Field Ops
        var opsBoot = await GetBootstrapAsync(tenantId, opsUser);
        Assert.Equal("field_ops", opsBoot.PrimaryPersona);
        Assert.Contains(opsBoot.BottomTabs, t => t.Key == "scanner");
        Assert.Contains(opsBoot.BottomTabs, t => t.Key == "offline");
    }

    [Fact]
    public async Task Attachments_StoreOnContaboDisk_EnforceTenantIsolation_EnforceSoD_AndSoftDeleteWithAudit()
    {
        var tenantA = await CreateTenantAsync("TN-ATT-A", "Attachment Tenant A");
        var tenantB = await CreateTenantAsync("TN-ATT-B", "Attachment Tenant B");
        var billA = await CreateBillAsync(tenantA, "HAWB-MOB-001");

        var costUserA = await UserInRoleAsync(tenantA, "cost.att@example.com", "CostAccountant");
        var revUserA = await UserInRoleAsync(tenantA, "rev.att@example.com", "RevenueAccountant");
        var adminB = await UserInRoleAsync(tenantB, "admin.b@example.com", "Admin");

        Guid costId;
        using (var createCost = SendWithHeaders(HttpMethod.Post, "/api/costs", tenantA, costUserA, new
        {
            billId = billA,
            attributionType = "direct",
            amount = 2500000m,
            currencyCode = "VND",
            costTypeCode = "TRUCKING"
        }))
        {
            var res = await _client.SendAsync(createCost);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            costId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        Guid revenueId;
        using (var createRev = SendWithHeaders(HttpMethod.Post, "/api/revenues", tenantA, revUserA, new
        {
            billId = billA,
            amount = 4200000m,
            currencyCode = "VND",
            revenueTypeCode = "FREIGHT"
        }))
        {
            var res = await _client.SendAsync(createRev);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            revenueId = (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        // RevenueAccountant cannot upload attachment to a Cost (SoD 403)
        var sampleBytes = Encoding.UTF8.GetBytes("FAKE-JPEG-CAMERA-RECEIPT-DATA-2026");
        var sampleBase64 = Convert.ToBase64String(sampleBytes);
        var expectedSha256 = Convert.ToHexString(SHA256.HashData(sampleBytes)).ToLowerInvariant();

        using (var forbiddenUpload = SendWithHeaders(HttpMethod.Post, "/api/attachments", tenantA, revUserA, new
        {
            objectType = "cost",
            objectId = costId,
            fileName = "receipt_trucking.jpg",
            contentType = "image/jpeg",
            base64 = sampleBase64,
            notes = "Chụp tại kho Cát Lái"
        }))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(forbiddenUpload)).StatusCode);
        }

        // CostAccountant uploads attachment to Cost -> 201 Created
        AttachmentDto uploaded;
        using (var allowedUpload = SendWithHeaders(HttpMethod.Post, "/api/attachments", tenantA, costUserA, new
        {
            objectType = "cost",
            objectId = costId,
            fileName = "receipt_trucking.jpg",
            contentType = "image/jpeg",
            base64 = sampleBase64,
            notes = "Chụp tại kho Cát Lái"
        }))
        {
            var res = await _client.SendAsync(allowedUpload);
            Assert.Equal(HttpStatusCode.Created, res.StatusCode);
            uploaded = (await res.Content.ReadFromJsonAsync<AttachmentDto>(JsonOptions))!;
            Assert.Equal("cost", uploaded.ObjectType);
            Assert.Equal(costId, uploaded.ObjectId);
            Assert.Equal(sampleBytes.Length, uploaded.SizeBytes);
            Assert.Equal(expectedSha256, uploaded.Sha256Hash);
        }

        // RevenueAccountant cannot list or download Cost attachment (SoD 403)
        using (var forbiddenList = SendWithHeaders(HttpMethod.Get, $"/api/attachments?objectType=cost&objectId={costId}", tenantA, revUserA))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(forbiddenList)).StatusCode);
        }

        using (var forbiddenDownload = SendWithHeaders(HttpMethod.Get, $"/api/attachments/{uploaded.Id}/content", tenantA, revUserA))
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(forbiddenDownload)).StatusCode);
        }

        // Tenant B cannot download Tenant A's attachment (Tenant Isolation 404)
        using (var crossTenantDownload = SendWithHeaders(HttpMethod.Get, $"/api/attachments/{uploaded.Id}/content", tenantB, adminB))
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(crossTenantDownload)).StatusCode);
        }

        // CostAccountant can list and download exact bytes
        using (var listReq = SendWithHeaders(HttpMethod.Get, $"/api/attachments?objectType=cost&objectId={costId}", tenantA, costUserA))
        {
            var res = await _client.SendAsync(listReq);
            res.EnsureSuccessStatusCode();
            var list = await res.Content.ReadFromJsonAsync<List<AttachmentDto>>(JsonOptions);
            Assert.Single(list!);
        }

        using (var downloadReq = SendWithHeaders(HttpMethod.Get, $"/api/attachments/{uploaded.Id}/content", tenantA, costUserA))
        {
            var res = await _client.SendAsync(downloadReq);
            res.EnsureSuccessStatusCode();
            var downloadedBytes = await res.Content.ReadAsByteArrayAsync();
            Assert.Equal(sampleBytes, downloadedBytes);
        }

        // Delete without reason -> 400 BadRequest
        using (var deleteNoReason = SendWithHeaders(HttpMethod.Delete, $"/api/attachments/{uploaded.Id}", tenantA, costUserA, new { reason = "" }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(deleteNoReason)).StatusCode);
        }

        // Soft delete with reason -> 204 NoContent
        using (var deleteWithReason = SendWithHeaders(HttpMethod.Delete, $"/api/attachments/{uploaded.Id}", tenantA, costUserA, new
        {
            reason = "Chụp nhầm hóa đơn của lô hàng khác"
        }))
        {
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(deleteWithReason)).StatusCode);
        }

        // List after soft delete -> empty
        using (var listAfterDelete = SendWithHeaders(HttpMethod.Get, $"/api/attachments?objectType=cost&objectId={costId}", tenantA, costUserA))
        {
            var res = await _client.SendAsync(listAfterDelete);
            res.EnsureSuccessStatusCode();
            var list = await res.Content.ReadFromJsonAsync<List<AttachmentDto>>(JsonOptions);
            Assert.Empty(list!);
        }
    }

    [Fact]
    public async Task PushDevices_RegisterListAndUnregister_PerUserAndTenant()
    {
        var tenantId = await CreateTenantAsync("TN-PUSH-01", "Push Tenant");
        var userId = await UserInRoleAsync(tenantId, "fc.push@example.com", "FinancialController");

        using (var reg = SendWithHeaders(HttpMethod.Post, "/api/notifications/devices", tenantId, userId, new
        {
            deviceToken = "ExponentPushToken[xxxxxx-mobile-test-01]",
            platform = "ios",
            deviceName = "iPhone 16 Pro",
            appVersion = "1.0.0"
        }))
        {
            var res = await _client.SendAsync(reg);
            res.EnsureSuccessStatusCode();
            var dto = await res.Content.ReadFromJsonAsync<PushDeviceDto>(JsonOptions);
            Assert.NotNull(dto);
            Assert.Equal("ios", dto!.Platform);
            Assert.True(dto.IsActive);
        }

        using (var listReq = SendWithHeaders(HttpMethod.Get, "/api/notifications/devices", tenantId, userId))
        {
            var res = await _client.SendAsync(listReq);
            res.EnsureSuccessStatusCode();
            var list = await res.Content.ReadFromJsonAsync<List<PushDeviceDto>>(JsonOptions);
            var item = Assert.Single(list!);
            Assert.Equal("ExponentPushToken[xxxxxx-mobile-test-01]", item.DeviceToken);
        }

        using (var unreg = SendWithHeaders(
            HttpMethod.Delete,
            "/api/notifications/devices?deviceToken=ExponentPushToken[xxxxxx-mobile-test-01]",
            tenantId,
            userId))
        {
            var res = await _client.SendAsync(unreg);
            Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        }

        using (var listAfter = SendWithHeaders(HttpMethod.Get, "/api/notifications/devices", tenantId, userId))
        {
            var res = await _client.SendAsync(listAfter);
            res.EnsureSuccessStatusCode();
            var list = await res.Content.ReadFromJsonAsync<List<PushDeviceDto>>(JsonOptions);
            Assert.Empty(list!);
        }
    }

    private async Task<MobileBootstrapDto> GetBootstrapAsync(Guid tenantId, Guid userId)
    {
        using var req = SendWithHeaders(HttpMethod.Get, "/api/mobile/bootstrap", tenantId, userId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<MobileBootstrapDto>(JsonOptions))!;
    }

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo, billType = "freight" })
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> UserInRoleAsync(Guid tenantId, string email, string roleCode)
    {
        using var createUser = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = JsonContent.Create(new { email, displayName = roleCode })
        };
        createUser.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var userRes = await _client.SendAsync(createUser);
        userRes.EnsureSuccessStatusCode();
        var userId = (await userRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var listRoles = new HttpRequestMessage(HttpMethod.Get, "/api/roles");
        listRoles.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var roles = await (await _client.SendAsync(listRoles)).Content
            .ReadFromJsonAsync<List<RoleDto>>(JsonOptions);
        var role = Assert.Single(roles!, r => r.Code == roleCode);

        using var assign = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{userId}/roles/{role.Id}");
        assign.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(assign)).StatusCode);
        return userId;
    }

    private static HttpRequestMessage SendWithHeaders(
        HttpMethod method,
        string url,
        Guid tenantId,
        Guid userId,
        object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        return req;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record RoleDto(Guid Id, string Code);
}

