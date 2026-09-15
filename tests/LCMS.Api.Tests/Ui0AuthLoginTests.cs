using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Api.Auth;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

public sealed class Ui0AuthLoginTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Login_WithValidPassword_ReturnsJwt_WithTenantAndSub()
    {
        await using var factory = new LcmsApiFactory();
        await factory.InitializeDatabaseAsync();

        var tenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        const string email = "ops@example.com";
        const string password = "CorrectHorse1!";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Code = "OPS",
                Name = "Ops",
                IsActive = true
            });
            var user = new User
            {
                Id = userId,
                TenantId = tenantId,
                Email = email,
                DisplayName = "Operator",
                IsActive = true
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            await TenantAccessSeeder.SeedAdminRoleAsync(db, tenantId, CancellationToken.None);
            await AssignAdminRoleAsync(db, tenantId, userId);
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, raw);

        var body = JsonSerializer.Deserialize<LoginOk>(raw, JsonOptions);
        Assert.False(string.IsNullOrWhiteSpace(body?.AccessToken));
        Assert.Equal("Bearer", body!.TokenType);
        Assert.True(body.ExpiresIn > 0);
        Assert.Equal(userId, body.User.Id);
        Assert.Equal(tenantId, body.User.TenantId);

        using var billReq = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo = "LOGIN-1", billType = "freight" })
        };
        billReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        var billRes = await client.SendAsync(billReq);
        Assert.Equal(HttpStatusCode.Created, billRes.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401_Vietnamese()
    {
        await using var factory = new LcmsApiFactory();
        await factory.InitializeDatabaseAsync();

        var tenantId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
            db.Tenants.Add(new Tenant { Id = tenantId, Code = "X", Name = "X", IsActive = true });
            var user = new User
            {
                TenantId = tenantId,
                Email = "user@example.com",
                DisplayName = "U",
                IsActive = true
            };
            user.PasswordHash = hasher.HashPassword(user, "RightPassword1!");
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "user@example.com",
            password = "WrongPassword1!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var err = await response.Content.ReadFromJsonAsync<ErrorBody>(JsonOptions);
        Assert.Equal("invalid_credentials", err!.Code);
        Assert.Contains("mật khẩu", err.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_Login_IsAnonymous_ThenJwtWorks()
    {
        await using var factory = new ProductionLcmsApiFactory();
        await factory.InitializeDatabaseAsync();

        var tenantId = Guid.NewGuid();
        const string email = "prod.login@example.com";
        const string password = "ProdLoginPass1!";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();
            db.Tenants.Add(new Tenant { Id = tenantId, Code = "PLOGIN", Name = "P", IsActive = true });
            var user = new User
            {
                TenantId = tenantId,
                Email = email,
                DisplayName = "Prod",
                IsActive = true
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            await TenantAccessSeeder.SeedAdminRoleAsync(db, tenantId, CancellationToken.None);
            await AssignAdminRoleAsync(db, tenantId, user.Id);
        }

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var raw = await login.Content.ReadAsStringAsync();
        Assert.True(login.StatusCode == HttpStatusCode.OK, raw);
        var body = JsonSerializer.Deserialize<LoginOk>(raw, JsonOptions);

        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/terminology");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(me)).StatusCode);

        using var usersReq = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        usersReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body!.AccessToken);
        var usersRes = await client.SendAsync(usersReq);
        Assert.Equal(HttpStatusCode.OK, usersRes.StatusCode);
    }

    private static async Task AssignAdminRoleAsync(LcmsDbContext db, Guid tenantId, Guid userId)
    {
        var adminRole = await db.Roles.FirstAsync(
            r => r.TenantId == tenantId && r.Code == TenantAccessSeeder.AdminRoleCode);
        db.UserRoles.Add(new UserRole
        {
            TenantId = tenantId,
            UserId = userId,
            RoleId = adminRole.Id
        });
        await db.SaveChangesAsync();
    }

    private sealed record LoginOk(
        string AccessToken,
        string TokenType,
        int ExpiresIn,
        LoginUser User);

    private sealed record LoginUser(Guid Id, string Email, string DisplayName, Guid TenantId);
    private sealed record ErrorBody(string Code, string Message);
}
