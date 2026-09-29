using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

/// <summary>ADR-0039 — Operational reference edit, rating context readiness, stale + rerate (AC-01..AC-12).</summary>
[Collection("Api")]
public sealed class OperationalReferenceEditRatingContextTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public OperationalReferenceEditRatingContextTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task MissingChargeable_IsNeverZero_ReadinessBlocks_AndNoRatingIsCreated()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-CW-MISS");
        await EditAsync(tenantId, "bill", billId, new { transport_mode = "air", gross_weight_kg = "85" });

        var edit = await GetEditAsync(tenantId, "bill", billId);
        Assert.Equal("missing", edit.Chargeable.State);
        Assert.Null(edit.Chargeable.Value);
        Assert.False(edit.Chargeable.CanConfirm);
        Assert.False(string.IsNullOrWhiteSpace(edit.Chargeable.MissingReason));

        var versionId = await PublishedUnitRateAsync(tenantId, "RC-MISS");
        using var readiness = Tenant(HttpMethod.Post, "/api/ratings/readiness", tenantId);
        readiness.Content = JsonContent.Create(new { billId, rateVersionId = versionId });
        var ready = await ReadAsync<ReadinessBody>(await _client.SendAsync(readiness));
        Assert.False(ready.Ready);
        var item = Assert.Single(ready.Missing);
        Assert.Equal("chargeable_weight_kg", item.Field);
        Assert.Contains("KG", item.RuleCodes);
        Assert.Equal("edit_bill", item.Action);

        using var rate = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        rate.Content = JsonContent.Create(new { billId, rateVersionId = versionId });
        var blocked = await _client.SendAsync(rate);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var err = await ReadAsync<NotReadyBody>(blocked, ensure: false);
        Assert.Equal("rating_not_ready", err.Code);
        Assert.Contains(err.Missing, m => m.Field == "chargeable_weight_kg");
        Assert.Empty(await HistoryAsync(tenantId, billId));
    }

    [Fact]
    public async Task SystemChargeable_NeedsGrossAndVolume_ConfirmIsNotOverride_AndRatingUsesIt()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-CW-SYS");
        await EditAsync(tenantId, "bill", billId, new { transport_mode = "air", gross_weight_kg = "85", volume_cbm = "0.9" });

        var edit = await GetEditAsync(tenantId, "bill", billId);
        Assert.Equal("system", edit.Chargeable.State);
        Assert.Equal(150.3m, edit.Chargeable.Value);
        Assert.Equal("Hệ thống tính", edit.Chargeable.SourceLabel);
        Assert.True(edit.Chargeable.CanConfirm);

        using var confirm = Tenant(HttpMethod.Post, $"/api/operational-references/bill/{billId}/chargeable-weight/confirm", tenantId);
        var confirmed = await ReadAsync<ChargeableBody>(await _client.SendAsync(confirm));
        Assert.True(confirmed.IsConfirmed);
        Assert.Null(confirmed.OverrideReason);

        var versionId = await PublishedUnitRateAsync(tenantId, "RC-SYS");
        var ratingId = await RateAsync(tenantId, new { billId, rateVersionId = versionId });
        var item = Assert.Single(await HistoryAsync(tenantId, billId));
        Assert.Equal(ratingId, item.Id);
        Assert.Equal(150.3m, item.ChargeableWeightKg);
        Assert.Equal("confirmed", item.ChargeableBasis);
        Assert.Equal(300.6m, item.TotalAmount);
    }

    [Fact]
    public async Task ContextChange_MarksOnlyUsedRatingsStale_KeepsAmounts_AndRerateAddsHistory()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-RERATE");
        await EditAsync(tenantId, "bill", billId, new { transport_mode = "air", gross_weight_kg = "85", volume_cbm = "0.9" });
        var versionId = await PublishedUnitRateAsync(tenantId, "RC-RERATE");
        var firstId = await RateAsync(tenantId, new { billId, rateVersionId = versionId });

        var unrelated = await EditAsync(tenantId, "bill", billId, new { route_code = "HAN-SGN" });
        Assert.Equal(0, unrelated.RatingsMarkedStale);
        Assert.Null(Assert.Single(await HistoryAsync(tenantId, billId)).StaleAt);

        var related = await EditAsync(tenantId, "bill", billId, new { volume_cbm = "1.2" });
        Assert.Equal(1, related.RatingsMarkedStale);
        Assert.Contains("chargeable_weight_kg", related.ChangedFields);
        var stale = Assert.Single(await HistoryAsync(tenantId, billId));
        Assert.NotNull(stale.StaleAt);
        Assert.False(string.IsNullOrWhiteSpace(stale.StaleReason));
        Assert.Equal(300.6m, stale.TotalAmount);

        var editView = await GetEditAsync(tenantId, "bill", billId);
        Assert.Contains(editView.CurrentRatings, r => r.RatingId == firstId && r.Stale);

        var secondId = await RateAsync(tenantId, new { billId, rateVersionId = versionId, supersedesRatingId = firstId });
        var history = await HistoryAsync(tenantId, billId);
        Assert.Equal(2, history.Count);
        var old = history.Single(h => h.Id == firstId);
        var fresh = history.Single(h => h.Id == secondId);
        Assert.Equal("superseded", old.Status);
        Assert.Equal(300.6m, old.TotalAmount);
        Assert.Null(fresh.StaleAt);
        Assert.Equal(200.4m, fresh.ChargeableWeightKg);
        Assert.Equal(400.8m, fresh.TotalAmount);
        Assert.Equal(firstId, fresh.SupersedesRatingId);
    }

    [Fact]
    public async Task ChargeableOverride_NeedsReason_AndCanBeCleared()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-CW-OVR");
        await EditAsync(tenantId, "bill", billId, new { transport_mode = "air", gross_weight_kg = "85", volume_cbm = "0.9" });
        using (var confirm = Tenant(HttpMethod.Post, $"/api/operational-references/bill/{billId}/chargeable-weight/confirm", tenantId))
        {
            (await _client.SendAsync(confirm)).EnsureSuccessStatusCode();
        }

        using var noReason = Patch(tenantId, "bill", billId, new { changes = new { chargeable_weight_kg = "160" } });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(noReason)).StatusCode);

        await EditAsync(tenantId, "bill", billId, new { chargeable_weight_kg = "160" }, "Khách chốt 160 kg theo booking");
        var edit = await GetEditAsync(tenantId, "bill", billId);
        Assert.Equal("override", edit.Chargeable.State);
        Assert.Equal(160m, edit.Chargeable.Value);
        Assert.Equal("Khách chốt 160 kg theo booking", edit.Chargeable.OverrideReason);

        using var revert = Patch(tenantId, "bill", billId, new { revertFields = new[] { "chargeable_weight_kg" }, reason = "Bỏ ghi đè" });
        (await _client.SendAsync(revert)).EnsureSuccessStatusCode();
        var cleared = await GetEditAsync(tenantId, "bill", billId);
        Assert.NotEqual("override", cleared.Chargeable.State);
        Assert.Equal(150.3m, cleared.Chargeable.Value);
    }

    [Fact]
    public async Task ImportOwnedField_NeedsReason_AndResyncDoesNotEraseOverride()
    {
        var tenantId = await CreateTenantAsync();
        await ImportBillAsync(tenantId, "HAN");
        var billId = (await ReadAsync<List<BillListBody>>(await _client.SendAsync(Tenant(HttpMethod.Get, "/api/bills?q=BL-SOT", tenantId))))
            .Single(b => b.BillNo == "BL-SOT").Id;

        var before = await GetEditAsync(tenantId, "bill", billId);
        var origin = before.Fields.Single(f => f.Code == "origin_code");
        Assert.True(origin.RequiresReason);
        Assert.NotEqual("manual", origin.Source);

        using var noReason = Patch(tenantId, "bill", billId, new { changes = new { origin_code = "HPH" } });
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(noReason)).StatusCode);

        await EditAsync(tenantId, "bill", billId, new { origin_code = "HPH" }, "Điều vận xác nhận đổi cảng đi");
        await ImportBillAsync(tenantId, "DAD");

        var after = await GetEditAsync(tenantId, "bill", billId);
        var overridden = after.Fields.Single(f => f.Code == "origin_code");
        Assert.Equal("HPH", overridden.Value);
        Assert.Equal("override", overridden.Source);
        Assert.Equal("DAD", overridden.SourceValue);
        Assert.Equal("Điều vận xác nhận đổi cảng đi", overridden.OverrideReason);
    }

    [Fact]
    public async Task LinkedOrderEdit_MarksLinkedBillRatingStale_AndOtherTenantGets404()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-ORD-LINK");
        await EditAsync(tenantId, "bill", billId, new { transport_mode = "air", gross_weight_kg = "85", volume_cbm = "0.9" });
        var versionId = await PublishedUnitRateAsync(tenantId, "RC-ORD");
        await RateAsync(tenantId, new { billId, rateVersionId = versionId });

        using var order = Tenant(HttpMethod.Put, "/api/orders", tenantId);
        order.Content = JsonContent.Create(new { orderNo = "ORD-LINK", sourceSystem = "lcms_manual", externalId = "ORD-LINK", transportMode = "air" });
        var orderId = (await ReadAsync<IdBody>(await _client.SendAsync(order))).Id;
        using var link = Tenant(HttpMethod.Post, $"/api/orders/{orderId}/bills/{billId}", tenantId);
        (await _client.SendAsync(link)).EnsureSuccessStatusCode();

        var result = await EditAsync(tenantId, "order", orderId, new { transport_mode = "sea" });
        Assert.Equal(1, result.RatingsMarkedStale);
        Assert.NotNull(Assert.Single(await HistoryAsync(tenantId, billId)).StaleAt);

        var otherTenant = await CreateTenantAsync();
        using var foreign = Tenant(HttpMethod.Get, $"/api/operational-references/bill/{billId}/edit", otherTenant);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(foreign)).StatusCode);
        using var foreignPatch = Patch(otherTenant, "bill", billId, new { changes = new { route_code = "X" } });
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(foreignPatch)).StatusCode);
    }

    [Fact]
    public async Task InvalidValues_AreRejected_AndEtdAfterEtaIsBlocked()
    {
        var tenantId = await CreateTenantAsync();
        var billId = await CreateBillAsync(tenantId, "BL-VALID");

        using var negative = Patch(tenantId, "bill", billId, new { changes = new { gross_weight_kg = "-5" } });
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(negative)).StatusCode);

        using var schedule = Patch(tenantId, "bill", billId, new
        {
            changes = new { etd_at = "2026-10-05T00:00:00Z", eta_at = "2026-10-01T00:00:00Z" }
        });
        var status = (await _client.SendAsync(schedule)).StatusCode;
        Assert.True(status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
    }

    private async Task ImportBillAsync(Guid tenantId, string origin)
    {
        using var req = Tenant(HttpMethod.Post, "/api/operational-import/commit", tenantId);
        req.Content = JsonContent.Create(new
        {
            sourceSystem = "tms",
            rows = new[] { new { objectType = "bill", businessNo = "BL-SOT", externalId = "EXT-SOT", originCode = origin, destinationCode = "SGN" } }
        });
        (await _client.SendAsync(req)).EnsureSuccessStatusCode();
    }

    private async Task<Guid> PublishedUnitRateAsync(Guid tenantId, string code)
    {
        using var cardReq = Tenant(HttpMethod.Post, "/api/rate-cards", tenantId);
        cardReq.Content = JsonContent.Create(new { code, name = code, partyType = "vendor", currencyCode = "USD" });
        var cardId = (await ReadAsync<IdBody>(await _client.SendAsync(cardReq))).Id;
        using var versionReq = Tenant(HttpMethod.Post, $"/api/rate-cards/{cardId}/versions", tenantId);
        versionReq.Content = JsonContent.Create(new { note = code });
        var versionId = (await ReadAsync<IdBody>(await _client.SendAsync(versionReq))).Id;
        using var rule = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/rules", tenantId);
        rule.Content = JsonContent.Create(new
        {
            code = "KG",
            name = "Theo kg",
            calcMethod = "unit_rate",
            unitAmount = 2,
            currencyCode = "USD",
            volumetricFactor = 167,
            sortOrder = 1
        });
        (await _client.SendAsync(rule)).EnsureSuccessStatusCode();
        using var publish = Tenant(HttpMethod.Post, $"/api/rate-versions/{versionId}/publish", tenantId);
        (await _client.SendAsync(publish)).EnsureSuccessStatusCode();
        return versionId;
    }

    private async Task<EditResultBody> EditAsync(Guid tenantId, string type, Guid id, object changes, string? reason = null)
    {
        using var req = Patch(tenantId, type, id, new { changes, reason });
        return await ReadAsync<EditResultBody>(await _client.SendAsync(req));
    }

    private HttpRequestMessage Patch(Guid tenantId, string type, Guid id, object body)
    {
        var req = Tenant(HttpMethod.Patch, $"/api/operational-references/{type}/{id}", tenantId);
        req.Content = JsonContent.Create(body);
        return req;
    }

    private async Task<EditBody> GetEditAsync(Guid tenantId, string type, Guid id)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/operational-references/{type}/{id}/edit", tenantId);
        return await ReadAsync<EditBody>(await _client.SendAsync(req));
    }

    private async Task<List<HistoryBody>> HistoryAsync(Guid tenantId, Guid billId)
    {
        using var req = Tenant(HttpMethod.Get, $"/api/bills/{billId}/ratings", tenantId);
        return await ReadAsync<List<HistoryBody>>(await _client.SendAsync(req));
    }

    private async Task<Guid> RateAsync(Guid tenantId, object body)
    {
        using var req = Tenant(HttpMethod.Post, "/api/ratings", tenantId);
        req.Content = JsonContent.Create(body);
        return (await ReadAsync<IdBody>(await _client.SendAsync(req))).Id;
    }

    private async Task<Guid> CreateBillAsync(Guid tenantId, string billNo)
    {
        using var req = Tenant(HttpMethod.Post, "/api/bills", tenantId);
        req.Content = JsonContent.Create(new { billNo, billType = "house", sourceSystem = "lcms_manual", externalId = billNo });
        return (await ReadAsync<IdBody>(await _client.SendAsync(req))).Id;
    }

    private async Task<Guid> CreateTenantAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code = "TN-" + Guid.NewGuid().ToString("N")[..8], name = "OpRef" });
        return (await ReadAsync<IdBody>(response)).Id;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage res, bool ensure = true)
    {
        if (ensure && !res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        }

        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static HttpRequestMessage Tenant(HttpMethod method, string url, Guid tenantId)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        return req;
    }

    private sealed record IdBody(Guid Id);
    private sealed record BillListBody(Guid Id, string BillNo);
    private sealed record EditResultBody(List<string> ChangedFields, int RatingsMarkedStale, string? RowVersion);
    private sealed record ChargeableBody(
        string State,
        decimal? Value,
        string? SourceLabel,
        bool IsConfirmed,
        string? OverrideReason,
        bool CanConfirm,
        string? MissingReason);
    private sealed record FieldBody(string Code, string? Value, string Source, bool RequiresReason, string? SourceValue, string? OverrideReason);
    private sealed record RatingStatusBody(Guid RatingId, bool Stale);
    private sealed record EditBody(List<FieldBody> Fields, ChargeableBody Chargeable, List<RatingStatusBody> CurrentRatings);
    private sealed record MissingBody(string Field, List<string> RuleCodes, string Action);
    private sealed record ReadinessBody(bool Ready, List<MissingBody> Missing);
    private sealed record NotReadyBody(string Code, List<MissingBody> Missing);
    private sealed record HistoryBody(
        Guid Id,
        decimal TotalAmount,
        string Status,
        Guid? SupersedesRatingId,
        decimal? ChargeableWeightKg,
        string? ChargeableBasis,
        DateTimeOffset? StaleAt,
        string? StaleReason);
}
