using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class AdminSettingsFullTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public AdminSettingsFullTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task CreateUser_WithPassword_CanLogin_AndAuditRecords()
    {
        var tenantId = await CreateTenantAsync("TN-ADM-U", "Admin Users");
        const string email = "ops.admin@example.com";
        const string password = "Passw0rd12";

        using var create = WithTenant(HttpMethod.Post, "/api/users", tenantId);
        create.Content = JsonContent.Create(new
        {
            email,
            displayName = "Điều hành",
            password
        });
        var created = await _client.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = await created.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);

        using var listReq = WithTenant(HttpMethod.Get, "/api/users", tenantId);
        var users = await (await _client.SendAsync(listReq)).Content
            .ReadFromJsonAsync<List<UserResponse>>(JsonOptions);
        var row = Assert.Single(users!, u => u.Id == user!.Id);
        Assert.True(row.PasswordSet);
        Assert.True(row.IsActive);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var auditReq = WithTenant(
            HttpMethod.Get, "/api/audit-events?action=user.create&objectType=user", tenantId);
        var audit = await (await _client.SendAsync(auditReq)).Content
            .ReadFromJsonAsync<List<AuditResponse>>(JsonOptions);
        Assert.Contains(audit!, e => e.ObjectId == user!.Id && e.Action == "user.create");
    }

    [Fact]
    public async Task WeakPassword_IsRejected_AndLastAdminCannotDeactivate()
    {
        var tenantId = await CreateTenantAsync("TN-ADM-P", "Password Policy");

        using var weak = WithTenant(HttpMethod.Post, "/api/users", tenantId);
        weak.Content = JsonContent.Create(new
        {
            email = "weak@example.com",
            displayName = "Weak",
            password = "short"
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(weak)).StatusCode);

        using var create = WithTenant(HttpMethod.Post, "/api/users", tenantId);
        create.Content = JsonContent.Create(new
        {
            email = "last.admin@example.com",
            displayName = "Last Admin",
            password = "Passw0rd12"
        });
        var userId = (await (await _client.SendAsync(create)).Content
            .ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var rolesReq = WithTenant(HttpMethod.Get, "/api/roles", tenantId);
        var roles = await (await _client.SendAsync(rolesReq)).Content
            .ReadFromJsonAsync<List<RoleResponse>>(JsonOptions);
        var admin = Assert.Single(roles!, r => r.Code == "Admin");

        using var assign = WithTenant(
            HttpMethod.Post, $"/api/users/{userId}/roles/{admin.Id}", tenantId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(assign)).StatusCode);

        using var deactivate = WithTenant(HttpMethod.Put, $"/api/users/{userId}", tenantId);
        deactivate.Content = JsonContent.Create(new
        {
            displayName = "Last Admin",
            isActive = false,
            organizationId = (Guid?)null
        });
        var denied = await _client.SendAsync(deactivate);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        var err = await denied.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Contains("Quản trị cuối", err!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SeatLimit_BlocksCreate_WhenFull()
    {
        var tenantId = await CreateTenantAsync("TN-ADM-SEAT", "Seat Cap");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var license = await db.TenantLicenses.FirstAsync(l => l.TenantId == tenantId);
            license.SeatLimit = 1;
            await db.SaveChangesAsync();
        }

        using var first = WithTenant(HttpMethod.Post, "/api/users", tenantId);
        first.Content = JsonContent.Create(new
        {
            email = "one@example.com",
            displayName = "One",
            password = "Passw0rd12"
        });
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(first)).StatusCode);

        using var second = WithTenant(HttpMethod.Post, "/api/users", tenantId);
        second.Content = JsonContent.Create(new
        {
            email = "two@example.com",
            displayName = "Two",
            password = "Passw0rd12"
        });
        var blocked = await _client.SendAsync(second);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await blocked.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
        Assert.Contains("hết chỗ", err!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompanyProfile_UpdatesLegalFields_AndRejectsUnknownTimezone()
    {
        var tenantId = await CreateTenantAsync("TN-ADM-CO", "Company Co");

        using var put = WithTenant(HttpMethod.Put, "/api/tenant-profile", tenantId);
        put.Content = JsonContent.Create(new
        {
            name = "Công ty TNHH Demo",
            legalName = "Công ty TNHH Demo Logistics",
            taxId = "0312345678",
            phone = "0281234567",
            email = "ke.toan@example.com",
            website = "https://example.com",
            addressLine1 = "12 Nguyễn Huệ",
            ward = "Bến Nghé",
            district = "1",
            city = "Hồ Chí Minh",
            province = "Hồ Chí Minh",
            countryCode = "vn",
            postalCode = "700000",
            timeZoneId = "Asia/Ho_Chi_Minh",
            dateFormat = "dd/MM/yyyy",
            defaultCurrencyCode = "VND"
        });
        var ok = await _client.SendAsync(put);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var profile = await ok.Content.ReadFromJsonAsync<ProfileResponse>(JsonOptions);
        Assert.Equal("0312345678", profile!.TaxId);
        Assert.Equal("VN", profile.CountryCode);
        Assert.Equal("Asia/Ho_Chi_Minh", profile.TimeZoneId);

        using var bad = WithTenant(HttpMethod.Put, "/api/tenant-profile", tenantId);
        bad.Content = JsonContent.Create(new
        {
            name = "X",
            timeZoneId = "Mars/Phobos",
            dateFormat = "dd/MM/yyyy",
            defaultCurrencyCode = "VND"
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(bad)).StatusCode);
    }

    [Fact]
    public async Task Catalog_TransportKinds_PersistAttributes_AndRejectBadLocationClass()
    {
        var tenantId = await CreateTenantAsync("TN-ADM-CAT", "Catalog Kinds");

        using var route = WithTenant(HttpMethod.Put, "/api/master-catalog", tenantId);
        route.Content = JsonContent.Create(new
        {
            kind = "transport_route",
            code = "SGN-HAN",
            name = "Tân Sơn Nhất — Nội Bài",
            attributesJson = """{"origin":"SGN","destination":"HAN"}""",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(route)).StatusCode);

        using var mode = WithTenant(HttpMethod.Put, "/api/master-catalog", tenantId);
        mode.Content = JsonContent.Create(new
        {
            kind = "transport_mode",
            code = "AIR",
            name = "Hàng không",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(mode)).StatusCode);

        using var loc = WithTenant(HttpMethod.Put, "/api/master-catalog", tenantId);
        loc.Content = JsonContent.Create(new
        {
            kind = "location",
            code = "VNSGN",
            name = "Cảng hàng không Tân Sơn Nhất",
            attributesJson = """{"class":"airport"}""",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(loc)).StatusCode);

        using var bad = WithTenant(HttpMethod.Put, "/api/master-catalog", tenantId);
        bad.Content = JsonContent.Create(new
        {
            kind = "location",
            code = "XXGPS",
            name = "Điểm GPS",
            attributesJson = """{"class":"gps_pin"}""",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(bad)).StatusCode);

        using var list = WithTenant(HttpMethod.Get, "/api/master-catalog?kind=transport_route", tenantId);
        var rows = await (await _client.SendAsync(list)).Content
            .ReadFromJsonAsync<List<CatalogResponse>>(JsonOptions);
        var item = Assert.Single(rows!, r => r.Code == "SGN-HAN");
        Assert.Contains("SGN", item.AttributesJson);
        Assert.Contains("HAN", item.AttributesJson);
    }

    [Fact]
    public async Task License_CannotEnableModuleOutsidePlan()
    {
        var tenantId = await CreateTenantAsync("TN-ADM-LIC", "License");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var row = await db.TenantLicenseModules.FirstAsync(
                m => m.TenantId == tenantId && m.ModuleCode == TenantModules.Reports);
            row.IncludedInPlan = false;
            row.IsEnabled = false;
            await db.SaveChangesAsync();
        }

        using var enable = WithTenant(
            HttpMethod.Put, "/api/tenant-license/modules/reports", tenantId);
        enable.Content = JsonContent.Create(new { isEnabled = true });
        var denied = await _client.SendAsync(enable);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);

        using var get = WithTenant(HttpMethod.Get, "/api/tenant-license", tenantId);
        var license = await (await _client.SendAsync(get)).Content
            .ReadFromJsonAsync<LicenseResponse>(JsonOptions);
        var reports = Assert.Single(license!.Modules, m => m.Code == "reports");
        Assert.False(reports.IncludedInPlan);
        Assert.False(reports.IsEnabled);
        Assert.True(license.SeatLimit > 0);
    }

    [Fact]
    public async Task BackupRestore_IsTenantIsolated_AndDoesNotTouchMoney()
    {
        var tenantA = await CreateTenantAsync("TN-BK-A", "Backup A");
        var tenantB = await CreateTenantAsync("TN-BK-B", "Backup B");

        using var catalog = WithTenant(HttpMethod.Put, "/api/master-catalog", tenantA);
        catalog.Content = JsonContent.Create(new
        {
            kind = "cost_type",
            code = "FUEL",
            name = "Nhiên liệu gốc",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(catalog)).StatusCode);

        using var billReq = WithTenant(HttpMethod.Post, "/api/bills", tenantA);
        billReq.Content = JsonContent.Create(new { billNo = "BL-KEEP", billType = "freight" });
        var billId = (await (await _client.SendAsync(billReq)).Content
            .ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using (var cost = WithTenant(HttpMethod.Post, "/api/costs", tenantA))
        {
            cost.Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount = 1000m,
                currencyCode = "VND",
                costTypeCode = "FREIGHT"
            });
            (await _client.SendAsync(cost)).EnsureSuccessStatusCode();
        }

        using (var revenue = WithTenant(HttpMethod.Post, "/api/revenues", tenantA))
        {
            revenue.Content = JsonContent.Create(new
            {
                billId,
                amount = 1500m,
                currencyCode = "VND",
                revenueTypeCode = "FREIGHT"
            });
            (await _client.SendAsync(revenue)).EnsureSuccessStatusCode();
        }

        Guid closeId;
        using (var start = WithTenant(HttpMethod.Post, "/api/financial-closes", tenantA))
        {
            start.Content = JsonContent.Create(new
            {
                scopeType = "bill",
                scopeId = billId,
                periodFrom = new DateOnly(2026, 9, 1),
                periodTo = new DateOnly(2026, 9, 30),
                policyVersion = "controlled",
                baseCurrency = "VND"
            });
            var started = await _client.SendAsync(start);
            started.EnsureSuccessStatusCode();
            closeId = (await started.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        Guid snapshotId;
        using (var snap = WithTenant(HttpMethod.Post, $"/api/financial-closes/{closeId}/snapshot", tenantA))
        {
            var snapped = await _client.SendAsync(snap);
            snapped.EnsureSuccessStatusCode();
            snapshotId = (await snapped.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
        }

        var hashBefore = await SnapshotHashAsync(tenantA, snapshotId);

        using var backupReq = WithTenant(HttpMethod.Post, "/api/tenant-backups", tenantA);
        backupReq.Content = JsonContent.Create(new { note = "trước khi sửa danh mục" });
        var backupRes = await _client.SendAsync(backupReq);
        Assert.Equal(HttpStatusCode.Created, backupRes.StatusCode);
        var backupId = (await backupRes.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;

        using var leak = WithTenant(HttpMethod.Get, $"/api/tenant-backups/{backupId}", tenantB);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(leak)).StatusCode);

        using var restoreAsB = WithTenant(
            HttpMethod.Post, $"/api/tenant-backups/{backupId}/restore", tenantB);
        restoreAsB.Content = JsonContent.Create(new { confirmPhrase = "RESTORE TN-BK-B" });
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(restoreAsB)).StatusCode);

        using var rename = WithTenant(HttpMethod.Put, "/api/master-catalog", tenantA);
        rename.Content = JsonContent.Create(new
        {
            kind = "cost_type",
            code = "FUEL",
            name = "Nhiên liệu đã sửa",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(rename)).StatusCode);

        using var badPhrase = WithTenant(
            HttpMethod.Post, $"/api/tenant-backups/{backupId}/restore", tenantA);
        badPhrase.Content = JsonContent.Create(new { confirmPhrase = "YES" });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(badPhrase)).StatusCode);

        using var restore = WithTenant(
            HttpMethod.Post, $"/api/tenant-backups/{backupId}/restore", tenantA);
        restore.Content = JsonContent.Create(new { confirmPhrase = "RESTORE TN-BK-A" });
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(restore)).StatusCode);

        using var listCat = WithTenant(HttpMethod.Get, "/api/master-catalog?kind=cost_type", tenantA);
        var cats = await (await _client.SendAsync(listCat)).Content
            .ReadFromJsonAsync<List<CatalogResponse>>(JsonOptions);
        Assert.Equal("Nhiên liệu gốc", Assert.Single(cats!, c => c.Code == "FUEL").Name);

        using var getBill = WithTenant(HttpMethod.Get, $"/api/bills/{billId}", tenantA);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(getBill)).StatusCode);

        using var close = WithTenant(HttpMethod.Get, $"/api/financial-closes/{closeId}", tenantA);
        var closeBody = await (await _client.SendAsync(close)).Content
            .ReadFromJsonAsync<CloseStatusBody>(JsonOptions);
        Assert.Equal("locked", closeBody!.Status);
        Assert.Equal(hashBefore, await SnapshotHashAsync(tenantA, snapshotId));
    }

    private async Task<string> SnapshotHashAsync(Guid tenantId, Guid snapshotId)
    {
        using var req = WithTenant(HttpMethod.Get, $"/api/financial-close-snapshots/{snapshotId}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SnapshotHashBody>(JsonOptions))!.ImmutableHash;
    }

    [Fact]
    public async Task NotificationSettings_Persist_AndSmtpFlagIsHonest()
    {
        var tenantId = await CreateTenantAsync("TN-ADM-NT", "Notify");
        using var get = WithTenant(HttpMethod.Get, "/api/notifications/settings", tenantId);
        var before = await (await _client.SendAsync(get)).Content
            .ReadFromJsonAsync<NotifyResponse>(JsonOptions);
        Assert.NotNull(before);
        Assert.False(before!.SmtpConfigured);
        Assert.NotEmpty(before.Events);

        var events = before.Events.Select(e => new
        {
            e.Code,
            e.Name,
            InApp = true,
            Email = e.Code == "approval.pending"
        }).ToList();

        using var put = WithTenant(HttpMethod.Put, "/api/notifications/settings", tenantId);
        put.Content = JsonContent.Create(new
        {
            inAppEnabled = true,
            emailEnabled = true,
            events
        });
        var afterRes = await _client.SendAsync(put);
        Assert.Equal(HttpStatusCode.OK, afterRes.StatusCode);
        var after = await afterRes.Content.ReadFromJsonAsync<NotifyResponse>(JsonOptions);
        Assert.True(after!.EmailEnabled);
        Assert.False(after.SmtpConfigured);
        Assert.True(Assert.Single(after.Events, e => e.Code == "approval.pending").Email);
    }

    [Fact]
    public async Task TenantReadiness_NeedsCustomerAndOperator_AndStaysInsideTheTenant()
    {
        var tenantA = await CreateTenantAsync("TN-RDY-A", "Ready A");
        var tenantB = await CreateTenantAsync("TN-RDY-B", "Ready B");

        var before = await ReadinessAsync(tenantA);
        Assert.False(before.ReadyForBill);
        Assert.True(Item(before, "roles").Done);
        Assert.True(Item(before, "vnd").Done);
        Assert.False(Item(before, "customer").Done);
        Assert.False(Item(before, "operator").Done);
        Assert.Contains("Không cần kết nối", before.Note);

        using (var vendor = WithTenant(HttpMethod.Post, "/api/business-parties", tenantA))
        {
            vendor.Content = JsonContent.Create(new
            {
                code = "NCC-1",
                name = "Nhà cung cấp",
                roleCodes = new[] { "vendor" }
            });
            (await _client.SendAsync(vendor)).EnsureSuccessStatusCode();
        }

        Assert.False(Item(await ReadinessAsync(tenantA), "customer").Done);

        using (var customer = WithTenant(HttpMethod.Post, "/api/business-parties", tenantA))
        {
            customer.Content = JsonContent.Create(new
            {
                code = "KH-1",
                name = "Khách hàng",
                roleCodes = new[] { "customer" }
            });
            (await _client.SendAsync(customer)).EnsureSuccessStatusCode();
        }

        Assert.True(Item(await ReadinessAsync(tenantA), "customer").Done);
        Assert.False((await ReadinessAsync(tenantA)).ReadyForBill);

        using (var user = WithTenant(HttpMethod.Post, "/api/users", tenantA))
        {
            user.Content = JsonContent.Create(new
            {
                email = "ready.a@example.com",
                displayName = "Kế toán",
                password = "Passw0rd12"
            });
            (await _client.SendAsync(user)).EnsureSuccessStatusCode();
        }

        var ready = await ReadinessAsync(tenantA);
        Assert.True(ready.ReadyForBill);
        Assert.True(ready.Items.All(i => i.Done));

        var other = await ReadinessAsync(tenantB);
        Assert.False(other.ReadyForBill);
        Assert.False(Item(other, "customer").Done);
        Assert.False(Item(other, "operator").Done);
    }

    private async Task<ReadinessBody> ReadinessAsync(Guid tenantId)
    {
        using var req = WithTenant(HttpMethod.Get, "/api/tenant-profile/readiness", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<ReadinessBody>(JsonOptions))!;
    }

    private static ReadinessItem Item(ReadinessBody body, string code) =>
        Assert.Single(body.Items, i => i.Code == code);

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
    private sealed record CloseStatusBody(string Status);
    private sealed record SnapshotHashBody(string ImmutableHash);
    private sealed record UserResponse(Guid Id, string Email, bool IsActive, bool PasswordSet);
    private sealed record RoleResponse(Guid Id, string Code);
    private sealed record AuditResponse(Guid ObjectId, string Action);
    private sealed record ErrorResponse(string Message);
    private sealed record ProfileResponse(string? TaxId, string? CountryCode, string TimeZoneId);
    private sealed record CatalogResponse(string Code, string Name, string? AttributesJson);
    private sealed record LicenseModule(string Code, bool IncludedInPlan, bool IsEnabled);
    private sealed record LicenseResponse(int SeatLimit, List<LicenseModule> Modules);
    private sealed record NotifyEvent(string Code, string Name, bool InApp, bool Email);
    private sealed record NotifyResponse(bool InAppEnabled, bool EmailEnabled, bool SmtpConfigured, List<NotifyEvent> Events);
    private sealed record ReadinessItem(string Code, string Label, bool Done, string Href);
    private sealed record ReadinessBody(bool ReadyForBill, string Note, List<ReadinessItem> Items);
}
