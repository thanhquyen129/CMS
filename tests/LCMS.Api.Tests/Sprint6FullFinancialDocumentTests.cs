using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Currencies;
using LCMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class Sprint6FullFinancialDocumentTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint6FullFinancialDocumentTests(LcmsApiFactory factory)
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
    public async Task MatchMethods_RequireAccept_LinkOnly_AndOpenAmountsList()
    {
        var tenantId = await CreateTenantAsync("TN-DOC-FULL-M", "Doc Full Methods");
        var billId = await CreateBillAsync(tenantId, "BL-FULL-M", "freight");

        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-FULL-1", 1000m);
        var lineId = await AddLineAsync(tenantId, docId, 1000m, "Cước");

        // Accept-before-match gate
        using (var blocked = new HttpRequestMessage(HttpMethod.Post, "/api/document-matches")
        {
            Content = JsonContent.Create(new { primaryDocumentId = docId, matchMethod = "line_to_cost" })
        })
        {
            blocked.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(blocked);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("chấp nhận", err!.Message, StringComparison.OrdinalIgnoreCase);
        }

        await AcceptDocumentAsync(tenantId, docId);
        var costId = await CreateDirectCostAsync(tenantId, billId, 1000m, "FREIGHT");
        Assert.Single(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));

        var costMatchId = await StartMatchAsync(tenantId, docId, "line_to_cost");
        var detailId = await AddMatchDetailAsync(tenantId, costMatchId, new
        {
            sourceLineId = lineId,
            targetCostId = costId,
            matchedAmount = 400m
        });
        Assert.NotEqual(Guid.Empty, detailId);

        // Method shape: line_to_cost rejects line target
        using (var wrong = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{costMatchId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceLineId = lineId,
                targetLineId = lineId,
                matchedAmount = 10m
            })
        })
        {
            wrong.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(wrong)).StatusCode);
        }

        // Still link-only
        Assert.Single(await ListCostsAsync(tenantId));
        Assert.Empty(await ListRevenuesAsync(tenantId));

        var docB = await ReceiveDocumentAsync(tenantId, billId, "DN-FULL-2", 600m);
        var lineB = await AddLineAsync(tenantId, docB, 600m, "DN");
        await AcceptDocumentAsync(tenantId, docB);
        var lineMatchId = await StartMatchAsync(tenantId, docId, "line_to_line");
        await AddMatchDetailAsync(tenantId, lineMatchId, new
        {
            sourceLineId = lineId,
            targetLineId = lineB,
            matchedAmount = 200m
        });

        var revenueId = await CreateRevenueAsync(tenantId, billId, 500m);
        var revDoc = await ReceiveDocumentAsync(tenantId, billId, "AR-INV-3", 500m, "receivable");
        var revLine = await AddLineAsync(tenantId, revDoc, 500m, "Doanh thu");
        await AcceptDocumentAsync(tenantId, revDoc);
        var revMatchId = await StartMatchAsync(tenantId, revDoc, "line_to_revenue");
        await AddMatchDetailAsync(tenantId, revMatchId, new
        {
            sourceLineId = revLine,
            targetRevenueId = revenueId,
            matchedAmount = 500m
        });
        Assert.Single(await ListRevenuesAsync(tenantId));

        using var openReq = new HttpRequestMessage(HttpMethod.Get, "/api/financial-documents/open-amounts");
        openReq.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var openRes = await _client.SendAsync(openReq);
        openRes.EnsureSuccessStatusCode();
        var open = await openRes.Content.ReadFromJsonAsync<List<OpenAmountItem>>(JsonOptions);
        Assert.NotNull(open);
        Assert.Contains(open!, o => o.DocumentId == docId && o.OpenAmount == 400m);
        Assert.Contains(open!, o => o.DocumentId == docB && o.OpenAmount == 400m);
        Assert.DoesNotContain(open!, o => o.DocumentId == revDoc);
    }

    [Fact]
    public async Task TolerancePolicy_AllowsWithinPolicy_RejectsBeyond_AndReverseRestoresOpen()
    {
        await using var tolFactory = new ToleranceApiFactory();
        await tolFactory.InitializeDatabaseAsync();
        using var client = tolFactory.CreateClient();

        var tenantId = await CreateTenantAsync(client, "TN-DOC-TOL", "Doc Tolerance");
        var billId = await CreateBillAsync(client, tenantId, "BL-TOL", "freight");
        var docA = await ReceiveDocumentAsync(client, tenantId, billId, "DN-TOL", 1000m);
        var docB = await ReceiveDocumentAsync(client, tenantId, billId, "INV-TOL", 1000m);
        var lineA = await AddLineAsync(client, tenantId, docA, 1000m, "A");
        var lineB = await AddLineAsync(client, tenantId, docB, 1000m, "B");
        await AcceptDocumentAsync(client, tenantId, docA);
        await AcceptDocumentAsync(client, tenantId, docB);

        var matchId = await StartMatchAsync(client, tenantId, docA, "line_to_line");

        // 1000 + absolute 10 from config → first 1000 OK, +5 within tolerance OK, +6 more would exceed
        await AddMatchDetailAsync(client, tenantId, matchId, new
        {
            sourceLineId = lineA,
            targetLineId = lineB,
            matchedAmount = 1000m
        });

        var withinId = await AddMatchDetailAsync(client, tenantId, matchId, new
        {
            sourceLineId = lineA,
            targetLineId = lineB,
            matchedAmount = 5m
        });

        using (var over = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/details")
        {
            Content = JsonContent.Create(new
            {
                sourceLineId = lineA,
                targetLineId = lineB,
                matchedAmount = 6m
            })
        })
        {
            over.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await client.SendAsync(over);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("C-007", err!.Message, StringComparison.OrdinalIgnoreCase);
        }

        using (var reverse = new HttpRequestMessage(
                   HttpMethod.Post,
                   $"/api/document-matches/{matchId}/details/{withinId}/reverse")
               {
                   Content = JsonContent.Create(new { reason = "Sai số dung sai thử nghiệm" })
               })
        {
            reverse.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(reverse)).StatusCode);
        }

        var match = await GetMatchAsync(client, tenantId, matchId);
        Assert.Contains(match.Details, d => d.Id == withinId && d.DetailStatus == "reversed");

        var doc = await GetDocumentAsync(client, tenantId, docA);
        Assert.Equal(1000m, doc.Lines[0].MatchedAmount);
        Assert.Equal(0m, doc.Lines[0].OpenAmount);

        using (var cancelMatch = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/cancel")
               {
                   Content = JsonContent.Create(new { reason = "Còn chi tiết active" })
               })
        {
            cancelMatch.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(cancelMatch)).StatusCode);
        }

        // Reverse remaining active detail then cancel session
        var active = match.Details.First(d => d.DetailStatus == "active");
        using (var reverse2 = new HttpRequestMessage(
                   HttpMethod.Post,
                   $"/api/document-matches/{matchId}/details/{active.Id}/reverse")
               {
                   Content = JsonContent.Create(new { reason = "Đảo toàn bộ trước hủy phiên" })
               })
        {
            reverse2.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(reverse2)).StatusCode);
        }

        using (var cancelOk = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/cancel")
               {
                   Content = JsonContent.Create(new { reason = "Hủy phiên nháp" })
               })
        {
            cancelOk.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(cancelOk)).StatusCode);
        }

        var afterCancel = await GetMatchAsync(client, tenantId, matchId);
        Assert.Equal("cancelled", afterCancel.MatchStatus);

        var restored = await GetDocumentAsync(client, tenantId, docA);
        Assert.Equal(0m, restored.Lines[0].MatchedAmount);
        Assert.Equal("unmatched", restored.MatchingStatus);
    }

    [Fact]
    public async Task DuplicateDocument_IsRejected_AndCancelBlocksActiveMatch()
    {
        var tenantId = await CreateTenantAsync("TN-DOC-DUP", "Doc Dup");
        var billId = await CreateBillAsync(tenantId, "BL-DUP", "freight");

        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-DUP-1", 100m);
        using (var dup = new HttpRequestMessage(HttpMethod.Post, "/api/financial-documents")
               {
                   Content = JsonContent.Create(new
                   {
                       documentType = "invoice",
                       documentNo = "INV-DUP-1",
                       direction = "payable",
                       totalAmount = 100m,
                       currencyCode = "VND",
                       billId
                   })
               })
        {
            dup.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var res = await _client.SendAsync(dup);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            var err = await res.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("trùng", err!.Message, StringComparison.OrdinalIgnoreCase);
        }

        var lineId = await AddLineAsync(tenantId, docId, 100m, "line");
        await AcceptDocumentAsync(tenantId, docId);
        var costId = await CreateDirectCostAsync(tenantId, billId, 100m, "FEE");
        var matchId = await StartMatchAsync(tenantId, docId, "line_to_cost");
        await AddMatchDetailAsync(tenantId, matchId, new
        {
            sourceLineId = lineId,
            targetCostId = costId,
            matchedAmount = 100m
        });

        using (var cancelDoc = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{docId}/cancel")
               {
                   Content = JsonContent.Create(new { reason = "Còn khớp active" })
               })
        {
            cancelDoc.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(cancelDoc)).StatusCode);
        }

        var match = await GetMatchAsync(_client, tenantId, matchId);
        var detailId = match.Details[0].Id;
        using (var reverse = new HttpRequestMessage(
                   HttpMethod.Post,
                   $"/api/document-matches/{matchId}/details/{detailId}/reverse")
               {
                   Content = JsonContent.Create(new { reason = "Đảo để hủy chứng từ" })
               })
        {
            reverse.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(reverse)).StatusCode);
        }

        using (var cancelOk = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{docId}/cancel")
               {
                   Content = JsonContent.Create(new { reason = "Hủy chứng từ sai", Void = false })
               })
        {
            cancelOk.Headers.Add("X-Tenant-Id", tenantId.ToString());
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(cancelOk)).StatusCode);
        }

        var cancelled = await GetDocumentAsync(_client, tenantId, docId);
        Assert.Equal("cancelled", cancelled.RecordStatus);
        Assert.Equal("received", cancelled.ReceiptStatus); // AC-005 independent

        // After cancel, new receive with same key is allowed (prior not active)
        var again = await ReceiveDocumentAsync(tenantId, billId, "INV-DUP-1", 100m);
        Assert.NotEqual(docId, again);
    }

    private sealed class ToleranceApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"lcms-tol-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Database:MigrateOnStartup", "false");
            builder.UseSetting("Demo:SeedOnStartup", "false");
            builder.UseSetting("Auth:RequireJwt", "false");
            builder.UseSetting("Auth:AllowHeaderBootstrap", "true");
            builder.UseSetting("Auth:Jwt:SigningKey", LCMS.Api.Auth.AuthServiceCollectionExtensions.DevFallbackSigningKey);
            builder.UseSetting("Documents:DefaultToleranceAbsolute", "10");
            builder.UseSetting("Documents:DefaultTolerancePercent", "0");
            builder.UseSetting("Documents:EnforceDuplicateControl", "true");
            builder.UseSetting("Documents:RequireAcceptBeforeMatch", "true");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<LcmsDbContext>>();
                services.RemoveAll<LcmsDbContext>();
                services.RemoveAll<ILcmsDbContext>();

                services.AddDbContext<LcmsDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={_dbPath}");
                    options.UseSnakeCaseNamingConvention();
                });
                services.AddScoped<ILcmsDbContext>(sp => sp.GetRequiredService<LcmsDbContext>());
            });
        }

        public async Task InitializeDatabaseAsync()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            await CurrencyCatalogSeeder.EnsureBaselineAsync(db);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                try
                {
                    if (File.Exists(_dbPath))
                    {
                        File.Delete(_dbPath);
                    }
                }
                catch
                {
                    // best-effort
                }
            }
        }
    }

    private Task<Guid> CreateTenantAsync(string code, string name) => CreateTenantAsync(_client, code, name);

    private static async Task<Guid> CreateTenantAsync(HttpClient client, string code, string name)
    {
        var response = await client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private Task<Guid> CreateBillAsync(Guid tenantId, string billNo, string billType) =>
        CreateBillAsync(_client, tenantId, billNo, billType);

    private static async Task<Guid> CreateBillAsync(HttpClient client, Guid tenantId, string billNo, string billType)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo, billType })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private Task AcceptDocumentAsync(Guid tenantId, Guid documentId) =>
        AcceptDocumentAsync(_client, tenantId, documentId);

    private static async Task AcceptDocumentAsync(HttpClient client, Guid tenantId, Guid documentId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{documentId}/accept");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        (await client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private Task<Guid> ReceiveDocumentAsync(
        Guid tenantId,
        Guid billId,
        string documentNo,
        decimal total,
        string direction = "payable") =>
        ReceiveDocumentAsync(_client, tenantId, billId, documentNo, total, direction);

    private static async Task<Guid> ReceiveDocumentAsync(
        HttpClient client,
        Guid tenantId,
        Guid billId,
        string documentNo,
        decimal total,
        string direction = "payable")
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/financial-documents")
        {
            Content = JsonContent.Create(new
            {
                documentType = "invoice",
                documentNo,
                direction,
                totalAmount = total,
                currencyCode = "VND",
                billId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private Task<Guid> AddLineAsync(Guid tenantId, Guid documentId, decimal amount, string description) =>
        AddLineAsync(_client, tenantId, documentId, amount, description);

    private static async Task<Guid> AddLineAsync(
        HttpClient client,
        Guid tenantId,
        Guid documentId,
        decimal amount,
        string description)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{documentId}/lines")
        {
            Content = JsonContent.Create(new { amount, description })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private Task<Guid> StartMatchAsync(Guid tenantId, Guid primaryDocumentId, string matchMethod) =>
        StartMatchAsync(_client, tenantId, primaryDocumentId, matchMethod);

    private static async Task<Guid> StartMatchAsync(
        HttpClient client,
        Guid tenantId,
        Guid primaryDocumentId,
        string matchMethod)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/document-matches")
        {
            Content = JsonContent.Create(new { primaryDocumentId, matchMethod })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private Task<Guid> AddMatchDetailAsync(Guid tenantId, Guid matchId, object body) =>
        AddMatchDetailAsync(_client, tenantId, matchId, body);

    private static async Task<Guid> AddMatchDetailAsync(HttpClient client, Guid tenantId, Guid matchId, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/details")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateDirectCostAsync(Guid tenantId, Guid billId, decimal amount, string costTypeCode)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/costs")
        {
            Content = JsonContent.Create(new
            {
                billId,
                attributionType = "direct",
                amount,
                currencyCode = "VND",
                costTypeCode
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateRevenueAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/revenues")
        {
            Content = JsonContent.Create(new
            {
                billId,
                amount,
                currencyCode = "VND",
                revenueTypeCode = "FREIGHT",
                sourceType = "manual"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private Task<DocumentResponse> GetDocumentAsync(Guid tenantId, Guid documentId) =>
        GetDocumentAsync(_client, tenantId, documentId);

    private static async Task<DocumentResponse> GetDocumentAsync(HttpClient client, Guid tenantId, Guid documentId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-documents/{documentId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<DocumentResponse>(JsonOptions))!;
    }

    private static async Task<MatchResponse> GetMatchAsync(HttpClient client, Guid tenantId, Guid matchId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/document-matches/{matchId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<MatchResponse>(JsonOptions))!;
    }

    private async Task<List<object>> ListCostsAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/costs");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<object>>(JsonOptions))!;
    }

    private async Task<List<object>> ListRevenuesAsync(Guid tenantId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/revenues");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<object>>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record OpenAmountItem(
        Guid DocumentId,
        string DocumentNo,
        Guid LineId,
        decimal Amount,
        decimal MatchedAmount,
        decimal OpenAmount);

    private sealed record DocumentLineResponse(
        Guid Id,
        int LineNo,
        decimal Amount,
        decimal MatchedAmount,
        decimal OpenAmount);

    private sealed record DocumentResponse(
        Guid Id,
        string ReceiptStatus,
        string AcceptanceStatus,
        string MatchingStatus,
        string RecordStatus,
        List<DocumentLineResponse> Lines);

    private sealed record MatchDetailResponse(
        Guid Id,
        decimal MatchedAmount,
        string DetailStatus);

    private sealed record MatchResponse(
        Guid Id,
        string MatchMethod,
        string MatchStatus,
        decimal ToleranceAmount,
        List<MatchDetailResponse> Details);

    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);
}
