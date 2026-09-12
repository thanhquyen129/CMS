using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Domain.Terminology;

namespace LCMS.Api.Tests;

/// <summary>
/// Pass 2 Sprint 12 FULL — Audit FULL, integration recovery/outbox stub, NFR, AC gate smoke, VI UX.
/// </summary>
[Collection("Api")]
public sealed class Sprint12FullHardeningUatTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public Sprint12FullHardeningUatTests(LcmsApiFactory factory)
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
    public async Task AuditFull_RicherJson_Filters_And_CoveredMutations()
    {
        var tenantId = await CreateTenantAsync("TN-E14F-AUD", "Audit FULL Tenant");
        var billId = await CreateBillAsync(tenantId, "BL-E14F-1", "freight");
        var userId = Guid.NewGuid();
        var correlationId = $"corr-full-{Guid.NewGuid():N}";
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);

        // Cost create + confirm
        var costId = await CreateDirectCostAsync(tenantId, billId, 1000m, "FREIGHT", userId, correlationId);
        await ConfirmCostAsync(tenantId, costId, 1100m, userId, correlationId);

        // Document accept + match
        var docId = await ReceiveDocumentAsync(tenantId, billId, "INV-E14F-1", 500m);
        var lineId = await AddLineAsync(tenantId, docId, 500m, "freight line");
        await AcceptDocumentAsync(tenantId, docId, userId, correlationId);
        var matchId = await StartMatchAsync(tenantId, docId, "line_to_cost");
        await AddMatchDetailAsync(tenantId, matchId, lineId, costId, 500m, userId, correlationId);

        // AP recognize + payment finalize + write-off
        var apId = await RecognizePayableAsync(
            tenantId, await CreatePayableExposureAsync(tenantId, billId, 200m), 200m, userId, correlationId);
        var paymentId = await CreatePaymentAsync(tenantId, billId, 150m);
        var allocId = await AllocatePaymentAsync(tenantId, paymentId, apId, 150m);
        await FinalizePaymentAllocationAsync(tenantId, allocId, userId, correlationId);
        await WriteOffPayableAsync(tenantId, apId, 10m, "làm tròn", userId, correlationId);

        // AR recognize
        await RecognizeReceivableAsync(
            tenantId, await CreateReceivableExposureAsync(tenantId, billId, 300m), 300m, userId, correlationId);

        var byCorr = await ListAuditEventsAsync(tenantId, correlationId: correlationId);
        Assert.Contains(byCorr, e => e.Action == "cost.create" && e.AfterJson!.Contains("\"amount\":1000"));
        Assert.Contains(byCorr, e => e.Action == "cost.confirm"
            && e.BeforeJson!.Contains("maturity")
            && e.AfterJson!.Contains("\"confirmed\":1100"));
        Assert.Contains(byCorr, e => e.Action == "financial_document.accept"
            && e.BeforeJson!.Contains("acceptanceStatus")
            && e.AfterJson!.Contains("accepted"));
        Assert.Contains(byCorr, e => e.Action == "document_match.detail_add"
            && e.AfterJson!.Contains("matchedAmount"));
        Assert.Contains(byCorr, e => e.Action == "accounts_payable.recognize"
            && e.AfterJson!.Contains("recognizedAmount"));
        Assert.Contains(byCorr, e => e.Action == "accounts_receivable.recognize");
        Assert.Contains(byCorr, e => e.Action == "payment_allocation.finalize"
            && e.BeforeJson!.Contains("apOutstanding")
            && e.AfterJson!.Contains("apSettled"));
        Assert.Contains(byCorr, e => e.Action == "accounts_payable.write_off"
            && e.AfterJson!.Contains("writeOff"));

        Assert.All(byCorr, e =>
        {
            Assert.Equal(userId, e.ActorId);
            Assert.Equal(correlationId, e.CorrelationId);
        });

        // Filters: action + objectType + date range
        var byAction = await ListAuditEventsAsync(tenantId, action: "cost.confirm");
        Assert.Contains(byAction, e => e.ObjectId == costId);
        Assert.DoesNotContain(byAction, e => e.Action == "cost.create");

        var byObject = await ListAuditEventsAsync(tenantId, objectType: "accounts_payable", objectId: apId);
        Assert.All(byObject, e => Assert.Equal(apId, e.ObjectId));
        Assert.Contains(byObject, e => e.Action == "accounts_payable.recognize");
        Assert.Contains(byObject, e => e.Action == "accounts_payable.write_off");

        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var byRange = await ListAuditEventsAsync(tenantId, from: from, to: to);
        Assert.NotEmpty(byRange);
        Assert.Contains(byRange, e => e.CorrelationId == correlationId);

        // Tenant isolation
        var other = await CreateTenantAsync("TN-E14F-AUD2", "Other");
        var otherEvents = await ListAuditEventsAsync(other);
        Assert.DoesNotContain(otherEvents, e => e.ObjectId == costId);
    }

    [Fact]
    public async Task IntegrationRecovery_OutboxStub_DuplicateStillC002()
    {
        var tenantA = await CreateTenantAsync("TN-E14F-INT-A", "Int A");
        var tenantB = await CreateTenantAsync("TN-E14F-INT-B", "Int B");

        var recordId = await UpsertIntegrationAsync(tenantA, "ops", "invoice", "EXT-FULL-1");

        // Duplicate → 409 C-002
        using (var dup = new HttpRequestMessage(HttpMethod.Post, "/api/integration-records")
        {
            Content = JsonContent.Create(new
            {
                sourceSystem = "ops",
                externalObjectType = "invoice",
                externalId = "EXT-FULL-1"
            })
        })
        {
            dup.Headers.Add("X-Tenant-Id", tenantA.ToString());
            var response = await _client.SendAsync(dup);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var err = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);
            Assert.Contains("C-002", err!.Message);
        }

        // Same key OK for other tenant
        var recordB = await UpsertIntegrationAsync(tenantB, "ops", "invoice", "EXT-FULL-1");
        Assert.NotEqual(recordId, recordB);

        // Record error → mark-retried
        var errorId = await RecordIntegrationErrorAsync(tenantA, recordId, "SYNC_FAIL", "Lỗi đồng bộ stub");
        var pending = await ListIntegrationErrorsAsync(tenantA, recoveryStatus: "pending");
        Assert.Contains(pending, e => e.Id == errorId && e.AttemptNo == 1);

        await MarkRetriedAsync(tenantA, errorId, "đã thử lại");
        var retried = await ListIntegrationErrorsAsync(tenantA, recoveryStatus: "retried");
        Assert.Contains(retried, e => e.Id == errorId);

        // Second error → dead-letter
        var error2 = await RecordIntegrationErrorAsync(tenantA, recordId, "SYNC_FAIL2", "Lỗi lần 2");
        await DeadLetterAsync(tenantA, error2, "hết retry");
        var dead = await ListIntegrationErrorsAsync(tenantA, recoveryStatus: "dead_letter");
        Assert.Contains(dead, e => e.Id == error2);

        var records = await ListIntegrationsAsync(tenantA);
        Assert.Contains(records, r => r.Id == recordId && r.Status == "dead_letter");

        // Outbox enqueue + process-once (no broker)
        var outboxId = await EnqueueOutboxAsync(tenantA, "integration.retry", "{\"recordId\":\"" + recordId + "\"}");
        var process = await ProcessOutboxOnceAsync(tenantA);
        Assert.True(process.Processed);
        Assert.Equal(outboxId, process.MessageId);
        Assert.Equal("processed", process.Status);

        var empty = await ProcessOutboxOnceAsync(tenantA);
        Assert.False(empty.Processed);

        var listed = await ListOutboxAsync(tenantA, status: "processed");
        Assert.Contains(listed, m => m.Id == outboxId && m.AttemptNo == 1);
    }

    [Fact]
    public async Task Nfr_SecurityHeaders_RateLimitConfig_TimedSmoke_And_ViGaps()
    {
        var health = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("nosniff", health.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", health.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(health.Headers.TryGetValues("X-Correlation-Id", out _));

        var ready = await _client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        // Timed API smoke (not full soak) — terminology + health under modest latency budget
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 20; i++)
        {
            var t = await _client.GetAsync("/api/terminology");
            Assert.Equal(HttpStatusCode.OK, t.StatusCode);
            Assert.Equal("nosniff", t.Headers.GetValues("X-Content-Type-Options").Single());
        }

        sw.Stop();
        // Soft budget: 20 GETs should finish well under 5s in-process test host
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Timed smoke took {sw.ElapsedMilliseconds}ms");

        var terminology = await _client.GetAsync("/api/terminology");
        var dict = await terminology.Content.ReadFromJsonAsync<Dictionary<string, string>>(JsonOptions);
        Assert.NotNull(dict);

        string[] required =
        [
            "BILL", "COST", "REVENUE", "EXPECTED", "CONFIRMED", "ACTUAL",
            "PAYMENT", "COLLECTION", "RECONCILIATION", "EXCEPTION", "APPROVAL",
            "FINANCIAL_CLOSE", "FINANCIAL_CLOSE_SNAPSHOT", "DASHBOARD",
            "AUDIT_EVENT", "AUDIT_BEFORE", "AUDIT_AFTER", "AUDIT_DATE_FROM", "AUDIT_DATE_TO",
            "INTEGRATION_RECORD", "INTEGRATION_ERROR", "INTEGRATION_RETRY", "INTEGRATION_DEAD_LETTER",
            "OUTBOX_MESSAGE", "OUTBOX_ENQUEUE", "OUTBOX_PROCESS_ONCE",
            "CORRELATION_ID", "RATE_LIMIT", "RATE_LIMIT_MONEY_PATH", "PASS2_COMPLETE"
        ];
        foreach (var key in required)
        {
            Assert.True(dict!.ContainsKey(key), $"Missing terminology key: {key}");
            Assert.False(string.IsNullOrWhiteSpace(dict[key]));
            Assert.True(VietnameseUiTerms.All.ContainsKey(key), $"VietnameseUiTerms missing: {key}");
        }
    }

    [Fact]
    public async Task AcGateSmoke_Ac007_Ac008_Ac009()
    {
        var tenantId = await CreateTenantAsync("TN-E14F-AC", "AC Gate Smoke");

        // --- AC-007 + AC-009 on settlement bill ---
        var settleBill = await CreateBillAsync(tenantId, "BL-E14F-AC-SET", "freight");
        var apId = await RecognizePayableAsync(
            tenantId, await CreatePayableExposureAsync(tenantId, settleBill, 400m), 400m);
        var paymentId = await CreatePaymentAsync(tenantId, settleBill, 200m);
        var allocId = await AllocatePaymentAsync(tenantId, paymentId, apId, 200m);

        var apAfterDraft = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(0m, apAfterDraft.FinalizedSettledAmount);
        Assert.Equal(400m, apAfterDraft.Outstanding);

        await FinalizePaymentAllocationAsync(tenantId, allocId);
        var apAfterFinal = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(200m, apAfterFinal.FinalizedSettledAmount);
        Assert.Equal(200m, apAfterFinal.Outstanding);

        // AC-009: repeat finalize is idempotent (no double settle)
        await FinalizePaymentAllocationAsync(tenantId, allocId);
        var apIdempotent = await GetAccountsPayableAsync(tenantId, apId);
        Assert.Equal(200m, apIdempotent.FinalizedSettledAmount);
        Assert.Equal(200m, apIdempotent.Outstanding);

        // --- AC-008: close snapshot immutable (clean bill — no unsettled AP/AR) ---
        var closeBill = await CreateBillAsync(tenantId, "BL-E14F-AC-CLS", "freight");
        await CreateDirectCostAsync(tenantId, closeBill, 1000m, "FREIGHT");
        var closeId = await StartCloseAsync(tenantId, closeBill);
        var snapshot1Id = await CreateSnapshotAsync(tenantId, closeId);
        var snap1 = await GetSnapshotAsync(tenantId, snapshot1Id);
        var hash1 = snap1.ImmutableHash;
        Assert.False(string.IsNullOrWhiteSpace(hash1));

        using (var lockedSnap = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-closes/{closeId}/snapshot"))
        {
            lockedSnap.Headers.Add("X-Tenant-Id", tenantId.ToString());
            var response = await _client.SendAsync(lockedSnap);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("C-010", body);
        }

        await ReopenCloseAsync(tenantId, closeId);
        var snapshot2Id = await CreateSnapshotAsync(tenantId, closeId);
        Assert.NotEqual(snapshot1Id, snapshot2Id);

        var snap1Again = await GetSnapshotAsync(tenantId, snapshot1Id);
        Assert.Equal(hash1, snap1Again.ImmutableHash);
        Assert.Equal(1, snap1Again.SnapshotVersion);

        var snap2 = await GetSnapshotAsync(tenantId, snapshot2Id);
        Assert.Equal(2, snap2.SnapshotVersion);
    }

    // --- helpers ---

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo, string billType)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/bills")
        {
            Content = JsonContent.Create(new { billNo, billType })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateDirectCostAsync(
        Guid tenantId,
        Guid billId,
        decimal amount,
        string costTypeCode,
        Guid? userId = null,
        string? correlationId = null)
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
        if (userId.HasValue) req.Headers.Add("X-User-Id", userId.Value.ToString());
        if (!string.IsNullOrWhiteSpace(correlationId)) req.Headers.Add("X-Correlation-Id", correlationId);
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task ConfirmCostAsync(
        Guid tenantId, Guid costId, decimal confirmedAmount, Guid userId, string correlationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/costs/{costId}/confirm")
        {
            Content = JsonContent.Create(new { confirmedAmount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-Correlation-Id", correlationId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> ReceiveDocumentAsync(Guid tenantId, Guid billId, string documentNo, decimal total)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/financial-documents")
        {
            Content = JsonContent.Create(new
            {
                documentType = "invoice",
                documentNo,
                direction = "payable",
                totalAmount = total,
                currencyCode = "VND",
                billId
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> AddLineAsync(Guid tenantId, Guid documentId, decimal amount, string description)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{documentId}/lines")
        {
            Content = JsonContent.Create(new { amount, description })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task AcceptDocumentAsync(Guid tenantId, Guid documentId, Guid userId, string correlationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-documents/{documentId}/accept");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-Correlation-Id", correlationId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<Guid> StartMatchAsync(Guid tenantId, Guid primaryDocumentId, string matchMethod)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/document-matches")
        {
            Content = JsonContent.Create(new { primaryDocumentId, matchMethod })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task AddMatchDetailAsync(
        Guid tenantId, Guid matchId, Guid sourceLineId, Guid targetCostId, decimal matchedAmount,
        Guid userId, string correlationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/document-matches/{matchId}/details")
        {
            Content = JsonContent.Create(new { sourceLineId, targetCostId, matchedAmount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-Correlation-Id", correlationId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreatePayableExposureAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payable-exposures")
        {
            Content = JsonContent.Create(new { amount, currencyCode = "VND", billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateReceivableExposureAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/receivable-exposures")
        {
            Content = JsonContent.Create(new { amount, currencyCode = "VND", billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> RecognizePayableAsync(
        Guid tenantId, Guid exposureId, decimal amount, Guid? userId = null, string? correlationId = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (userId.HasValue) req.Headers.Add("X-User-Id", userId.Value.ToString());
        if (!string.IsNullOrWhiteSpace(correlationId)) req.Headers.Add("X-Correlation-Id", correlationId);
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> RecognizeReceivableAsync(
        Guid tenantId, Guid exposureId, decimal amount, Guid? userId = null, string? correlationId = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/receivable-exposures/{exposureId}/recognize")
        {
            Content = JsonContent.Create(new { amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (userId.HasValue) req.Headers.Add("X-User-Id", userId.Value.ToString());
        if (!string.IsNullOrWhiteSpace(correlationId)) req.Headers.Add("X-Correlation-Id", correlationId);
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreatePaymentAsync(Guid tenantId, Guid billId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new { amount, currencyCode = "VND", billId })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> AllocatePaymentAsync(Guid tenantId, Guid paymentId, Guid apId, decimal amount)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/{paymentId}/allocations")
        {
            Content = JsonContent.Create(new { accountsPayableId = apId, amount })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task FinalizePaymentAllocationAsync(
        Guid tenantId, Guid allocationId, Guid? userId = null, string? correlationId = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/payment-allocations/{allocationId}/finalize");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (userId.HasValue) req.Headers.Add("X-User-Id", userId.Value.ToString());
        if (!string.IsNullOrWhiteSpace(correlationId)) req.Headers.Add("X-Correlation-Id", correlationId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task WriteOffPayableAsync(
        Guid tenantId, Guid apId, decimal amount, string reason, Guid userId, string correlationId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/accounts-payable/{apId}/write-off")
        {
            Content = JsonContent.Create(new { amount, reason })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        req.Headers.Add("X-User-Id", userId.ToString());
        req.Headers.Add("X-Correlation-Id", correlationId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<AccountsPayableDto> GetAccountsPayableAsync(Guid tenantId, Guid id)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/accounts-payable/{id}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccountsPayableDto>(JsonOptions))!;
    }

    private async Task<Guid> StartCloseAsync(Guid tenantId, Guid billId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/financial-closes")
        {
            Content = JsonContent.Create(new
            {
                scopeType = "bill",
                scopeId = billId,
                periodFrom = new DateOnly(2026, 9, 1),
                periodTo = new DateOnly(2026, 9, 30),
                policyVersion = "controlled"
            })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CreateSnapshotAsync(Guid tenantId, Guid closeId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-closes/{closeId}/snapshot");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task ReopenCloseAsync(Guid tenantId, Guid closeId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/api/financial-closes/{closeId}/reopen")
        {
            Content = JsonContent.Create(new { reason = "AC-008 smoke reopen" })
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(req)).StatusCode);
    }

    private async Task<SnapshotDto> GetSnapshotAsync(Guid tenantId, Guid snapshotId)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/api/financial-close-snapshots/{snapshotId}");
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SnapshotDto>(JsonOptions))!;
    }

    private async Task<Guid> UpsertIntegrationAsync(
        Guid tenantId, string sourceSystem, string externalObjectType, string externalId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/integration-records")
        {
            Content = JsonContent.Create(new { sourceSystem, externalObjectType, externalId })
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> RecordIntegrationErrorAsync(
        Guid tenantId, Guid integrationRecordId, string errorCode, string message)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/integration-errors")
        {
            Content = JsonContent.Create(new { integrationRecordId, errorCode, message })
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task MarkRetriedAsync(Guid tenantId, Guid errorId, string note)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/integration-errors/{errorId}/mark-retried")
        {
            Content = JsonContent.Create(new { note })
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(request)).StatusCode);
    }

    private async Task DeadLetterAsync(Guid tenantId, Guid errorId, string note)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/integration-errors/{errorId}/dead-letter")
        {
            Content = JsonContent.Create(new { note })
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(request)).StatusCode);
    }

    private async Task<List<IntegrationErrorDto>> ListIntegrationErrorsAsync(
        Guid tenantId, string? recoveryStatus = null)
    {
        var url = "/api/integration-errors";
        if (!string.IsNullOrWhiteSpace(recoveryStatus))
        {
            url += $"?recoveryStatus={Uri.EscapeDataString(recoveryStatus)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<IntegrationErrorDto>>(JsonOptions))!;
    }

    private async Task<List<IntegrationRecordDto>> ListIntegrationsAsync(Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/integration-records");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<IntegrationRecordDto>>(JsonOptions))!;
    }

    private async Task<Guid> EnqueueOutboxAsync(Guid tenantId, string topic, string payloadJson)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/outbox/enqueue")
        {
            Content = JsonContent.Create(new { topic, payloadJson })
        };
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions))!.Id;
    }

    private async Task<ProcessOutboxResult> ProcessOutboxOnceAsync(Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/outbox/process-once");
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProcessOutboxResult>(JsonOptions))!;
    }

    private async Task<List<OutboxDto>> ListOutboxAsync(Guid tenantId, string? status = null)
    {
        var url = "/api/outbox";
        if (!string.IsNullOrWhiteSpace(status))
        {
            url += $"?status={Uri.EscapeDataString(status)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<OutboxDto>>(JsonOptions))!;
    }

    private async Task<List<AuditEventDto>> ListAuditEventsAsync(
        Guid tenantId,
        string? correlationId = null,
        string? action = null,
        string? objectType = null,
        Guid? objectId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(correlationId))
            qs.Add($"correlationId={Uri.EscapeDataString(correlationId)}");
        if (!string.IsNullOrWhiteSpace(action))
            qs.Add($"action={Uri.EscapeDataString(action)}");
        if (!string.IsNullOrWhiteSpace(objectType))
            qs.Add($"objectType={Uri.EscapeDataString(objectType)}");
        if (objectId.HasValue)
            qs.Add($"objectId={objectId.Value}");
        if (from.HasValue)
            qs.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        if (to.HasValue)
            qs.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");

        var url = "/api/audit-events" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Tenant-Id", tenantId.ToString());
        var response = await _client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }
        return (await response.Content.ReadFromJsonAsync<List<AuditEventDto>>(JsonOptions))!;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record ErrorResponse(string CorrelationId, string Code, string Message);

    private sealed record AuditEventDto(
        Guid Id,
        Guid? ActorId,
        string Action,
        string ObjectType,
        Guid ObjectId,
        string? BeforeJson,
        string? AfterJson,
        string? Reason,
        string? CorrelationId,
        DateTimeOffset OccurredAt);

    private sealed record IntegrationRecordDto(
        Guid Id,
        string SourceSystem,
        string ExternalObjectType,
        string ExternalId,
        string Status);

    private sealed record IntegrationErrorDto(
        Guid Id,
        Guid IntegrationRecordId,
        string ErrorCode,
        string Message,
        int AttemptNo,
        string RecoveryStatus);

    private sealed record ProcessOutboxResult(bool Processed, Guid? MessageId, string? Topic, string? Status);

    private sealed record OutboxDto(Guid Id, string Topic, string Status, int AttemptNo);

    private sealed record AccountsPayableDto(
        Guid Id,
        decimal RecognizedAmount,
        decimal AdjustmentAmount,
        decimal FinalizedSettledAmount,
        decimal Outstanding,
        string SettlementStatus);

    private sealed record SnapshotDto(
        Guid Id,
        int SnapshotVersion,
        string ImmutableHash);
}
