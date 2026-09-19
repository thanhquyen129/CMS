using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LCMS.Api.Tests;

/// <summary>ADR-0018: capture a VNPost-class waybill as Bill + Expected postage costs.</summary>
[Collection("Api")]
public sealed class BillWaybillApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public BillWaybillApiTests(LcmsApiFactory factory)
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
    public async Task CaptureVnPostWaybill_SeedsExpectedCosts_AndIsTenantIsolated()
    {
        var tenantA = await CreateTenantAsync("TN-WB-A", "Waybill A");
        var tenantB = await CreateTenantAsync("TN-WB-B", "Waybill B");

        using var create = WithTenant(HttpMethod.Post, "/api/bills/waybills", tenantA, VnPostBody("EE5556576340VN"));
        var createdRes = await _client.SendAsync(create);
        var createdText = await createdRes.Content.ReadAsStringAsync();
        Assert.True(createdRes.StatusCode == HttpStatusCode.Created, createdText);
        var created = JsonSerializer.Deserialize<IdResponse>(createdText, JsonOptions);
        Assert.NotNull(created);

        using var getA = WithTenant(HttpMethod.Get, $"/api/bills/{created!.Id}/waybill", tenantA);
        var waybill = await (await _client.SendAsync(getA)).Content.ReadFromJsonAsync<WaybillResponse>(JsonOptions);
        Assert.NotNull(waybill);
        Assert.Equal("EE5556576340VN", waybill!.BillNo);
        Assert.Equal("NGUYỄN THANH QUYỀN", waybill.Sender.Name);
        Assert.Equal("TRẦN THỊ LỢI", waybill.Consignee.Name);
        Assert.Equal("goods", waybill.PackageKind);
        Assert.Equal(1.275m, waybill.ActualWeightKg);
        Assert.False(waybill.Charges.AmountsRedacted);
        Assert.Equal(42000m, waybill.Charges.BasePostage);
        Assert.Equal(10500m, waybill.Charges.Surcharge);
        Assert.Equal(56700m, waybill.Charges.GrandTotal);
        Assert.Equal(0m, waybill.Charges.CodCollectAmount);

        using var costsReq = WithTenant(HttpMethod.Get, $"/api/costs?billId={created.Id}", tenantA);
        var costs = await (await _client.SendAsync(costsReq)).Content
            .ReadFromJsonAsync<List<CostRow>>(JsonOptions);
        Assert.NotNull(costs);
        Assert.Equal(3, costs!.Count);
        Assert.Equal(56700m, costs.Sum(c => c.Amount));
        Assert.All(costs, c => Assert.Equal("expected", c.FinancialMaturity));
        Assert.Contains(costs, c => c.CostTypeCode == "postage.base" && c.Amount == 42000m);
        Assert.Contains(costs, c => c.CostTypeCode == "postage.surcharge" && c.Amount == 10500m);
        Assert.Contains(costs, c => c.CostTypeCode == "postage.other" && c.Amount == 4200m);

        using var fv = WithTenant(HttpMethod.Get, $"/api/bills/{created.Id}/financial-view", tenantA);
        var view = await (await _client.SendAsync(fv)).Content.ReadFromJsonAsync<FinancialViewRow>(JsonOptions);
        Assert.NotNull(view);
        Assert.Equal("NGUYỄN THANH QUYỀN", view!.Bill.CustomerName);
        Assert.NotNull(view.Waybill);
        Assert.Single(view.Graph.Shipments);
        Assert.Equal("EE5556576340VN", view.Graph.Shipments[0].ShipmentNo);

        using var getB = WithTenant(HttpMethod.Get, $"/api/bills/{created.Id}/waybill", tenantB);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(getB)).StatusCode);

        using var search = WithTenant(HttpMethod.Get, "/api/bills?q=51427", tenantA);
        var listed = await (await _client.SendAsync(search)).Content
            .ReadFromJsonAsync<List<BillListRow>>(JsonOptions);
        Assert.Contains(listed!, b => b.Id == created.Id);
    }

    [Fact]
    public async Task CaptureWaybill_MismatchedTotals_Returns400()
    {
        var tenantId = await CreateTenantAsync("TN-WB-VAL", "Waybill val");
        var body = VnPostBody("EE-BAD-TOTAL") with { GrandTotal = 99999m };

        using var req = WithTenant(HttpMethod.Post, "/api/bills/waybills", tenantId, body);
        var res = await _client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task CaptureWaybill_DuplicateBillNo_Returns409()
    {
        var tenantId = await CreateTenantAsync("TN-WB-DUP", "Waybill dup");
        using var first = WithTenant(HttpMethod.Post, "/api/bills/waybills", tenantId, VnPostBody("EE-DUP-1"));
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(first)).StatusCode);

        using var second = WithTenant(HttpMethod.Post, "/api/bills/waybills", tenantId, VnPostBody("EE-DUP-1"));
        Assert.Equal(HttpStatusCode.Conflict, (await _client.SendAsync(second)).StatusCode);
    }

    [Fact]
    public async Task CaptureWaybill_WithoutTenant_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/bills/waybills", VnPostBody("EE-NO-TENANT"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task UpsertWaybill_OnExistingBill_SeedsCosts()
    {
        var tenantId = await CreateTenantAsync("TN-WB-UP", "Waybill upsert");
        using var createBill = WithTenant(HttpMethod.Post, "/api/bills", tenantId, new
        {
            billNo = "BL-THIN-1",
            billType = "freight"
        });
        var bill = await (await _client.SendAsync(createBill)).Content.ReadFromJsonAsync<IdResponse>(JsonOptions);

        var body = VnPostBody("BL-THIN-1");
        using var put = WithTenant(HttpMethod.Put, $"/api/bills/{bill!.Id}/waybill", tenantId, body);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(put)).StatusCode);

        using var get = WithTenant(HttpMethod.Get, $"/api/bills/{bill.Id}/waybill", tenantId);
        var waybill = await (await _client.SendAsync(get)).Content.ReadFromJsonAsync<WaybillResponse>(JsonOptions);
        Assert.Equal("NGUYỄN THANH QUYỀN", waybill!.Sender.Name);

        using var costsReq = WithTenant(HttpMethod.Get, $"/api/costs?billId={bill.Id}", tenantId);
        var costs = await (await _client.SendAsync(costsReq)).Content.ReadFromJsonAsync<List<CostRow>>(JsonOptions);
        Assert.Equal(3, costs!.Count);
    }

    private static WaybillPostBody VnPostBody(string billNo) => new(
        billNo,
        "parcel",
        "vnpost",
        "Vietnam Post",
        "BD1",
        new WaybillPartyPost("NGUYỄN THANH QUYỀN", "0905776691", "KCN - P. Điện Bàn Đông - TP. Đà Nẵng", "51427", "51427", null),
        new WaybillPartyPost("TRẦN THỊ LỢI", "0360000000", "Thôn Tường An", "55356", "55356", "55356"),
        "goods",
        "ÁO QUẦN",
        11,
        0,
        "2026-08-17T00:00:00+07:00",
        1,
        1.275m,
        1m,
        42000m,
        0m,
        10500m,
        0m,
        0m,
        56700m,
        0m,
        56700m,
        "VND",
        "sender",
        "cost",
        0m,
        "2026-08-17T00:00:00+07:00",
        "TRẦN THỊ Ý NHI",
        true);

    private sealed record WaybillPartyPost(
        string Name,
        string? Phone,
        string? Address,
        string? CustomerCode,
        string? PostalCode,
        string? DeliveryCode);

    private sealed record WaybillPostBody(
        string BillNo,
        string BillType,
        string SourceSystem,
        string CarrierName,
        string ItemFormCode,
        WaybillPartyPost Sender,
        WaybillPartyPost Consignee,
        string PackageKind,
        string ContentsDescription,
        int ContentsQuantity,
        decimal DeclaredValue,
        string SentAt,
        int ParcelCount,
        decimal ActualWeightKg,
        decimal ChargeableWeightKg,
        decimal BasePostage,
        decimal VatPostage,
        decimal Surcharge,
        decimal CodFee,
        decimal OtherFee,
        decimal TotalPostageInclVat,
        decimal TotalCollect,
        decimal GrandTotal,
        string CurrencyCode,
        string PostagePayer,
        string ChargeEconomicRole,
        decimal CodCollectAmount,
        string AcceptedAt,
        string AcceptedBy,
        bool SenderCommitAccepted);

    private async Task<Guid> CreateTenantAsync(string code, string name)
    {
        var response = await _client.PostAsJsonAsync("/api/tenants", new { code, name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOptions);
        return body!.Id;
    }

    private static HttpRequestMessage WithTenant(HttpMethod method, string url, Guid tenantId, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (body is not null)
        {
            req.Content = JsonContent.Create(body);
        }

        return req;
    }

    private sealed record IdResponse(Guid Id);

    private sealed record WaybillResponse(
        Guid Id,
        Guid BillId,
        string BillNo,
        PartyRow Sender,
        PartyRow Consignee,
        string PackageKind,
        decimal? ActualWeightKg,
        ChargesRow Charges);

    private sealed record PartyRow(string? Name);

    private sealed record ChargesRow(
        decimal BasePostage,
        decimal Surcharge,
        decimal GrandTotal,
        decimal CodCollectAmount,
        bool AmountsRedacted);

    private sealed record CostRow(decimal Amount, string FinancialMaturity, string? CostTypeCode);

    private sealed record FinancialViewRow(BillRow Bill, GraphRow Graph, WaybillResponse? Waybill);

    private sealed record BillRow(string? CustomerName);

    private sealed record GraphRow(List<ShipmentRow> Shipments);

    private sealed record ShipmentRow(string ShipmentNo);

    private sealed record BillListRow(Guid Id, string BillNo);
}
