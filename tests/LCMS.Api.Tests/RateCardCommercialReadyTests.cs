using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Api.Tests;

[Collection("Api")]
public sealed class RateCardCommercialReadyTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly LcmsApiFactory _factory;
    private HttpClient _client = null!;

    public RateCardCommercialReadyTests(LcmsApiFactory factory) => _factory = factory;

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
    public async Task Dual_payload_returns_vietnamese_code_and_correlation()
    {
        var tenantId = await CreateTenantAsync("TN-CR-DUAL");
        var routeId = await CreateRouteAsync(tenantId, "VN-MY", true);
        using var req = Card(HttpMethod.Post, "/api/rate-cards", tenantId, "corr-dual-01");
        req.Content = JsonContent.Create(BasicBody("RC-DUAL", routeId, rules: true, matrix: true));
        var res = await _client.SendAsync(req);
        var problem = await ReadProblem(res);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("invalid_declaration_mode", problem.Code);
        Assert.Equal(RateDeclarationPolicy.DualPayloadMessage, problem.Message);
        Assert.Equal("corr-dual-01", problem.CorrelationId);
        Assert.DoesNotContain("Exception", problem.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_currency_and_negative_amount_stay_vietnamese()
    {
        var tenantId = await CreateTenantAsync("TN-CR-CUR");
        var routeId = await CreateRouteAsync(tenantId, "SGN-HAN", true);
        using var currency = Card(HttpMethod.Post, "/api/rate-cards", tenantId, "corr-cur-01");
        currency.Content = JsonContent.Create(BasicBody("RC-ZZZ", routeId, rules: true) with { CurrencyCode = "ZZZ" });
        var currencyRes = await _client.SendAsync(currency);
        var currencyProblem = await ReadProblem(currencyRes);
        Assert.Equal(HttpStatusCode.Conflict, currencyRes.StatusCode);
        Assert.Equal("invalid_currency", currencyProblem.Code);
        Assert.Equal(RateDeclarationPolicy.UnknownCurrencyMessage, currencyProblem.Message);

        using var amount = Card(HttpMethod.Post, "/api/rate-cards", tenantId, "corr-amt-01");
        var body = BasicBody("RC-NEG", routeId, rules: true);
        body = body with
        {
            Rules = body.Rules!.Select(r => r with { UnitAmount = -1m }).ToArray()
        };
        amount.Content = JsonContent.Create(body);
        var amountRes = await _client.SendAsync(amount);
        var amountProblem = await ReadProblem(amountRes);
        Assert.Equal(HttpStatusCode.Conflict, amountRes.StatusCode);
        Assert.Equal("invalid_amount", amountProblem.Code);
        Assert.Equal("Số tiền / đơn giá không được âm.", amountProblem.Message);
        Assert.Equal("corr-amt-01", amountProblem.CorrelationId);
    }

    [Fact]
    public async Task Route_must_be_active_master_and_inactive_saved_route_stays()
    {
        var tenantId = await CreateTenantAsync("TN-CR-ROUTE");
        var missing = Guid.NewGuid();
        using var bad = Card(HttpMethod.Post, "/api/rate-cards", tenantId, "corr-route-01");
        bad.Content = JsonContent.Create(BasicBody("RC-BAD-ROUTE", missing, rules: true));
        var badRes = await _client.SendAsync(bad);
        var badProblem = await ReadProblem(badRes);
        Assert.Equal(HttpStatusCode.Conflict, badRes.StatusCode);
        Assert.Equal("invalid_route", badProblem.Code);
        Assert.Equal(RateDeclarationPolicy.MissingRouteMessage, badProblem.Message);

        var routeId = await CreateRouteAsync(tenantId, "VN-MY", true);
        var created = await CreateBasicAsync(tenantId, "RC-ROUTE", routeId);
        var card = await GetCard(tenantId, created.Id);
        Assert.Equal(routeId, card.RouteId);
        Assert.Equal("VN-MY", card.RouteCode);
        Assert.Equal("USD", card.CurrencyCode);
        Assert.Equal("basic", card.DeclarationMode);

        await PutRouteAsync(tenantId, routeId, "VN-MY", false);
        using var keep = Card(HttpMethod.Put, $"/api/rate-cards/{created.Id}", tenantId, "corr-route-keep");
        keep.Content = JsonContent.Create(BasicBody("RC-ROUTE", routeId, rules: true, name: "Giữ tuyến") with
        {
            RowVersion = card.RowVersion
        });
        var keepRes = await _client.SendAsync(keep);
        Assert.Equal(HttpStatusCode.OK, keepRes.StatusCode);
        var kept = await GetCard(tenantId, created.Id);
        Assert.Equal(routeId, kept.RouteId);
        Assert.Equal("USD", kept.CurrencyCode);
        Assert.False(kept.RouteIsActive);
    }

    [Fact]
    public async Task Mode_switch_without_confirm_keeps_rules_and_published_mode_is_locked()
    {
        var tenantId = await CreateTenantAsync("TN-CR-MODE");
        var routeId = await CreateRouteAsync(tenantId, "VN-MY", true);
        var created = await CreateBasicAsync(tenantId, "RC-MODE", routeId);
        using var unconfirmed = Card(HttpMethod.Put, $"/api/rate-cards/{created.Id}", tenantId, "corr-mode-01");
        unconfirmed.Content = JsonContent.Create(new DeclarationBody(
            "RC-MODE", "Bảng giá", "vendor", "USD", routeId, "advanced", false,
            null, MatrixColumns(), MatrixBands(), null));
        var denied = await _client.SendAsync(unconfirmed);
        var deniedProblem = await ReadProblem(denied);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.Equal("invalid_declaration_mode", deniedProblem.Code);
        Assert.Equal(RateDeclarationPolicy.ModeChangeConfirmMessage, deniedProblem.Message);
        var rules = await GetRules(tenantId, created.RateVersionId);
        Assert.Contains(rules, r => r.Code == "FRT");

        using var publish = Card(HttpMethod.Post, $"/api/rate-versions/{created.RateVersionId}/publish", tenantId, "corr-pub-01");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(publish)).StatusCode);
        var card = await GetCard(tenantId, created.Id);
        using var locked = Card(HttpMethod.Put, $"/api/rate-cards/{created.Id}", tenantId, "corr-mode-lock");
        locked.Content = JsonContent.Create(new DeclarationBody(
            "RC-MODE", "Bảng giá", "vendor", "USD", routeId, "advanced", true,
            null, MatrixColumns(), MatrixBands(), card.RowVersion));
        var lockedRes = await _client.SendAsync(locked);
        var lockedProblem = await ReadProblem(lockedRes);
        Assert.Equal(HttpStatusCode.Conflict, lockedRes.StatusCode);
        Assert.Equal("Phiên bản đã phát hành không được sửa. Hãy tạo phiên bản mới.", lockedProblem.Message);

        using var next = Card(HttpMethod.Post, $"/api/rate-cards/{created.Id}/versions", tenantId, "corr-mode-next");
        next.Content = JsonContent.Create(new { note = "nháp sau phát hành" });
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(next)).StatusCode);
        using var modeLocked = Card(HttpMethod.Put, $"/api/rate-cards/{created.Id}", tenantId, "corr-mode-lock-2");
        modeLocked.Content = JsonContent.Create(new DeclarationBody(
            "RC-MODE", "Bảng giá", "vendor", "USD", routeId, "advanced", true,
            null, MatrixColumns(), MatrixBands(), card.RowVersion));
        var modeLockedRes = await _client.SendAsync(modeLocked);
        var modeLockedProblem = await ReadProblem(modeLockedRes);
        Assert.Equal(HttpStatusCode.Conflict, modeLockedRes.StatusCode);
        Assert.Equal("invalid_declaration_mode", modeLockedProblem.Code);
        Assert.Equal(RateDeclarationPolicy.PublishedModeLockedMessage, modeLockedProblem.Message);
        Assert.Equal("basic", (await GetCard(tenantId, created.Id)).DeclarationMode);
    }

    [Fact]
    public async Task Stale_row_version_is_rejected()
    {
        var tenantId = await CreateTenantAsync("TN-CR-ROW");
        var routeId = await CreateRouteAsync(tenantId, "VN-MY", true);
        var created = await CreateBasicAsync(tenantId, "RC-ROW", routeId);
        var first = await GetCard(tenantId, created.Id);
        using var ok = Card(HttpMethod.Put, $"/api/rate-cards/{created.Id}", tenantId, "corr-row-ok");
        ok.Content = JsonContent.Create(BasicBody("RC-ROW", routeId, rules: true, name: "Lần một") with
        {
            RowVersion = first.RowVersion
        });
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(ok)).StatusCode);

        using var stale = Card(HttpMethod.Put, $"/api/rate-cards/{created.Id}", tenantId, "corr-row-stale");
        stale.Content = JsonContent.Create(BasicBody("RC-ROW", routeId, rules: true, name: "Lần cũ") with
        {
            RowVersion = first.RowVersion
        });
        var staleRes = await _client.SendAsync(stale);
        var problem = await ReadProblem(staleRes);
        Assert.Equal(HttpStatusCode.Conflict, staleRes.StatusCode);
        Assert.Equal("concurrency_conflict", problem.Code);
        Assert.Contains("thay đổi", problem.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbUpdate", problem.Message, StringComparison.OrdinalIgnoreCase);
        var current = await GetCard(tenantId, created.Id);
        Assert.Equal("Lần một", current.Name);
    }

    [Fact]
    public async Task Declaration_version_and_publish_are_audited()
    {
        var tenantId = await CreateTenantAsync("TN-CR-AUD");
        var routeId = await CreateRouteAsync(tenantId, "VN-MY", true);
        var created = await CreateBasicAsync(tenantId, "RC-AUD", routeId);
        using var version = Card(HttpMethod.Post, $"/api/rate-cards/{created.Id}/versions", tenantId, "corr-ver-01");
        version.Content = JsonContent.Create(new { note = "bản nháp tiếp" });
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(version)).StatusCode);
        using var publish = Card(HttpMethod.Post, $"/api/rate-versions/{created.RateVersionId}/publish", tenantId, "corr-pub-aud");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(publish)).StatusCode);

        var actions = await AuditActions(tenantId, created.Id);
        Assert.Contains("rate_card.declaration_changed", actions);
        Assert.Contains("rate_card.version_created", actions);
        Assert.Contains("rate_card.published", actions);
    }

    [Fact]
    public async Task Basic_round_trips_two_rules_and_advanced_version_publishes()
    {
        var tenantId = await CreateTenantAsync("TN-CR-ADV");
        var routeId = await CreateRouteAsync(tenantId, "VN-MY", true);
        var body = BasicBody("RC-TWO", routeId, rules: true) with
        {
            Rules =
            [
                new RuleBody("FRT", "Cước", "fixed", 100000m, "USD"),
                new RuleBody("DOC", "Chứng từ", "unit_rate", 2m, "USD")
            ]
        };
        using var create = Card(HttpMethod.Post, "/api/rate-cards", tenantId, "corr-two");
        create.Content = JsonContent.Create(body);
        var createdRes = await _client.SendAsync(create);
        var createdText = await createdRes.Content.ReadAsStringAsync();
        Assert.True(createdRes.IsSuccessStatusCode, createdText);
        var created = JsonSerializer.Deserialize<CreatedCard>(createdText, Json)!;
        var rules = await GetRules(tenantId, created.RateVersionId);
        Assert.Equal(2, rules.Count);
        Assert.Contains(rules, r => r.Code == "DOC");

        using var advanced = Card(HttpMethod.Post, "/api/rate-cards", tenantId, "corr-adv");
        advanced.Content = JsonContent.Create(new DeclarationBody(
            "RC-ADV", "Nâng cao", "vendor", "USD", routeId, "advanced", false,
            null, MatrixColumns(), MatrixBands(), null));
        var advancedRes = await _client.SendAsync(advanced);
        var advancedText = await advancedRes.Content.ReadAsStringAsync();
        Assert.True(advancedRes.IsSuccessStatusCode, advancedText);
        var advancedCard = JsonSerializer.Deserialize<CreatedCard>(advancedText, Json)!;
        var stored = await GetCard(tenantId, advancedCard.Id);
        Assert.Equal("advanced", stored.DeclarationMode);
        var matrix = await GetRules(tenantId, advancedCard.RateVersionId);
        Assert.All(matrix, r => Assert.Equal("weight_break_pivot", r.CalcMethod));
        using var publish = Card(HttpMethod.Post, $"/api/rate-versions/{advancedCard.RateVersionId}/publish", tenantId, "corr-adv-pub");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(publish)).StatusCode);
    }

    [Fact]
    public async Task Shared_buy_card_has_no_partner()
    {
        var tenantId = await CreateTenantAsync("TN-CR-SHARE");
        var routeId = await CreateRouteAsync(tenantId, "VN-MY", true);
        var created = await CreateBasicAsync(tenantId, "RC-SHARE", routeId);
        var card = await GetCard(tenantId, created.Id);
        Assert.Equal("vendor", card.PartyType);
        Assert.Null(card.SupplierPartyId);
        Assert.Null(card.CustomerPartyId);
    }

    [Fact]
    public async Task Mixed_declaration_is_reported_and_not_deleted_or_assigned()
    {
        var tenantId = await CreateTenantAsync("TN-CR-MIX");
        var cardId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var basicId = Guid.NewGuid();
        var matrixId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
            db.RateCards.Add(new RateCard
            {
                Id = cardId,
                TenantId = tenantId,
                Code = "RC-MIX",
                Name = "Hỗn hợp",
                PartyType = "vendor",
                CurrencyCode = "VND",
                DeclarationMode = null
            });
            db.RateVersions.Add(new RateVersion
            {
                Id = versionId,
                TenantId = tenantId,
                RateCardId = cardId,
                VersionNo = 3,
                Status = RateVersionStatuses.Draft
            });
            db.PricingRules.Add(new PricingRule
            {
                Id = basicId,
                TenantId = tenantId,
                RateVersionId = versionId,
                Code = "BASIC",
                Name = "Cơ bản",
                CalcMethod = PricingCalcMethods.Fixed,
                UnitAmount = 10,
                CurrencyCode = "VND"
            });
            db.PricingRules.Add(new PricingRule
            {
                Id = matrixId,
                TenantId = tenantId,
                RateVersionId = versionId,
                Code = "MATRIX",
                Name = "Ma trận",
                CalcMethod = PricingCalcMethods.WeightBreakPivot,
                UnitAmount = 0,
                CurrencyCode = "VND"
            });
            db.RateBreaks.Add(new RateBreak
            {
                TenantId = tenantId,
                PricingRuleId = matrixId,
                SequenceNo = 1,
                MinQuantity = 0,
                MaxQuantity = 45,
                UnitAmount = 12
            });
            await db.SaveChangesAsync();
        }

        using var req = Card(HttpMethod.Get, "/api/rate-cards/mixed-declaration", tenantId, "corr-mix-01");
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var rows = await res.Content.ReadFromJsonAsync<List<MixedRow>>(Json);
        var row = Assert.Single(rows!);
        Assert.Equal(cardId, row.Id);
        Assert.Equal("RC-MIX", row.Code);
        Assert.Equal(3, row.VersionNumber);
        Assert.Equal(1, row.CountRules);
        Assert.Equal(1, row.CountMatrixBands);
        Assert.Equal("draft", row.Status);

        var card = await GetCard(tenantId, cardId);
        Assert.Null(card.DeclarationMode);
        Assert.Equal("RC-MIX", card.Code);
    }

    private async Task<Guid> CreateTenantAsync(string code)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/tenants")
        {
            Content = JsonContent.Create(new { code, name = code })
        };
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> CreateRouteAsync(Guid tenantId, string code, bool active)
    {
        var origin = await PutLocationAsync(tenantId, code + "-O");
        var destination = await PutLocationAsync(tenantId, code + "-D");
        return await PutRouteAsync(tenantId, null, code, active, origin, destination);
    }

    private async Task<Guid> PutLocationAsync(Guid tenantId, string code)
    {
        using var req = Card(HttpMethod.Put, "/api/locations", tenantId, null);
        req.Content = JsonContent.Create(new
        {
            code,
            name = code,
            locationType = "airport",
            countryCode = "VN",
            isActive = true,
            aliases = Array.Empty<object>()
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<Guid> PutRouteAsync(
        Guid tenantId,
        Guid? routeId,
        string code,
        bool active,
        Guid? origin = null,
        Guid? destination = null)
    {
        if (origin is null || destination is null)
        {
            using var list = Card(HttpMethod.Get, "/api/routes", tenantId, null);
            var routes = await (await _client.SendAsync(list)).Content.ReadFromJsonAsync<List<RouteBody>>(Json);
            var current = routes!.Single(r => r.Id == routeId);
            origin = current.OriginLocationId;
            destination = current.DestinationLocationId;
        }

        using var req = Card(HttpMethod.Put, "/api/routes", tenantId, null);
        req.Content = JsonContent.Create(new
        {
            code,
            name = code,
            originLocationId = origin,
            destinationLocationId = destination,
            isActive = active,
            intermediateLocationIds = Array.Empty<Guid>()
        });
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<IdBody>(Json))!.Id;
    }

    private async Task<CreatedCard> CreateBasicAsync(Guid tenantId, string code, Guid routeId)
    {
        using var req = Card(HttpMethod.Post, "/api/rate-cards", tenantId, "corr-create");
        req.Content = JsonContent.Create(BasicBody(code, routeId, rules: true));
        var res = await _client.SendAsync(req);
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, text);
        var created = JsonSerializer.Deserialize<CreatedCard>(text, Json);
        return created!;
    }

    private async Task<CardBody> GetCard(Guid tenantId, Guid id)
    {
        using var req = Card(HttpMethod.Get, $"/api/rate-cards/{id}", tenantId, null);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CardBody>(Json))!;
    }

    private async Task<List<RuleBody>> GetRules(Guid tenantId, Guid versionId)
    {
        using var req = Card(HttpMethod.Get, $"/api/rate-versions/{versionId}/rules", tenantId, null);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<List<RuleBody>>(Json))!;
    }

    private async Task<List<string>> AuditActions(Guid tenantId, Guid cardId)
    {
        using var req = Card(HttpMethod.Get, $"/api/audit-events?objectType=rate_card&objectId={cardId}", tenantId, null);
        var res = await _client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var rows = await res.Content.ReadFromJsonAsync<List<AuditBody>>(Json);
        return rows!.Select(r => r.Action).ToList();
    }

    private static DeclarationBody BasicBody(string code, Guid routeId, bool rules, bool matrix = false, string name = "Bảng giá") =>
        new(
            code,
            name,
            "vendor",
            "USD",
            routeId,
            "basic",
            false,
            rules
                ? [new RuleBody("FRT", "Cước", "fixed", 100000m, "USD")]
                : null,
            matrix ? MatrixColumns() : null,
            matrix ? MatrixBands() : null,
            null);

    private static ColumnBody[] MatrixColumns() => [new("GEN", "Hàng thường")];

    private static BandBody[] MatrixBands() => [new(0m, 45m, [10m])];

    private static HttpRequestMessage Card(HttpMethod method, string path, Guid tenantId, string? correlationId)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Add("X-Tenant-Id", tenantId.ToString());
        if (correlationId is not null)
        {
            req.Headers.Add("X-Correlation-Id", correlationId);
        }

        return req;
    }

    private static async Task<ProblemBody> ReadProblem(HttpResponseMessage res)
    {
        var body = await res.Content.ReadFromJsonAsync<ProblemBody>(Json);
        return body!;
    }

    private sealed record IdBody(Guid Id);
    private sealed record CreatedCard(Guid Id, Guid RateCardId, Guid RateVersionId);
    private sealed record ProblemBody(string Code, string Message, string CorrelationId);
    private sealed record RouteBody(Guid Id, Guid OriginLocationId, Guid DestinationLocationId);
    private sealed record CardBody(
        Guid Id,
        string Code,
        string Name,
        string PartyType,
        string CurrencyCode,
        Guid? RouteId,
        string? RouteCode,
        string? DeclarationMode,
        bool? RouteIsActive,
        string? RowVersion,
        Guid? SupplierPartyId,
        Guid? CustomerPartyId);
    private sealed record RuleBody(string Code, string Name, string CalcMethod, decimal UnitAmount, string CurrencyCode);
    private sealed record ColumnBody(string Code, string Name);
    private sealed record BandBody(decimal MinQuantity, decimal? MaxQuantity, decimal?[] Prices);
    private sealed record DeclarationBody(
        string Code,
        string Name,
        string PartyType,
        string CurrencyCode,
        Guid RouteId,
        string DeclarationMode,
        bool ConfirmModeChange,
        RuleBody[]? Rules,
        ColumnBody[]? Columns,
        BandBody[]? Bands,
        string? RowVersion);
    private sealed record MixedRow(
        Guid Id,
        string Code,
        int VersionNumber,
        int CountRules,
        int CountMatrixBands,
        string Status);
    private sealed record AuditBody(string Action);
}
