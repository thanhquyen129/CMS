using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Application.FinancialDocuments.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class FinancialDocumentSourceMatchingTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public FinancialDocumentSourceMatchingTests(LcmsApiFactory factory)
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
    public async Task ModeB_AutoCalculatesTotalAmount_AndCreatesConfirmedMatch_SingleEconomicFact()
    {
        // AC-FD-001 & AC-FD-011: AR Mode B calculates total from selected revenues and does not duplicate revenue
        var tenantId = await CreateTenantAsync("TN-MODE-B-AR", "Mode B AR");
        var billId = await CreateBillAsync(tenantId, "BL-MB-01");
        var partyId = await CreatePartyAsync(tenantId, "CUST-01", "Customer 01", isCustomer: true);

        var rev1 = await CreateRevenueAsync(tenantId, billId, 1_000_000m, "VND", partyId);
        var rev2 = await CreateRevenueAsync(tenantId, billId, 500_000m, "VND", partyId);

        var initialRevenues = await ListRevenuesAsync(tenantId);
        Assert.Equal(2, initialRevenues.Count);

        // Call eligible-source-lines query
        using var eligibleReq = Tenant(HttpMethod.Get, $"/api/financial-documents/eligible-source-lines?direction=receivable&billId={billId}", tenantId);
        var eligibleRes = await _client.SendAsync(eligibleReq);
        Assert.Equal(HttpStatusCode.OK, eligibleRes.StatusCode);
        var eligibleLines = await eligibleRes.Content.ReadFromJsonAsync<List<EligibleSourceLineDto>>(JsonOptions);
        Assert.NotNull(eligibleLines);
        Assert.Equal(2, eligibleLines.Count);

        // Receive Document in Mode B
        using var receiveReq = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        receiveReq.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-MB-001",
            direction = "receivable",
            totalAmount = 0m, // In Mode B, server calculates 1,500,000m
            currencyCode = "VND",
            documentDate = "2026-10-03",
            counterpartyId = partyId,
            billId,
            mode = "lcms_generated",
            selectedSourceLines = new[]
            {
                new { sourceId = rev1, sourceType = "revenue", amount = 1_000_000m, billId },
                new { sourceId = rev2, sourceType = "revenue", amount = 500_000m, billId }
            }
        });

        var receiveRes = await _client.SendAsync(receiveReq);
        Assert.Equal(HttpStatusCode.Created, receiveRes.StatusCode);
        var docBody = await receiveRes.Content.ReadFromJsonAsync<IdBody>(JsonOptions);
        Assert.NotNull(docBody);

        // Check document details
        var doc = await GetDocumentAsync(tenantId, docBody.Id);
        Assert.Equal(1_500_000m, doc.TotalAmount);
        Assert.Equal("lcms_generated", doc.Mode);
        Assert.Equal("not_accepted", doc.AcceptanceStatus); // Receipt != Accept != Match (ADR-0012)
        Assert.Equal("matched", doc.MatchingStatus);        // Mode B auto-matches
        Assert.Equal(2, doc.Lines.Count);

        // AC-FD-011: Revenue count must stay 2 (Single economic fact)
        var afterRevenues = await ListRevenuesAsync(tenantId);
        Assert.Equal(2, afterRevenues.Count);

        // Verify remaining eligible amount is now 0
        using var recheckReq = Tenant(HttpMethod.Get, $"/api/financial-documents/eligible-source-lines?direction=receivable&billId={billId}", tenantId);
        var recheckRes = await _client.SendAsync(recheckReq);
        var recheckLines = await recheckRes.Content.ReadFromJsonAsync<List<EligibleSourceLineDto>>(JsonOptions);
        Assert.NotNull(recheckLines);
        Assert.All(recheckLines, l => Assert.Equal(0m, l.RemainingEligibleAmount));
    }

    [Fact]
    public async Task ModeA_PartialMatch_TracksRemainingAndVariance()
    {
        // AC-FD-003, AC-FD-005, AC-FD-006: Mode A external invoice with partial line matching
        var tenantId = await CreateTenantAsync("TN-MODE-A-AP", "Mode A AP");
        var billId = await CreateBillAsync(tenantId, "BL-MA-01");
        var vendorId = await CreatePartyAsync(tenantId, "VEND-01", "Vendor 01", isVendor: true);

        var costId = await CreateCostAsync(tenantId, billId, 1_000_000m, "VND", vendorId);

        // Step 1: External invoice 1 arrives for 600,000 VND, matching 600,000 VND of the 1,000,000 cost line
        using var receiveReq1 = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        receiveReq1.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-EXT-001",
            direction = "payable",
            totalAmount = 600_000m,
            currencyCode = "VND",
            documentDate = "2026-10-03",
            counterpartyId = vendorId,
            billId,
            mode = "external_received",
            selectedSourceLines = new[]
            {
                new { sourceId = costId, sourceType = "cost", amount = 600_000m, billId }
            }
        });

        var receiveRes1 = await _client.SendAsync(receiveReq1);
        Assert.Equal(HttpStatusCode.Created, receiveRes1.StatusCode);

        // Check remaining eligible amount: should be 400,000
        using var check1Req = Tenant(HttpMethod.Get, $"/api/financial-documents/eligible-source-lines?direction=payable&billId={billId}", tenantId);
        var check1Res = await _client.SendAsync(check1Req);
        var check1Lines = await check1Res.Content.ReadFromJsonAsync<List<EligibleSourceLineDto>>(JsonOptions);
        Assert.NotNull(check1Lines);
        var costLine = Assert.Single(check1Lines);
        Assert.Equal(400_000m, costLine.RemainingEligibleAmount);
        Assert.Equal(600_000m, costLine.AlreadyDocumentedAmount);

        // Step 2: External invoice 2 arrives for 500,000 VND, attempting to match 500,000 VND -> AC-FD-007 Over-match rejected
        using var overMatchReq = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        overMatchReq.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-EXT-OVER",
            direction = "payable",
            totalAmount = 500_000m,
            currencyCode = "VND",
            documentDate = "2026-10-03",
            counterpartyId = vendorId,
            billId,
            mode = "external_received",
            selectedSourceLines = new[]
            {
                new { sourceId = costId, sourceType = "cost", amount = 500_000m, billId }
            }
        });
        var overMatchRes = await _client.SendAsync(overMatchReq);
        Assert.Equal(HttpStatusCode.BadRequest, overMatchRes.StatusCode);

        // Step 3: Match the remaining 400,000 VND on invoice 2 (which has a header total of 450,000 -> variance 50,000)
        using var receiveReq2 = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        receiveReq2.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-EXT-002",
            direction = "payable",
            totalAmount = 450_000m, // Mode A allows variance
            currencyCode = "VND",
            documentDate = "2026-10-03",
            counterpartyId = vendorId,
            billId,
            mode = "external_received",
            selectedSourceLines = new[]
            {
                new { sourceId = costId, sourceType = "cost", amount = 400_000m, billId }
            }
        });

        var receiveRes2 = await _client.SendAsync(receiveReq2);
        Assert.Equal(HttpStatusCode.Created, receiveRes2.StatusCode);

        // Cost is now fully documented (remaining = 0, so eligible-source-lines returns empty)
        using var check2Req = Tenant(HttpMethod.Get, $"/api/financial-documents/eligible-source-lines?direction=payable&billId={billId}", tenantId);
        var check2Res = await _client.SendAsync(check2Req);
        var check2Lines = await check2Res.Content.ReadFromJsonAsync<List<EligibleSourceLineDto>>(JsonOptions);
        Assert.NotNull(check2Lines);
        Assert.Empty(check2Lines);
    }

    [Fact]
    public async Task DirectionAndCounterpartyIsolation_Enforced()
    {
        // AC-FD-009, AC-FD-010, AC-FD-019
        var tenantId = await CreateTenantAsync("TN-ISOLATION", "Isolation Test");
        var billId = await CreateBillAsync(tenantId, "BL-ISO-01");
        var partyA = await CreatePartyAsync(tenantId, "PTY-A", "Party A", isCustomer: true, isVendor: true);
        var partyB = await CreatePartyAsync(tenantId, "PTY-B", "Party B", isCustomer: true, isVendor: true);

        var costA = await CreateCostAsync(tenantId, billId, 500_000m, "VND", partyA);
        var revA = await CreateRevenueAsync(tenantId, billId, 700_000m, "VND", partyA);

        // 1. Eligible query for payable direction returns cost only, never revenue
        using var apReq = Tenant(HttpMethod.Get, $"/api/financial-documents/eligible-source-lines?direction=payable&billId={billId}", tenantId);
        var apRes = await _client.SendAsync(apReq);
        var apLines = await apRes.Content.ReadFromJsonAsync<List<EligibleSourceLineDto>>(JsonOptions);
        Assert.NotNull(apLines);
        Assert.All(apLines, l => Assert.Equal("cost", l.SourceType));

        // 2. Eligible query for receivable direction returns revenue only, never cost
        using var arReq = Tenant(HttpMethod.Get, $"/api/financial-documents/eligible-source-lines?direction=receivable&billId={billId}", tenantId);
        var arRes = await _client.SendAsync(arReq);
        var arLines = await arRes.Content.ReadFromJsonAsync<List<EligibleSourceLineDto>>(JsonOptions);
        Assert.NotNull(arLines);
        Assert.All(arLines, l => Assert.Equal("revenue", l.SourceType));

        // 3. Counterparty mismatch rejection (AC-FD-019): Document for Party B cannot select source line owned by Party A
        using var mismatchPartyReq = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        mismatchPartyReq.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-MISMATCH-PARTY",
            direction = "payable",
            totalAmount = 500_000m,
            currencyCode = "VND",
            documentDate = "2026-10-03",
            counterpartyId = partyB, // Mismatch with partyA who owns costA
            billId,
            mode = "lcms_generated",
            selectedSourceLines = new[]
            {
                new { sourceId = costA, sourceType = "cost", amount = 500_000m, billId }
            }
        });
        var mismatchPartyRes = await _client.SendAsync(mismatchPartyReq);
        Assert.Equal(HttpStatusCode.BadRequest, mismatchPartyRes.StatusCode);
    }

    [Fact]
    public async Task CancelMatch_RestoresRemainingEligibleAmount()
    {
        // AC-FD-018: Cancelling match frees up the source line
        var tenantId = await CreateTenantAsync("TN-RESTORE", "Restore Test");
        var billId = await CreateBillAsync(tenantId, "BL-RES-01");
        var vendorId = await CreatePartyAsync(tenantId, "VEND-RES", "Vendor Res", isVendor: true);

        var costId = await CreateCostAsync(tenantId, billId, 800_000m, "VND", vendorId);

        // Receive document in Mode B (which creates confirmed match)
        using var receiveReq = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        receiveReq.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-RES-01",
            direction = "payable",
            totalAmount = 800_000m,
            currencyCode = "VND",
            documentDate = "2026-10-03",
            counterpartyId = vendorId,
            billId,
            mode = "lcms_generated",
            selectedSourceLines = new[]
            {
                new { sourceId = costId, sourceType = "cost", amount = 800_000m, billId }
            }
        });
        var receiveRes = await _client.SendAsync(receiveReq);
        Assert.Equal(HttpStatusCode.Created, receiveRes.StatusCode);

        // Check remaining is 0 (so line is not eligible anymore)
        using var check1Req = Tenant(HttpMethod.Get, $"/api/financial-documents/eligible-source-lines?direction=payable&billId={billId}", tenantId);
        var check1Res = await _client.SendAsync(check1Req);
        var check1Lines = await check1Res.Content.ReadFromJsonAsync<List<EligibleSourceLineDto>>(JsonOptions);
        Assert.NotNull(check1Lines);
        Assert.Empty(check1Lines);

        // Reverse the match detail to simulate cancellation
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LCMS.Infrastructure.Persistence.LcmsDbContext>();
            var detail = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(db.DocumentMatchDetails, d => d.TargetCostId == costId);
            detail.DetailStatus = "reversed";
            detail.ReversedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        // Verify remaining eligible amount is restored back to 800,000!
        using var check2Req = Tenant(HttpMethod.Get, $"/api/financial-documents/eligible-source-lines?direction=payable&billId={billId}", tenantId);
        var check2Res = await _client.SendAsync(check2Req);
        var check2Lines = await check2Res.Content.ReadFromJsonAsync<List<EligibleSourceLineDto>>(JsonOptions);
        Assert.NotNull(check2Lines);
        var restored = Assert.Single(check2Lines);
        Assert.Equal(800_000m, restored.RemainingEligibleAmount);
    }

    [Fact]
    public async Task BillReference_DoesNotAutoTakeAllLines_AC_FD_004()
    {
        // AC-FD-004: Bill reference does not auto-take all lines
        var tenantId = await CreateTenantAsync("TN-BILL-REF", "Bill Ref Test");
        var billId = await CreateBillAsync(tenantId, "BL-REF-01");
        var vendorId = await CreatePartyAsync(tenantId, "VEND-REF", "Vendor Ref", isVendor: true);

        var cost1 = await CreateCostAsync(tenantId, billId, 500_000m, "VND", vendorId);
        var cost2 = await CreateCostAsync(tenantId, billId, 300_000m, "VND", vendorId);

        // Receive document referencing billId but without selecting source lines
        using var receiveReq = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        receiveReq.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-NO-SEL",
            direction = "payable",
            totalAmount = 200_000m,
            currencyCode = "VND",
            documentDate = "2026-10-03",
            counterpartyId = vendorId,
            billId,
            mode = "external_received"
            // selectedSourceLines omitted / empty
        });
        var receiveRes = await _client.SendAsync(receiveReq);
        Assert.Equal(HttpStatusCode.Created, receiveRes.StatusCode);
        var docBody = await receiveRes.Content.ReadFromJsonAsync<IdBody>(JsonOptions);

        var doc = await GetDocumentAsync(tenantId, docBody!.Id);
        Assert.Equal(200_000m, doc.TotalAmount);
        Assert.Empty(doc.Lines); // No lines auto-created

        // Source costs remain completely untouched with full remaining amounts
        using var eligibleReq = Tenant(HttpMethod.Get, $"/api/financial-documents/eligible-source-lines?direction=payable&billId={billId}", tenantId);
        var eligibleRes = await _client.SendAsync(eligibleReq);
        var lines = await eligibleRes.Content.ReadFromJsonAsync<List<EligibleSourceLineDto>>(JsonOptions);
        Assert.NotNull(lines);
        Assert.Equal(2, lines.Count);
        Assert.Equal(500_000m, lines.First(l => l.SourceId == cost1).RemainingEligibleAmount);
        Assert.Equal(300_000m, lines.First(l => l.SourceId == cost2).RemainingEligibleAmount);
    }

    [Fact]
    public async Task MultiBill_Many_To_Many_Matching_AC_FD_008()
    {
        // AC-FD-008: Consolidated invoice matching source lines across multiple bills
        var tenantId = await CreateTenantAsync("TN-MULTI-BILL", "Multi Bill Test");
        var bill1 = await CreateBillAsync(tenantId, "BL-MULTI-1");
        var bill2 = await CreateBillAsync(tenantId, "BL-MULTI-2");
        var vendorId = await CreatePartyAsync(tenantId, "VEND-MULTI", "Vendor Multi", isVendor: true);

        var cost1 = await CreateCostAsync(tenantId, bill1, 400_000m, "VND", vendorId);
        var cost2 = await CreateCostAsync(tenantId, bill2, 250_000m, "VND", vendorId);

        using var receiveReq = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        receiveReq.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-CONSOLIDATED",
            direction = "payable",
            totalAmount = 0m,
            currencyCode = "VND",
            documentDate = "2026-10-03",
            counterpartyId = vendorId,
            mode = "lcms_generated",
            selectedSourceLines = new[]
            {
                new { sourceId = cost1, sourceType = "cost", amount = 400_000m, billId = (Guid?)bill1 },
                new { sourceId = cost2, sourceType = "cost", amount = 250_000m, billId = (Guid?)bill2 }
            }
        });

        var receiveRes = await _client.SendAsync(receiveReq);
        Assert.Equal(HttpStatusCode.Created, receiveRes.StatusCode);
        var docBody = await receiveRes.Content.ReadFromJsonAsync<IdBody>(JsonOptions);

        var doc = await GetDocumentAsync(tenantId, docBody!.Id);
        Assert.Equal(650_000m, doc.TotalAmount);
        Assert.Equal(2, doc.Lines.Count);
        Assert.Contains(doc.Lines, l => l.BillId == bill1 && l.Amount == 400_000m);
        Assert.Contains(doc.Lines, l => l.BillId == bill2 && l.Amount == 250_000m);
    }

    [Fact]
    public async Task ModeB_CrossCurrency_Rejects_AC_FD_014()
    {
        // AC-FD-014: Mode B cross currency summation rejected
        var tenantId = await CreateTenantAsync("TN-CROSS-CURR", "Cross Currency Test");
        var billId = await CreateBillAsync(tenantId, "BL-CC-01");
        var customerId = await CreatePartyAsync(tenantId, "CUST-CC", "Cust CC", isCustomer: true);

        var revVnd = await CreateRevenueAsync(tenantId, billId, 1_000_000m, "VND", customerId);
        var revUsd = await CreateRevenueAsync(tenantId, billId, 50m, "USD", customerId);

        using var receiveReq = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        receiveReq.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-CROSS-REJECT",
            direction = "receivable",
            totalAmount = 0m,
            currencyCode = "VND",
            documentDate = "2026-10-03",
            counterpartyId = customerId,
            billId,
            mode = "lcms_generated",
            selectedSourceLines = new[]
            {
                new { sourceId = revVnd, sourceType = "revenue", amount = 1_000_000m, billId },
                new { sourceId = revUsd, sourceType = "revenue", amount = 50m, billId }
            }
        });

        var receiveRes = await _client.SendAsync(receiveReq);
        Assert.Equal(HttpStatusCode.BadRequest, receiveRes.StatusCode);
    }

    [Fact]
    public async Task MissingFxRate_WithoutOverride_Rejects_AC_FD_016()
    {
        // AC-FD-016: Missing FX rate for foreign currency document is rejected
        var tenantId = await CreateTenantAsync("TN-MISSING-FX", "Missing FX Test");
        var billId = await CreateBillAsync(tenantId, "BL-MFX-01");

        using var receiveReq = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        receiveReq.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "INV-JPY-NO-FX",
            direction = "payable",
            totalAmount = 100_000m,
            currencyCode = "JPY", // JPY has no seed FX rate against default VND
            documentDate = "2026-10-03",
            billId,
            mode = "external_received"
        });

        var receiveRes = await _client.SendAsync(receiveReq);
        Assert.Equal(HttpStatusCode.BadRequest, receiveRes.StatusCode);
    }

    // Helpers
    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = Tenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreatePartyAsync(Guid tenantId, string code, string name, bool isCustomer = false, bool isVendor = false)
    {
        var roles = new List<string>();
        if (isCustomer) roles.Add("customer");
        if (isVendor) roles.Add("vendor");
        if (roles.Count == 0) roles.Add("other");

        using var req = Tenant(HttpMethod.Post, "/api/business-parties", tenantId);
        req.Content = JsonContent.Create(new
        {
            code,
            name,
            legalName = name,
            countryCode = "VN",
            defaultCurrencyCode = "VND",
            roleCodes = roles.ToArray()
        });
        var res = await _client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"CreateParty failed: {res.StatusCode} - {err}");
        }
        return (await res.Content.ReadFromJsonAsync<IdBody>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateCostAsync(Guid tenantId, Guid billId, decimal amount, string currency, Guid? vendorId)
    {
        using var req = Tenant(HttpMethod.Post, "/api/costs", tenantId);
        req.Content = JsonContent.Create(new
        {
            billId,
            attributionType = "direct",
            costTypeCode = "FREIGHT",
            amount,
            currencyCode = currency,
            vendorPartyId = vendorId
        });
        var res = await _client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"CreateCost failed: {res.StatusCode} - {err}");
        }
        return (await res.Content.ReadFromJsonAsync<IdBody>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateRevenueAsync(Guid tenantId, Guid billId, decimal amount, string currency, Guid? customerId)
    {
        using var req = Tenant(HttpMethod.Post, "/api/revenues", tenantId);
        req.Content = JsonContent.Create(new
        {
            billId,
            revenueTypeCode = "FREIGHT",
            amount,
            currencyCode = currency,
            customerPartyId = customerId
        });
        var res = await _client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            throw new HttpRequestException($"CreateRevenue failed: {res.StatusCode} - {err}");
        }
        return (await res.Content.ReadFromJsonAsync<IdBody>(JsonOptions))!.Id;
    }

    private async Task<List<JsonElement>> ListCostsAsync(Guid tenantId)
    {
        using var req = Tenant(HttpMethod.Get, "/api/costs", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var items = await res.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        return items ?? new List<JsonElement>();
    }

    private async Task<List<JsonElement>> ListRevenuesAsync(Guid tenantId)
    {
        using var req = Tenant(HttpMethod.Get, "/api/revenues", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var items = await res.Content.ReadFromJsonAsync<List<JsonElement>>(JsonOptions);
        return items ?? new List<JsonElement>();
    }

    private async Task<FinancialDocumentDto> GetDocumentAsync(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/financial-documents/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<FinancialDocumentDto>(JsonOptions))!;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
}
