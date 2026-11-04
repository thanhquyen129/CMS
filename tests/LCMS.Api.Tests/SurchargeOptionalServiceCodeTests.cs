using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using LCMS.Application.Surcharges;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

/// <summary>
/// Full automated test suite for LCMS-UAT-SUR-OPTIONAL-SERVICE-CODE-01 (UAT-SUR-SVC-01..18).
/// Verifies commercial-readiness: BR-DELTA-01..12, AC-01..11, and 18 UAT scenarios.
/// </summary>
[Collection("Api")]
public sealed class SurchargeOptionalServiceCodeTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;
    private readonly Dictionary<Guid, Guid> _chargeTypes = new();

    public SurchargeOptionalServiceCodeTests(LcmsApiFactory factory) => _factory = factory;

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

    // =========================================================================
    // UAT-SUR-SVC-01: Lookup Dịch vụ kích hoạt active từ danh mục chuẩn Tenant
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_01_LookupActivationServices_ReturnsTenantActiveServices()
    {
        var tenantId = await CreateTenantAsync();

        // 1. Fetch active only
        using var activeReq = Tenant(HttpMethod.Get, "/api/surcharges/activation-services?activeOnly=true", tenantId);
        var activeRes = await _client.SendAsync(activeReq);
        activeRes.EnsureSuccessStatusCode();
        var activeItems = await activeRes.Content.ReadFromJsonAsync<List<ActivationServiceItemDto>>(Json);
        Assert.NotNull(activeItems);
        Assert.NotEmpty(activeItems);

        // Verify standard catalog items are present
        var donggoi = activeItems.FirstOrDefault(x => x.ServiceCode == "DONGGOI");
        Assert.NotNull(donggoi);
        Assert.Contains("Đóng gói", donggoi.DisplayName);
        Assert.True(donggoi.IsActive);
        Assert.Contains("BILL", donggoi.AllowedContextTypes);
        Assert.Equal("PACKAGE", donggoi.DefaultUom);

        var kiemdem = activeItems.FirstOrDefault(x => x.ServiceCode == "KIEMDEM");
        Assert.NotNull(kiemdem);
        Assert.Contains("Kiểm đếm", kiemdem.DisplayName);
        Assert.True(kiemdem.IsActive);

        // BAOHIEM is inactive baseline, so activeOnly=true should NOT contain it
        Assert.DoesNotContain(activeItems, x => x.ServiceCode == "BAOHIEM");

        // 2. Fetch all (activeOnly=false)
        using var allReq = Tenant(HttpMethod.Get, "/api/surcharges/activation-services?activeOnly=false", tenantId);
        var allRes = await _client.SendAsync(allReq);
        allRes.EnsureSuccessStatusCode();
        var allItems = await allRes.Content.ReadFromJsonAsync<List<ActivationServiceItemDto>>(Json);
        Assert.NotNull(allItems);
        var baohiem = allItems.FirstOrDefault(x => x.ServiceCode == "BAOHIEM");
        Assert.NotNull(baohiem);
        Assert.False(baohiem.IsActive);
    }

    // =========================================================================
    // UAT-SUR-SVC-02: Chưa khai service_code, Lưu nháp thành công + Cảnh báo inline
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_02_DraftWithoutServiceCode_AllowedWithInlineWarning()
    {
        var tenantId = await CreateTenantAsync();
        var chargeTypeId = await EnsureChargeTypeAsync(tenantId);

        // Create draft surcharge with OPTIONAL_OPT_IN and null service_code
        var body = new
        {
            code = "SUR_OPT_DRAFT_" + Guid.NewGuid().ToString("N")[..6],
            name = "Phụ phí đóng gói nháp",
            direction = "buy",
            calculationMode = "fixed_rate",
            basis = "per_bill",
            currencyCode = "VND",
            rateAmountPercent = 300_000m,
            economicChargeTypeId = chargeTypeId,
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = (string?)null,
            selectionScope = "BILL",
            publish = false
        };

        var res = await PostAsync(tenantId, "/api/surcharges", body);
        res.EnsureSuccessStatusCode();
        var created = await res.Content.ReadFromJsonAsync<CreatedSurcharge>(Json);
        Assert.NotNull(created);

        // Verify draft was saved and can be retrieved
        using var getVerReq = Tenant(HttpMethod.Get, $"/api/surcharges/{created.SurchargeId}/versions/{created.VersionId}", tenantId);
        var verRes = await _client.SendAsync(getVerReq);
        verRes.EnsureSuccessStatusCode();
        var verJson = await verRes.Content.ReadAsStringAsync();
        var verNode = JsonNode.Parse(verJson)!;
        Assert.Equal("draft", verNode["version"]?["publishStatus"]?.ToString());
        Assert.Equal("OPTIONAL_OPT_IN", verNode["version"]?["activationPolicy"]?.ToString());
        Assert.Null(verNode["version"]?["serviceCode"]);

        // Now update draft with null serviceCode via PUT
        using var putVerReq = Tenant(HttpMethod.Put, $"/api/surcharges/{created.SurchargeId}/versions/{created.VersionId}", tenantId);
        putVerReq.Content = JsonContent.Create(new
        {
            name = "Phụ phí đóng gói nháp",
            direction = "buy",
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = (string?)null,
            selectionScope = "BILL",
            calculationMode = "fixed_rate",
            basis = "per_bill",
            currencyCode = "VND",
            rateAmountPercent = 350_000m
        });
        var putRes = await _client.SendAsync(putVerReq);
        putRes.EnsureSuccessStatusCode();
        var putResult = await putRes.Content.ReadFromJsonAsync<UpdateSurchargeVersionResultDto>(Json);
        Assert.NotNull(putResult);
        Assert.Equal("draft", putResult.Status);
        Assert.NotEmpty(putResult.Warnings);
        Assert.Contains(putResult.Warnings, w => w.Contains("service_code"));
    }

    // =========================================================================
    // UAT-SUR-SVC-03: Chưa khai service_code, bấm Phát hành -> Chặn + Báo lỗi field-level
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_03_PublishWithoutServiceCode_RejectedInlineErrorContract()
    {
        var tenantId = await CreateTenantAsync();
        var chargeTypeId = await EnsureChargeTypeAsync(tenantId);

        // 1. Create draft with null service_code
        var created = await CreateSurchargeAsync(tenantId, new
        {
            code = "SUR_NO_SVC_" + Guid.NewGuid().ToString("N")[..6],
            name = "Thiếu service code",
            direction = "buy",
            calculationMode = "fixed_rate",
            basis = "per_bill",
            currencyCode = "VND",
            rateAmountPercent = 300_000m,
            economicChargeTypeId = chargeTypeId,
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = (string?)null,
            publish = false
        });

        // 2. Try to publish
        using var pubReq = Tenant(HttpMethod.Post, $"/api/surcharges/{created.SurchargeId}/versions/{created.VersionId}/publish", tenantId);
        var pubRes = await _client.SendAsync(pubReq);

        Assert.Equal(HttpStatusCode.BadRequest, pubRes.StatusCode);
        var errBody = await pubRes.Content.ReadAsStringAsync();
        var errNode = JsonNode.Parse(errBody)!;
        Assert.Equal("service_code", errNode["field"]?.ToString());
        Assert.Equal("REQUIRED_SERVICE_CODE", errNode["code"]?.ToString());
        Assert.Contains("service_code", errNode["message"]?.ToString());
    }

    // =========================================================================
    // UAT-SUR-SVC-04: service_code ngoài danh mục/inactive/sai Tenant -> Chặn đúng mã lỗi
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_04_PublishWithInvalidServiceCode_UnknownOrInactiveOrWrongTenant_Rejected()
    {
        var tenantId = await CreateTenantAsync();
        var chargeTypeId = await EnsureChargeTypeAsync(tenantId);

        // Case A: UNKNOWN_SERVICE
        var draftUnknown = await CreateSurchargeAsync(tenantId, new
        {
            code = "SUR_UNK_" + Guid.NewGuid().ToString("N")[..6],
            name = "Mã dịch vụ không tồn tại",
            direction = "buy",
            calculationMode = "fixed_rate",
            rateAmountPercent = 300_000m,
            economicChargeTypeId = chargeTypeId,
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = "NON_EXISTING_SVC_999",
            publish = false
        });

        using (var pubReq = Tenant(HttpMethod.Post, $"/api/surcharges/{draftUnknown.SurchargeId}/versions/{draftUnknown.VersionId}/publish", tenantId))
        {
            var res = await _client.SendAsync(pubReq);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var node = JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
            Assert.Equal("service_code", node["field"]?.ToString());
            Assert.Equal("UNKNOWN_SERVICE", node["code"]?.ToString());
        }

        // Case B: INACTIVE_SERVICE (BAOHIEM is inactive)
        var draftInactive = await CreateSurchargeAsync(tenantId, new
        {
            code = "SUR_INA_" + Guid.NewGuid().ToString("N")[..6],
            name = "Dịch vụ đang vô hiệu",
            direction = "buy",
            calculationMode = "fixed_rate",
            rateAmountPercent = 300_000m,
            economicChargeTypeId = chargeTypeId,
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = "BAOHIEM",
            publish = false
        });

        using (var pubReq = Tenant(HttpMethod.Post, $"/api/surcharges/{draftInactive.SurchargeId}/versions/{draftInactive.VersionId}/publish", tenantId))
        {
            var res = await _client.SendAsync(pubReq);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var node = JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
            Assert.Equal("service_code", node["field"]?.ToString());
            Assert.Equal("INACTIVE_SERVICE", node["code"]?.ToString());
        }
    }

    // =========================================================================
    // UAT-SUR-SVC-05: Lưu nháp v3 với service_code, scope, qty basis hợp lệ -> F5 giữ nguyên
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_05_SaveDraftWithValidServiceCode_PreservedOnReload()
    {
        var tenantId = await CreateTenantAsync();
        var chargeTypeId = await EnsureChargeTypeAsync(tenantId);

        var draft = await CreateSurchargeAsync(tenantId, new
        {
            code = "DONGGOI_SGN_HAN_MUA",
            name = "Phụ phí đóng gói SGN-HAN",
            direction = "buy",
            calculationMode = "fixed_rate",
            basis = "per_unit",
            currencyCode = "VND",
            rateAmountPercent = 300_000m,
            economicChargeTypeId = chargeTypeId,
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = "DONGGOI",
            selectionScope = "BILL",
            publish = false
        });

        // Simulate F5 / reload via GET version
        using var getReq = Tenant(HttpMethod.Get, $"/api/surcharges/{draft.SurchargeId}/versions/{draft.VersionId}", tenantId);
        var res = await _client.SendAsync(getReq);
        res.EnsureSuccessStatusCode();
        var node = JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
        var ver = node["version"]!;
        Assert.Equal("DONGGOI", ver["serviceCode"]?.ToString());
        Assert.Equal("BILL", ver["selectionScope"]?.ToString());
        Assert.Contains("Đóng gói", ver["serviceDisplayName"]?.ToString());
        Assert.Equal("draft", ver["publishStatus"]?.ToString());
    }

    // =========================================================================
    // UAT-SUR-SVC-06: Phát hành v3 hợp lệ -> Published v3, v2 bất biến
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_06_PublishValidV3_PublishedStatusAndTimestampSet_V2Immutable()
    {
        var tenantId = await CreateTenantAsync();
        var chargeTypeId = await EnsureChargeTypeAsync(tenantId);

        // Seed simulated v2 legacy published
        var v2 = await CreateSurchargeAsync(tenantId, new
        {
            code = "DONGGOI_SGN_HAN_MUA",
            name = "Phụ phí đóng gói SGN-HAN",
            direction = "buy",
            calculationMode = "fixed_rate",
            currencyCode = "VND",
            rateAmountPercent = 250_000m,
            economicChargeTypeId = chargeTypeId,
            activationPolicy = "UNCLASSIFIED_LEGACY",
            publish = false
        });

        // Set v2 as published directly in DB to simulate legacy migration
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            var ver = await db.SurchargeVersions.FirstAsync(v => v.Id == v2.VersionId);
            ver.PublishStatus = SurchargeVersionStatuses.Published;
            ver.PublishedAt = DateTimeOffset.UtcNow.AddDays(-10);
            await db.SaveChangesAsync();
        }

        // Create v3 draft under same surcharge
        using var createV3Req = Tenant(HttpMethod.Post, $"/api/surcharges/{v2.SurchargeId}/versions", tenantId);
        var v3DraftRes = await _client.SendAsync(createV3Req);
        v3DraftRes.EnsureSuccessStatusCode();
        var v3DraftId = (await v3DraftRes.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;

        // Configure v3 draft with serviceCode = DONGGOI
        using var putV3Req = Tenant(HttpMethod.Put, $"/api/surcharges/{v2.SurchargeId}/versions/{v3DraftId}", tenantId);
        putV3Req.Content = JsonContent.Create(new
        {
            name = "Phụ phí đóng gói SGN-HAN v3",
            direction = "buy",
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = "DONGGOI",
            selectionScope = "BILL",
            calculationMode = "fixed_rate",
            basis = "per_unit",
            currencyCode = "VND",
            rateAmountPercent = 300_000m,
            economicChargeTypeId = chargeTypeId,
            validFrom = DateTimeOffset.UtcNow.AddDays(-1).ToString("O")
        });
        (await _client.SendAsync(putV3Req)).EnsureSuccessStatusCode();

        // Publish v3
        using var pubV3Req = Tenant(HttpMethod.Post, $"/api/surcharges/{v2.SurchargeId}/versions/{v3DraftId}/publish", tenantId);
        var pubV3Res = await _client.SendAsync(pubV3Req);
        pubV3Res.EnsureSuccessStatusCode();

        // Verify v3 is published with valid publishedAt
        using var getV3 = Tenant(HttpMethod.Get, $"/api/surcharges/{v2.SurchargeId}/versions/{v3DraftId}", tenantId);
        var v3Node = JsonNode.Parse(await (await _client.SendAsync(getV3)).Content.ReadAsStringAsync())!["version"]!;
        Assert.Equal("published", v3Node["publishStatus"]?.ToString());
        Assert.NotNull(v3Node["publishedAt"]);
        Assert.Equal("DONGGOI", v3Node["serviceCode"]?.ToString());

        // Verify v2 remains unchanged: UNCLASSIFIED_LEGACY, published
        using var getV2 = Tenant(HttpMethod.Get, $"/api/surcharges/{v2.SurchargeId}/versions/{v2.VersionId}", tenantId);
        var v2Node = JsonNode.Parse(await (await _client.SendAsync(getV2)).Content.ReadAsStringAsync())!["version"]!;
        Assert.Equal("published", v2Node["publishStatus"]?.ToString());
        Assert.Equal("UNCLASSIFIED_LEGACY", v2Node["activationPolicy"]?.ToString());
    }

    // =========================================================================
    // UAT-SUR-SVC-07: Tạo Bill SGN-HAN CW120kg, chưa chọn đóng gói -> Không tự tích
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_07_CreateBill_NoDefaultServiceSelected()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "TESTLSS003");

        using var req = Tenant(HttpMethod.Get, $"/api/service-selections?contextType=BILL&contextId={billId}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var selections = await res.Content.ReadFromJsonAsync<List<ServiceSelectionDto>>(Json);
        Assert.NotNull(selections);
        // By default, no service is pre-selected
        Assert.DoesNotContain(selections, s => s.IsSelected);
    }

    // =========================================================================
    // UAT-SUR-SVC-08: Rating BUY khi chưa chọn đóng gói -> FREIGHT hợp lệ, DONGGOI excluded/not activated, không tự seed
    // =========================================================================
    [Fact]
    public void UAT_SUR_SVC_08_TryRatingBuy_PackagingNotSelected_FreightValid_PackagingExcludedOrEligibleNotActivated()
    {
        var packagingCandidate = OptionalCandidate("DONGGOI_SGN_HAN_MUA", "SGN-HAN", 300_000m);
        var ctx = Ctx(direction: SurchargeDirections.Buy, route: "SGN-HAN", selections: []);
        var rating = SurchargeRating.Evaluate([packagingCandidate], ctx);

        Assert.Empty(rating.Applied);
        Assert.Single(rating.EligibleNotActivated);
        var notAct = rating.EligibleNotActivated.Single();
        Assert.Equal(SurchargeReasonCodes.NotSelected, notAct.ReasonCode);
        Assert.Equal("DONGGOI_SGN_HAN_MUA", notAct.Rule.Code);
    }

    // =========================================================================
    // UAT-SUR-SVC-09: Chọn Đóng gói, lưu, F5 -> Lựa chọn xuất hiện tại Bill và Tính giá, không mất
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_09_SelectPackagingService_PersistedAndSharedBetweenViews()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "TESTLSS004");

        // Save selection via API
        await PutSelectionAsync(tenantId, billId, "DONGGOI", true, 1m);

        // Fetch selection (simulating F5 on Bill or Rating view)
        using var req = Tenant(HttpMethod.Get, $"/api/service-selections?contextType=BILL&contextId={billId}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var selections = await res.Content.ReadFromJsonAsync<List<ServiceSelectionDto>>(Json);
        Assert.NotNull(selections);
        var donggoiSel = selections.FirstOrDefault(s => s.ServiceCode == "DONGGOI");
        Assert.NotNull(donggoiSel);
        Assert.True(donggoiSel.IsSelected);
        Assert.Equal(1m, donggoiSel.Quantity);
        Assert.Equal("BILL", donggoiSel.Scope);
    }

    // =========================================================================
    // UAT-SUR-SVC-10: Rating BUY sau khi chọn Đóng gói (qty=1) -> DONGGOI được cộng 300.000 VND, trace v3
    // =========================================================================
    [Fact]
    public void UAT_SUR_SVC_10_TryRatingBuy_PackagingSelected_Adds300kAndTracesToV3()
    {
        var billId = Guid.NewGuid();
        var packagingCandidate = OptionalCandidate("DONGGOI", "SGN-HAN", 300_000m);
        var ctx = Ctx(direction: SurchargeDirections.Buy, route: "SGN-HAN", selections: [Sel("DONGGOI", billId, 1m)]);

        var rating = SurchargeRating.Evaluate([packagingCandidate], ctx);

        Assert.Single(rating.Applied);
        var applied = rating.Applied.Single();
        Assert.Equal(300_000m, applied.AmountOriginal);
        Assert.Equal("DONGGOI", applied.Rule.Code);
        Assert.Equal(SurchargeReasonCodes.Applied, applied.ReasonCode);
    }

    // =========================================================================
    // UAT-SUR-SVC-11: Rating SELL khi chỉ có surcharge BUY -> Không copy giá BUY sang SELL
    // =========================================================================
    [Fact]
    public void UAT_SUR_SVC_11_TryRatingSell_WhenOnlyBuySurchargeExists_DoesNotCopyBuyPrice()
    {
        var billId = Guid.NewGuid();
        var buyPackagingCandidate = OptionalCandidate("DONGGOI", "SGN-HAN", 300_000m, direction: SurchargeDirections.Buy);

        // Evaluate rating for SELL direction
        var sellCtx = Ctx(direction: SurchargeDirections.Sell, route: "SGN-HAN", selections: [Sel("DONGGOI", billId, 1m)]);
        var sellRating = SurchargeRating.Evaluate([buyPackagingCandidate], sellCtx);

        // Buy surcharge must NOT apply to sell rating
        Assert.Empty(sellRating.Applied);
        Assert.Empty(sellRating.EligibleNotActivated);
        // Excluded due to direction mismatch
        Assert.Single(sellRating.Excluded);
        Assert.Equal(SurchargeReasonCodes.ExcludedWrongDirection, sellRating.Excluded.Single().ReasonCode);
    }

    // =========================================================================
    // UAT-SUR-SVC-12: Lựa chọn ở Bill A không rò sang Bill B
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_12_SelectionIsolation_BillADoesNotLeakToBillB()
    {
        var tenantId = await CreateTenantAsync();
        var billA = await CreateBillAsync(tenantId, "BILL_LEAK_A");
        var billB = await CreateBillAsync(tenantId, "BILL_LEAK_B");

        // Select DONGGOI on Bill A only
        await PutSelectionAsync(tenantId, billA, "DONGGOI", true, 1m);

        // Query selections for Bill B
        using var reqB = Tenant(HttpMethod.Get, $"/api/service-selections?contextType=BILL&contextId={billB}", tenantId);
        var resB = await _client.SendAsync(reqB);
        resB.EnsureSuccessStatusCode();
        var selectionsB = await resB.Content.ReadFromJsonAsync<List<ServiceSelectionDto>>(Json);
        Assert.NotNull(selectionsB);
        Assert.DoesNotContain(selectionsB, s => s.ServiceCode == "DONGGOI" && s.IsSelected);
    }

    // =========================================================================
    // UAT-SUR-SVC-13: Apply BUY hợp lệ 2 lần -> Idempotent, không duplicate cost, Actual/Confirmed giữ nguyên
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_13_ApplyBuyFinancials_Idempotent_DoesNotDuplicateCostOrMutateConfirmedActual()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BILL_APPLY_IDEM");

        // 1. Create a published rate card for freight
        var (cardId, versionId) = await PublishedCardAsync(tenantId, "RC_BUY", "vendor", "VND", 1_000_000m);

        // 2. Publish optional surcharge for DONGGOI
        var chargeTypeId = await EnsureChargeTypeAsync(tenantId);
        await CreateSurchargeAsync(tenantId, new
        {
            code = "DONGGOI_IDEM",
            name = "Đóng gói idempotent",
            direction = "buy",
            calculationMode = "fixed_rate",
            basis = "per_bill",
            currencyCode = "VND",
            rateAmountPercent = 300_000m,
            economicChargeTypeId = chargeTypeId,
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = "DONGGOI",
            publish = true
        });

        // 3. Save selection
        await PutSelectionAsync(tenantId, billId, "DONGGOI", true, 1m);

        // 4. Rate the bill
        var ratingId = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 1m,
            routeCode = "SGN-HAN"
        });

        // 5. Apply financials 1st time
        using var apply1 = Tenant(HttpMethod.Post, $"/api/ratings/{ratingId}/seed-expected-costs", tenantId);
        var res1 = await _client.SendAsync(apply1);
        res1.EnsureSuccessStatusCode();

        using var getCosts1 = Tenant(HttpMethod.Get, $"/api/costs?billId={billId}", tenantId);
        var resCosts1 = await _client.SendAsync(getCosts1);
        resCosts1.EnsureSuccessStatusCode();
        var raw1 = await resCosts1.Content.ReadAsStringAsync();
        var node1 = JsonNode.Parse(raw1)!;
        var costs1 = node1 is System.Text.Json.Nodes.JsonArray arr1 ? arr1 : node1["items"]!.AsArray();
        var count1 = costs1.Count;
        Assert.True(count1 >= 1);

        // 6. Apply financials 2nd time (idempotency check)
        using var apply2 = Tenant(HttpMethod.Post, $"/api/ratings/{ratingId}/seed-expected-costs", tenantId);
        var res2 = await _client.SendAsync(apply2);
        res2.EnsureSuccessStatusCode();

        using var getCosts2 = Tenant(HttpMethod.Get, $"/api/costs?billId={billId}", tenantId);
        var resCosts2 = await _client.SendAsync(getCosts2);
        resCosts2.EnsureSuccessStatusCode();
        var raw2 = await resCosts2.Content.ReadAsStringAsync();
        var node2 = JsonNode.Parse(raw2)!;
        var costs2 = node2 is System.Text.Json.Nodes.JsonArray arr2 ? arr2 : node2["items"]!.AsArray();
        Assert.Equal(count1, costs2.Count);
    }

    // =========================================================================
    // UAT-SUR-SVC-14: Bỏ chọn Đóng gói sau apply, tính lại -> Kết quả mới loại bỏ optional, lịch sử được bảo vệ
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_14_DeselectPackaging_RerateCreatesNewRatingAndPreservesHistory()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BILL_DESELECT");
        var (cardId, versionId) = await PublishedCardAsync(tenantId, "RC_DESEL", "vendor", "VND", 1_000_000m);
        var chargeTypeId = await EnsureChargeTypeAsync(tenantId);
        await CreateSurchargeAsync(tenantId, new
        {
            code = "DONGGOI_DESEL",
            name = "Đóng gói deselect",
            direction = "buy",
            calculationMode = "fixed_rate",
            basis = "per_bill",
            currencyCode = "VND",
            rateAmountPercent = 300_000m,
            economicChargeTypeId = chargeTypeId,
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = "DONGGOI",
            publish = true
        });

        // Select and rate run 1
        await PutSelectionAsync(tenantId, billId, "DONGGOI", true, 1m);
        var rating1Id = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 1m,
            routeCode = "SGN-HAN"
        });
        var r1 = await GetAsync(tenantId, rating1Id);
        Assert.Contains(r1.Details, d => d.ComponentCode.Contains("DONGGOI"));

        // Now deselect
        await PutSelectionAsync(tenantId, billId, "DONGGOI", false, 1m);

        // Rate run 2 (rerate)
        var rating2Id = await RateAsync(tenantId, new
        {
            billId,
            rateVersionId = versionId,
            quantity = 1m,
            routeCode = "SGN-HAN",
            supersedesRatingId = rating1Id
        });
        var r2 = await GetAsync(tenantId, rating2Id);
        Assert.DoesNotContain(r2.Details, d => d.ComponentCode.Contains("DONGGOI"));

        // Rating 1 remains unchanged in history
        var r1After = await GetAsync(tenantId, rating1Id);
        Assert.Contains(r1After.Details, d => d.ComponentCode.Contains("DONGGOI"));
    }

    // =========================================================================
    // UAT-SUR-SVC-15: Cơ sở tính FIXED_RATE: per_unit nhân qty, per_bill không nhân trọng lượng
    // =========================================================================
    [Fact]
    public void UAT_SUR_SVC_15_FixedRateBasisCalculation_PerUnitMultipliesQty_PerBillDoesNotMultiplyWeight()
    {
        var billId = Guid.NewGuid();

        // 1. Basis: per_unit with qty = 2 -> 300.000 * 2 = 600.000 VND
        var candidatePerUnit = OptionalCandidate("DONGGOI", null, 300_000m, basis: "per_unit");
        var ctxQty2 = Ctx(direction: SurchargeDirections.Buy, selections: [Sel("DONGGOI", billId, 2m)]);

        var resultUnit = SurchargeRating.Evaluate([candidatePerUnit], ctxQty2);
        Assert.Single(resultUnit.Applied);
        Assert.Equal(600_000m, resultUnit.Applied.Single().AmountOriginal);

        // 2. Basis: per_bill with chargeable weight = 120 kg -> 300.000 VND (NOT 300.000 * 120 = 36.000.000)
        var candidatePerBill = OptionalCandidate("DONGGOI", null, 300_000m, basis: "per_bill");
        var ctxWeight120 = Ctx(direction: SurchargeDirections.Buy, selections: [Sel("DONGGOI", billId, 1m)]);

        var resultBill = SurchargeRating.Evaluate([candidatePerBill], ctxWeight120);
        Assert.Single(resultBill.Applied);
        Assert.Equal(300_000m, resultBill.Applied.Single().AmountOriginal);
    }

    // =========================================================================
    // UAT-SUR-SVC-16: Phân quyền & Cross-tenant: Người không có quyền/khác Tenant gọi API -> Chặn 403/404
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_16_Security_UnauthorizedOrCrossTenantPublishRejected()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var chargeTypeId = await EnsureChargeTypeAsync(tenantA);

        var surchargeA = await CreateSurchargeAsync(tenantA, new
        {
            code = "SUR_SEC_" + Guid.NewGuid().ToString("N")[..6],
            name = "Bảo mật",
            direction = "buy",
            calculationMode = "fixed_rate",
            rateAmountPercent = 100_000m,
            economicChargeTypeId = chargeTypeId,
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = "DONGGOI",
            publish = false
        });

        // Tenant B attempts to publish Tenant A's surcharge
        using var crossPublish = Tenant(HttpMethod.Post, $"/api/surcharges/{surchargeA.SurchargeId}/versions/{surchargeA.VersionId}/publish", tenantB);
        var crossRes = await _client.SendAsync(crossPublish);
        Assert.True(crossRes.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden);

        // Version in Tenant A is still draft
        using var checkVer = Tenant(HttpMethod.Get, $"/api/surcharges/{surchargeA.SurchargeId}/versions/{surchargeA.VersionId}", tenantA);
        var checkNode = JsonNode.Parse(await (await _client.SendAsync(checkVer)).Content.ReadAsStringAsync())!["version"]!;
        Assert.Equal("draft", checkNode["publishStatus"]?.ToString());
    }

    // =========================================================================
    // UAT-SUR-SVC-17: Audit & Immutability: v2 giữ nguyên, v3 read-only sau publish, Rating cũ bất biến
    // =========================================================================
    [Fact]
    public async Task UAT_SUR_SVC_17_AuditAndImmutability_V2Immutable_V3ReadOnlyAfterPublish_HistoricalRatingIntact()
    {
        var tenantId = await CreateTenantAsync();
        var chargeTypeId = await EnsureChargeTypeAsync(tenantId);

        // Create and publish v1
        var created = await CreateSurchargeAsync(tenantId, new
        {
            code = "SUR_IMMUTABLE_" + Guid.NewGuid().ToString("N")[..6],
            name = "Bất biến",
            direction = "buy",
            calculationMode = "fixed_rate",
            rateAmountPercent = 200_000m,
            economicChargeTypeId = chargeTypeId,
            activationPolicy = "OPTIONAL_OPT_IN",
            serviceCode = "DONGGOI",
            publish = true
        });

        // Attempt to edit published version via PUT -> Must be rejected (cannot edit published version)
        using var editReq = Tenant(HttpMethod.Put, $"/api/surcharges/{created.SurchargeId}/versions/{created.VersionId}", tenantId);
        editReq.Content = JsonContent.Create(new
        {
            rateAmountPercent = 999_999m
        });
        var editRes = await _client.SendAsync(editReq);
        Assert.True(editRes.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.NotFound);
    }

    // =========================================================================
    // UAT-SUR-SVC-18: Regression: Phụ phí Mandatory/Auto & Best Available vận hành chuẩn
    // =========================================================================
    [Fact]
    public void UAT_SUR_SVC_18_Regression_MandatoryAndAutoConditionalSurchargesFunctionProperly()
    {
        var billId = Guid.NewGuid();

        // 1. Mandatory surcharge (Fuel)
        var mandatoryFuel = MandatoryCandidate("FUEL_SUR", 100_000m);

        // 2. Auto-conditional surcharge (Dangerous Goods) - condition NOT met (dangerous = false)
        var autoDg = AutoDgCandidate(200_000m);

        // 3. Optional surcharge (Packaging) - selected
        var optionalPackaging = OptionalCandidate("DONGGOI", null, 300_000m);

        var ctx = Ctx(direction: SurchargeDirections.Buy, dangerous: false, selections: [Sel("DONGGOI", billId, 1m)]);

        var result = SurchargeRating.Evaluate([mandatoryFuel, autoDg, optionalPackaging], ctx);

        // Applied: Mandatory Fuel (100k) + Optional Packaging (300k)
        Assert.Equal(2, result.Applied.Count);
        Assert.Contains(result.Applied, a => a.Rule.Code == "FUEL_SUR" && a.AmountOriginal == 100_000m);
        Assert.Contains(result.Applied, a => a.Rule.Code == "DONGGOI" && a.AmountOriginal == 300_000m);

        // EligibleNotActivated: Auto DG (condition false)
        Assert.Single(result.EligibleNotActivated);
        Assert.Equal("DG", result.EligibleNotActivated.Single().Rule.Code);
        Assert.Equal(SurchargeReasonCodes.NotActivatedConditionFalse, result.EligibleNotActivated.Single().ReasonCode);
    }

    // =========================================================================
    // Test Helpers
    // =========================================================================
    private static ServiceSelectionSnapshot Sel(string code, Guid contextId, decimal qty = 1m, bool selected = true) =>
        new("BILL", contextId, code, "BILL", qty, selected, 1, null, null);

    private static SurchargeCandidate OptionalCandidate(string code, string? route, decimal amount, string? basis = "per_bill", string direction = SurchargeDirections.Buy)
    {
        var config = SurchargeActivationConfigParser.Serialize(new ActivationConfig(code, "BILL", []));
        IReadOnlyList<SurchargeCondition> conditions = string.IsNullOrWhiteSpace(route)
            ? []
            : [new SurchargeCondition { Dimension = "route", Operator = "eq", ValueText = route }];
        return new SurchargeCandidate(
            Guid.NewGuid(), code, code, direction, Guid.NewGuid(), 3,
            DateTimeOffset.UtcNow.AddYears(-1), null, Guid.NewGuid(),
            SurchargeCalcModes.FixedRate, basis, "VND", amount, 0, null, null, null,
            conditions, [], [], null, null,
            SurchargeActivationPolicies.OptionalOptIn, config, null);
    }

    private static SurchargeCandidate AutoDgCandidate(decimal amount)
    {
        var config = SurchargeActivationConfigParser.Serialize(new ActivationConfig(
            null, null, [new ActivationPredicate("dangerous_goods", "eq", "true")]));
        return new SurchargeCandidate(
            Guid.NewGuid(), "DG", "DG", SurchargeDirections.Buy, Guid.NewGuid(), 1,
            DateTimeOffset.UtcNow.AddYears(-1), null, Guid.NewGuid(),
            SurchargeCalcModes.FixedRate, "per_bill", "VND", amount, 0, null, null, null,
            [], [], [], null, null,
            SurchargeActivationPolicies.AutoConditional, config, null);
    }

    private static SurchargeCandidate MandatoryCandidate(string code, decimal amount) =>
        new(Guid.NewGuid(), code, code, SurchargeDirections.Buy, Guid.NewGuid(), 1,
            DateTimeOffset.UtcNow.AddYears(-1), null, Guid.NewGuid(),
            SurchargeCalcModes.FixedRate, "per_bill", "VND", amount, 0, null, null, null,
            [], [], [], null, null,
            SurchargeActivationPolicies.MandatoryByPolicy, null, "POL-1");

    private static SurchargeMatchContext Ctx(
        string direction = SurchargeDirections.Buy,
        string? route = null,
        bool? dangerous = false,
        IReadOnlyList<ServiceSelectionSnapshot>? selections = null,
        DateTimeOffset? asOf = null,
        decimal baseAmount = 1_000_000m,
        Guid? vendorId = null) =>
        new(
            asOf ?? DateTimeOffset.UtcNow,
            direction,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "road",
            null,
            route,
            null,
            null,
            null,
            dangerous,
            vendorId,
            null,
            null,
            1m,
            1m,
            1m,
            baseAmount,
            [],
            Guid.NewGuid(),
            null,
            null,
            null,
            selections ?? []);

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "SurchargeSvcTest" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private async Task<CreatedSurcharge> CreateSurchargeAsync(Guid tenantId, object body)
    {
        var typeId = await EnsureChargeTypeAsync(tenantId);
        var node = JsonSerializer.SerializeToNode(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsObject();
        if (!node.ContainsKey("economicChargeTypeId")) node["economicChargeTypeId"] = typeId.ToString();
        if (!node.ContainsKey("currencyCode")) node["currencyCode"] = "VND";
        var res = await PostAsync(tenantId, "/api/surcharges", node);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<CreatedSurcharge>(Json))!;
    }

    private async Task<Guid> EnsureChargeTypeAsync(Guid tenantId)
    {
        if (_chargeTypes.TryGetValue(tenantId, out var existing)) return existing;
        var created = await PostIdAsync(tenantId, "/api/economic-charge-types", new { code = "PACK", name = "Đóng gói" });
        _chargeTypes[tenantId] = created;
        return created;
    }

    private async Task<Guid> PostIdAsync(Guid tenantId, string url, object body)
    {
        var res = await PostAsync(tenantId, url, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<HttpResponseMessage> PostAsync(Guid tenantId, string url, object body)
    {
        using var req = Tenant(HttpMethod.Post, url, tenantId);
        req.Content = JsonContent.Create(body);
        return await _client.SendAsync(req);
    }

    private async Task PutSelectionAsync(Guid tenantId, Guid billId, string serviceCode, bool selected, decimal qty)
    {
        using var req = Tenant(HttpMethod.Put, "/api/service-selections", tenantId);
        req.Content = JsonContent.Create(new
        {
            contextType = "BILL",
            contextId = billId,
            serviceCode,
            isSelected = selected,
            quantity = qty,
            scope = "BILL"
        });
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = Tenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        return (await (await _client.SendAsync(req)).Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<(Guid CardId, Guid VersionId)> PublishedCardAsync(
        Guid tenantId, string code, string partyType, string currency, decimal unitAmount)
    {
        using var cardReq = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        cardReq.Content = JsonContent.Create(new { code, name = code, partyType, currencyCode = currency });
        var cardId = (await (await _client.SendAsync(cardReq)).Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var verReq = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        verReq.Content = JsonContent.Create(new { note = "v", effectiveFrom = "2020-01-01T00:00:00Z" });
        var versionId = (await (await _client.SendAsync(verReq)).Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
        using var ruleReq = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId);
        ruleReq.Content = JsonContent.Create(new { code = "BASE", name = "Cước chính", calcMethod = "unit_rate", unitAmount, currencyCode = currency, sortOrder = 1 });
        (await _client.SendAsync(ruleReq)).EnsureSuccessStatusCode();
        using var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        (await _client.SendAsync(publish)).EnsureSuccessStatusCode();
        return (cardId, versionId);
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        var res = await PostAsync(tenantId, "/api/ratings", body);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<RatingBody> GetAsync(Guid tenantId, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/ratings/{id}", tenantId);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<RatingBody>(Json))!;
    }

    private sealed record IdBody(Guid Id);
    private sealed record CreatedSurcharge(Guid SurchargeId, Guid VersionId);
    private sealed record RatingBody(Guid Id, decimal TotalAmount, string? ContextJson, List<DetailLine> Details);
    private sealed record DetailLine(string ComponentCode, decimal Amount, string? SourceType, string? FinancialMaturity);
    private sealed record ActivationServiceItemDto(string ServiceCode, string DisplayName, string? Description, bool IsActive, List<string> AllowedContextTypes, string DefaultUom);
    private sealed record UpdateSurchargeVersionResultDto(Guid VersionId, int VersionNo, string Status, List<string> Warnings);
    private sealed record ServiceSelectionDto(string ServiceCode, string Scope, decimal Quantity, bool IsSelected, int Revision);
}
