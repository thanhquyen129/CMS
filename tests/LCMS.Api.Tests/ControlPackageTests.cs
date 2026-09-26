using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class ControlPackageTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public ControlPackageTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task AmbiguousLineToCost_DoesNotAutoPick_AndExactMatchStoresOutcome()
    {
        var tenantId = await CreateTenantAsync();
        var tied = await CreateBillAsync(tenantId, "BL-F-TIE");
        await CreateCostAsync(tenantId, tied, 100m);
        await CreateCostAsync(tenantId, tied, 100m);
        var lineId = await DocumentLineAsync(tenantId, tied, "INV-F-TIE", 100m);
        var matchId = await StartMatchAsync(tenantId, lineId.DocumentId, "line_to_cost", 0m);

        using var resolve = Tenant(HttpMethod.Post, $"/api/document-matches/{matchId}/resolve", tenantId);
        resolve.Content = JsonContent.Create(new { sourceLineId = lineId.LineId });
        var ambiguous = await _client.SendAsync(resolve);
        Assert.Equal(HttpStatusCode.Conflict, ambiguous.StatusCode);
        Assert.Contains("MATCH_AMBIGUOUS", (await ambiguous.Content.ReadFromJsonAsync<Err>(Json))!.Message);

        var single = await CreateBillAsync(tenantId, "BL-F-ONE");
        await CreateCostAsync(tenantId, single, 100m);
        var oneLine = await DocumentLineAsync(tenantId, single, "INV-F-ONE", 100m);
        var oneMatch = await StartMatchAsync(tenantId, oneLine.DocumentId, "line_to_cost", 0m);
        using var pick = Tenant(HttpMethod.Post, $"/api/document-matches/{oneMatch}/resolve", tenantId);
        pick.Content = JsonContent.Create(new { sourceLineId = oneLine.LineId });
        var picked = await _client.SendAsync(pick);
        Assert.Equal(HttpStatusCode.Created, picked.StatusCode);

        var match = await GetMatchAsync(tenantId, oneMatch);
        var detail = Assert.Single(match.Details);
        Assert.Equal("matched", detail.OutcomeCode);
        Assert.Equal(0m, detail.AppliedTolerance);
    }

    [Fact]
    public async Task WithinTolerance_StoresAppliedThreshold()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-F-TOL");
        await CreateCostAsync(tenantId, billId, 99m);
        var line = await DocumentLineAsync(tenantId, billId, "INV-F-TOL", 100m);
        var matchId = await StartMatchAsync(tenantId, line.DocumentId, "line_to_cost", 2m);

        using var resolve = Tenant(HttpMethod.Post, $"/api/document-matches/{matchId}/resolve", tenantId);
        resolve.Content = JsonContent.Create(new { sourceLineId = line.LineId });
        (await _client.SendAsync(resolve)).EnsureSuccessStatusCode();

        var detail = Assert.Single((await GetMatchAsync(tenantId, matchId)).Details);
        Assert.Equal("matched_with_tolerance", detail.OutcomeCode);
        Assert.Equal(2m, detail.AppliedTolerance);
    }

    [Fact]
    public async Task CrossCurrencySettlement_RequiresFxSnapshot_AndDoesNotChangeCost()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-F-FX");
        var costId = await CreateCostAsync(tenantId, billId, 25000m);
        var exposureId = await PostId(tenantId, "/api/payable-exposures", new { amount = 25000m, currencyCode = "VND", billId });
        var apId = await PostId(tenantId, $"/api/payable-exposures/{exposureId}/recognize", new { amount = 25000m });
        var paymentId = await PostId(tenantId, "/api/payments", new { amount = 1m, currencyCode = "USD", billId });

        using (var missing = Tenant(HttpMethod.Post, $"/api/payments/{paymentId}/allocations", tenantId))
        {
            missing.Content = JsonContent.Create(new { accountsPayableId = apId, amount = 1m });
            var blocked = await _client.SendAsync(missing);
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
            Assert.Contains("Không phân bổ khác tiền tệ", (await blocked.Content.ReadFromJsonAsync<Err>(Json))!.Message);
        }

        await PostId(tenantId, "/api/fx-rates", new
        {
            fromCurrencyCode = "USD",
            toCurrencyCode = "VND",
            rateDate = DateOnly.FromDateTime(DateTime.UtcNow),
            rate = 25000m,
            source = "manual"
        });

        using var ok = Tenant(HttpMethod.Post, $"/api/payments/{paymentId}/allocations", tenantId);
        ok.Content = JsonContent.Create(new { accountsPayableId = apId, amount = 1m });
        (await _client.SendAsync(ok)).EnsureSuccessStatusCode();

        var payment = await GetAsync<PaymentBody>(tenantId, $"/api/payments/{paymentId}");
        var allocation = Assert.Single(payment.Allocations);
        Assert.Equal(25000m, allocation.FxRate);
        Assert.Equal(25000m, allocation.SettledAmount);
        Assert.Equal(1m, allocation.Amount);

        var cost = await GetAsync<CostBody>(tenantId, $"/api/costs/{costId}");
        Assert.Equal(25000m, cost.Amount);
    }

    [Fact]
    public async Task CriticalWaiver_WaitsForApproval_AndOtherSeverityWaivesImmediately()
    {
        var tenantId = await CreateTenantAsync();
        var criticalId = await PostId(tenantId, "/api/exceptions", new
        {
            ruleCode = "PC-19",
            severity = "critical",
            title = "Miễn mức nghiêm trọng"
        });
        await PostNoContent(tenantId, $"/api/exceptions/{criticalId}/waive", new { reason = "Đối soát đã rõ" });
        Assert.Equal("waiting", (await GetAsync<ExceptionBody>(tenantId, $"/api/exceptions/{criticalId}")).Status);

        var approvals = await GetAsync<List<ApprovalBody>>(tenantId, "/api/approvals?objectType=exception");
        var approval = Assert.Single(approvals, a => a.ObjectId == criticalId);
        await PostNoContent(tenantId, $"/api/approvals/{approval.Id}/approve", new { decisionReason = "Đồng ý miễn" });
        Assert.Equal("waived", (await GetAsync<ExceptionBody>(tenantId, $"/api/exceptions/{criticalId}")).Status);

        var lowId = await PostId(tenantId, "/api/exceptions", new
        {
            ruleCode = "PC-19",
            severity = "low",
            title = "Miễn mức thấp"
        });
        await PostNoContent(tenantId, $"/api/exceptions/{lowId}/waive", new { reason = "Không trọng yếu" });
        Assert.Equal("waived", (await GetAsync<ExceptionBody>(tenantId, $"/api/exceptions/{lowId}")).Status);
    }

    [Fact]
    public async Task ChangedCost_RequiresRereview_AndReplayKeepsCompletedSession()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-F-APR");
        var costId = await CreateCostAsync(tenantId, billId, 80m);
        var approvalId = await PostId(tenantId, "/api/approvals", new
        {
            objectType = "cost",
            objectId = costId,
            requestReason = "Xác nhận chi phí"
        });
        await PostId(tenantId, $"/api/costs/{costId}/adjustments", new
        {
            adjustmentType = "adjustment",
            deltaAmount = 5m,
            reason = "Bổ sung sau yêu cầu"
        });

        using var decide = Tenant(HttpMethod.Post, $"/api/approvals/{approvalId}/approve", tenantId);
        decide.Content = JsonContent.Create(new { decisionReason = "Duyệt" });
        var blocked = await _client.SendAsync(decide);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("Phê duyệt lại", (await blocked.Content.ReadFromJsonAsync<Err>(Json))!.Message);
        Assert.Equal("needs_rereview", (await GetAsync<ApprovalBody>(tenantId, $"/api/approvals/{approvalId}")).Status);

        var sessionId = await PostId(tenantId, "/api/reconciliations", new
        {
            reconciliationType = "manual",
            billId,
            notes = "Đối soát F"
        });
        await PostNoContent(tenantId, $"/api/reconciliations/{sessionId}/complete", null);
        var replayId = await PostId(tenantId, $"/api/reconciliations/{sessionId}/replay", null);
        Assert.NotEqual(sessionId, replayId);
        Assert.Equal("completed", (await GetAsync<ReconBody>(tenantId, $"/api/reconciliations/{sessionId}")).Status);
        var replay = await GetAsync<ReconBody>(tenantId, $"/api/reconciliations/{replayId}");
        Assert.Equal("draft", replay.Status);
        Assert.Equal(2, replay.VersionNo);
    }

    [Fact]
    public async Task SameIdempotencyKey_CreatesOneDocument()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-F-IDEM");
        var key = "doc-" + Guid.NewGuid().ToString("N");
        var first = await ReceiveAsync(tenantId, billId, "INV-F-IDEM", key);
        var second = await ReceiveAsync(tenantId, billId, "INV-F-IDEM-2", key);
        Assert.Equal(first, second);

        var docs = await GetAsync<List<DocBody>>(tenantId, $"/api/financial-documents?billId={billId}");
        Assert.Single(docs);
    }

    [Fact]
    public async Task Payment_Collection_Exposure_ByBillBusinessCode_StoresBillId()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "HAWB-UAT-002");

        var paymentId = await PostId(tenantId, "/api/payments", new
        {
            amount = 100m,
            currencyCode = "USD",
            billId = "HAWB-UAT-002",
            referenceNo = "UAT-PAY-AP-001"
        });
        var payment = await GetAsync<CashBillBody>(tenantId, $"/api/payments/{paymentId}");
        Assert.Equal(billId, payment.BillId);
        Assert.Equal("HAWB-UAT-002", payment.BillNo);

        var collectionId = await PostId(tenantId, "/api/collections", new
        {
            amount = 100m,
            currencyCode = "USD",
            billId = "HAWB-UAT-002",
            referenceNo = "UAT-COL-AR-001"
        });
        var collection = await GetAsync<CashBillBody>(tenantId, $"/api/collections/{collectionId}");
        Assert.Equal(billId, collection.BillId);
        Assert.Equal("HAWB-UAT-002", collection.BillNo);

        var payableExposureId = await PostId(tenantId, "/api/payable-exposures", new
        {
            amount = 280m,
            currencyCode = "USD",
            billId = "HAWB-UAT-002"
        });
        var payable = await GetAsync<ExposureBillBody>(tenantId, $"/api/payable-exposures/{payableExposureId}");
        Assert.Equal(billId, payable.BillId);
        Assert.Equal("HAWB-UAT-002", payable.BillNo);

        var receivableExposureId = await PostId(tenantId, "/api/receivable-exposures", new
        {
            amount = 280m,
            currencyCode = "USD",
            billId = "HAWB-UAT-002"
        });
        var receivable = await GetAsync<ExposureBillBody>(tenantId, $"/api/receivable-exposures/{receivableExposureId}");
        Assert.Equal(billId, receivable.BillId);
        Assert.Equal("HAWB-UAT-002", receivable.BillNo);

        var apId = await PostId(tenantId, $"/api/payable-exposures/{payableExposureId}/recognize", new { amount = 280m });
        var arId = await PostId(tenantId, $"/api/receivable-exposures/{receivableExposureId}/recognize", new { amount = 280m });
        var ap = await GetAsync<ApArBillBody>(tenantId, $"/api/accounts-payable/{apId}");
        var ar = await GetAsync<ApArBillBody>(tenantId, $"/api/accounts-receivable/{arId}");
        Assert.Equal(billId, ap.BillId);
        Assert.Equal(billId, ar.BillId);

        var profile = await GetAsync<BillProfileBody>(tenantId, $"/api/bills/{billId}/financial-profile");
        Assert.Contains(profile.SettlementOutstanding, b => b.CurrencyCode == "USD" && b.AccountsPayableOutstanding == 280m);
        Assert.Contains(profile.SettlementOutstanding, b => b.CurrencyCode == "USD" && b.AccountsReceivableOutstanding == 280m);
    }

    [Fact]
    public async Task ReceiveDocument_ByBillBusinessCode_StoresBill_AndRejectsUnknownCurrency()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "HAWB-UAT-001");
        var documentId = await PostId(tenantId, "/api/financial-documents", new
        {
            documentType = "invoice",
            documentNo = "UAT-INV-NEW-" + Guid.NewGuid().ToString("N")[..8],
            direction = "payable",
            totalAmount = 235m,
            currencyCode = "USD",
            billId = "HAWB-UAT-001"
        });

        var doc = await GetAsync<DocDetail>(tenantId, $"/api/financial-documents/{documentId}");
        Assert.Equal(billId, doc.BillId);
        Assert.Equal("HAWB-UAT-001", doc.BillNo);
        Assert.Equal("USD", doc.CurrencyCode);

        await PostId(tenantId, $"/api/financial-documents/{documentId}/lines", new
        {
            amount = 235m,
            description = "FREIGHT"
        });
        var detailed = await GetAsync<DocWithLines>(tenantId, $"/api/financial-documents/{documentId}");
        var line = Assert.Single(detailed.Lines);
        Assert.Equal(billId, line.BillId);
        Assert.Equal("HAWB-UAT-001", line.BillNo);

        var listed = await GetAsync<List<DocListItem>>(tenantId, $"/api/financial-documents?billId={billId}");
        Assert.Contains(listed, d => d.Id == documentId && d.BillNo == "HAWB-UAT-001");

        using var badCurrency = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        badCurrency.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "BAD-CCY-" + Guid.NewGuid().ToString("N")[..8],
            direction = "payable",
            totalAmount = 1m,
            currencyCode = "ZZZ"
        });
        var rejected = await _client.SendAsync(badCurrency);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("ZZZ", await rejected.Content.ReadAsStringAsync());

        using var missingBill = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        missingBill.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo = "BAD-BILL-" + Guid.NewGuid().ToString("N")[..8],
            direction = "payable",
            totalAmount = 1m,
            currencyCode = "USD",
            billId = "NO-SUCH-BILL"
        });
        var missing = await _client.SendAsync(missingBill);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("NO-SUCH-BILL", (await missing.Content.ReadFromJsonAsync<Err>(Json))!.Message);
    }

    [Fact]
    public async Task CancelOpenPayment_ReversesDraft_AndBlocksWhenAllocationIsFinalized()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-F-CANCEL");
        await CreateCostAsync(tenantId, billId, 100m);
        var exposureId = await PostId(tenantId, "/api/payable-exposures", new { amount = 100m, currencyCode = "VND", billId });
        var apId = await PostId(tenantId, $"/api/payable-exposures/{exposureId}/recognize", new { amount = 100m });

        var draftPaymentId = await PostId(tenantId, "/api/payments", new { amount = 100m, currencyCode = "VND", billId });
        await PostId(tenantId, $"/api/payments/{draftPaymentId}/allocations", new { accountsPayableId = apId, amount = 40m });
        await PostNoContent(tenantId, $"/api/payments/{draftPaymentId}/cancel", new { reason = "Nhầm phiếu" });
        var cancelled = await GetAsync<CashBody>(tenantId, $"/api/payments/{draftPaymentId}");
        Assert.Equal("cancelled", cancelled.Status);
        Assert.Equal("reversed", Assert.Single(cancelled.Allocations).AllocationStatus);

        var lockedPaymentId = await PostId(tenantId, "/api/payments", new { amount = 60m, currencyCode = "VND", billId });
        var allocationId = await PostId(
            tenantId,
            $"/api/payments/{lockedPaymentId}/allocations",
            new { accountsPayableId = apId, amount = 60m });
        await PostNoContent(tenantId, $"/api/payment-allocations/{allocationId}/finalize", null);
        using var blocked = Tenant(HttpMethod.Post, $"/api/payments/{lockedPaymentId}/cancel", tenantId);
        blocked.Content = JsonContent.Create(new { reason = "Muốn hủy sau chốt" });
        var denied = await _client.SendAsync(blocked);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Contains("đã chốt", (await denied.Content.ReadFromJsonAsync<Err>(Json))!.Message);
    }

    [Fact]
    public async Task SoftDeleteRateCard_AllowsDraftOnly_AndBlocksPublishedVersion()
    {
        var tenantId = await CreateTenantAsync();
        var draftCardId = await PostId(tenantId, "/api/rate-cards", new
        {
            code = "RC-DRAFT-" + Guid.NewGuid().ToString("N")[..8],
            name = "Nháp",
            partyType = "vendor",
            currencyCode = "USD"
        });
        await DeleteNoContent(tenantId, $"/api/rate-cards/{draftCardId}");
        using var gone = Tenant(HttpMethod.Get, $"/api/rate-cards/{draftCardId}", tenantId);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(gone)).StatusCode);

        var publishedCardId = await PostId(tenantId, "/api/rate-cards", new
        {
            code = "RC-PUB-" + Guid.NewGuid().ToString("N")[..8],
            name = "Đã phát hành",
            partyType = "vendor",
            currencyCode = "USD"
        });
        var versionId = await PostId(tenantId, $"/api/rate-cards/{publishedCardId}/versions", new { note = "v1" });
        await PostId(tenantId, $"/api/rate-versions/{versionId}/rules", new
        {
            code = "FREIGHT",
            name = "Cước",
            calcMethod = "fixed",
            unitAmount = 10m,
            currencyCode = "USD",
            sortOrder = 1
        });
        await PostNoContent(tenantId, $"/api/rate-versions/{versionId}/publish", null);
        using var blocked = Tenant(HttpMethod.Delete, $"/api/rate-cards/{publishedCardId}", tenantId);
        var denied = await _client.SendAsync(blocked);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Contains("phát hành", (await denied.Content.ReadFromJsonAsync<Err>(Json))!.Message);
    }

    private async Task<Guid> ReceiveAsync(Guid tenantId, Guid billId, string documentNo, string key)
    {
        using var req = Tenant(HttpMethod.Post, "/api/financial-documents", tenantId);
        req.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        req.Content = JsonContent.Create(new
        {
            documentType = "invoice",
            documentNo,
            direction = "payable",
            totalAmount = 10m,
            currencyCode = "VND",
            billId
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<(Guid DocumentId, Guid LineId)> DocumentLineAsync(
        Guid tenantId, Guid billId, string documentNo, decimal amount)
    {
        var documentId = await PostId(tenantId, "/api/financial-documents", new
        {
            documentType = "invoice",
            documentNo,
            direction = "payable",
            totalAmount = amount,
            currencyCode = "VND",
            billId
        });
        var lineId = await PostId(tenantId, $"/api/financial-documents/{documentId}/lines", new { amount, description = "Dòng" });
        await PostNoContent(tenantId, $"/api/financial-documents/{documentId}/accept", null);
        return (documentId, lineId);
    }

    private async Task<Guid> StartMatchAsync(Guid tenantId, Guid documentId, string method, decimal tolerance)
    {
        return await PostId(tenantId, "/api/document-matches", new
        {
            primaryDocumentId = documentId,
            matchMethod = method,
            toleranceAmount = tolerance
        });
    }

    private async Task<Guid> CreateCostAsync(Guid tenantId, Guid billId, decimal amount) =>
        await PostId(tenantId, "/api/costs", new
        {
            billId,
            attributionType = "direct",
            amount,
            currencyCode = "VND",
            costTypeCode = "FREIGHT"
        });

    private async Task<MatchBody> GetMatchAsync(Guid tenantId, Guid id) =>
        await GetAsync<MatchBody>(tenantId, $"/api/document-matches/{id}");

    private async Task<Guid> PostId(Guid tenantId, string url, object? body)
    {
        using var req = Tenant(HttpMethod.Post, url, tenantId);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task PostNoContent(Guid tenantId, string url, object? body)
    {
        using var req = Tenant(HttpMethod.Post, url, tenantId);
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task DeleteNoContent(Guid tenantId, string url)
    {
        using var req = Tenant(HttpMethod.Delete, url, tenantId);
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<T> GetAsync<T>(Guid tenantId, string url)
    {
        using var req = Tenant(HttpMethod.Get, url, tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        return await PostId(tenantId, "/api/bills", new
        {
            billNo,
            billType = "house",
            sourceSystem = "lcms_manual",
            externalId = billNo
        });
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new
        {
            code = "TN-" + Guid.NewGuid().ToString("N")[..8],
            name = "Control"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record Err(string Message);
    private sealed record MatchBody(List<DetailBody> Details);
    private sealed record DetailBody(string OutcomeCode, decimal AppliedTolerance);
    private sealed record PaymentBody(List<AllocBody> Allocations);
    private sealed record CashBody(string Status, List<CashAlloc> Allocations);
    private sealed record CashAlloc(string AllocationStatus);
    private sealed record AllocBody(decimal Amount, decimal? SettledAmount, decimal? FxRate);
    private sealed record CostBody(decimal Amount);
    private sealed record ExceptionBody(string Status);
    private sealed record ApprovalBody(Guid Id, Guid ObjectId, string Status);
    private sealed record ReconBody(string Status, int VersionNo);
    private sealed record DocBody(Guid Id, string DocumentNo);
    private sealed record DocDetail(Guid BillId, string? BillNo, string CurrencyCode);
    private sealed record DocLine(Guid? BillId, string? BillNo);
    private sealed record DocWithLines(Guid BillId, string? BillNo, List<DocLine> Lines);
    private sealed record DocListItem(Guid Id, string? BillNo);
    private sealed record CashBillBody(Guid? BillId, string? BillNo);
    private sealed record ExposureBillBody(Guid? BillId, string? BillNo);
    private sealed record ApArBillBody(Guid? BillId);
    private sealed record BillProfileBody(List<SettlementBucket> SettlementOutstanding);
    private sealed record SettlementBucket(
        string CurrencyCode,
        decimal AccountsPayableOutstanding,
        decimal AccountsReceivableOutstanding);
}
