using LCMS.Application.PricingRules.Commands;
using LCMS.Application.RateCards.Commands;
using LCMS.Application.RateCards.Queries;
using LCMS.Application.RateVersions.Commands;
using LCMS.Application.RateVersions.Queries;
using LCMS.Application.Ratings;
using LCMS.Application.Ratings.Commands;
using LCMS.Application.Ratings.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class RatePricingEndpoints
{
    public static IEndpointRouteBuilder MapRatePricingEndpoints(this IEndpointRouteBuilder app)
    {
        var cards = app.MapGroup("/api/rate-cards").WithTags("RateCards");
        cards.MapPost("/", async (CreateRateCardRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateRateCardCommand(
                    body.Code,
                    body.Name,
                    body.PartyType,
                    body.CurrencyCode,
                    body.Description,
                    body.TransportMode,
                    body.RouteCode,
                    body.CarrierName),
                ct);
            return Results.Created($"/api/rate-cards/{id}", new { id });
        });
        cards.MapPost("/compose", async (ComposeTariffRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new ComposeTariffCommand(
                    body.Code,
                    body.Name,
                    body.PartyType,
                    body.CurrencyCode,
                    body.Description,
                    body.TransportMode,
                    body.RouteCode,
                    body.CarrierName,
                    body.EffectiveFrom,
                    body.Note,
                    body.RateVersionId,
                    body.MinimumQuantity,
                    body.Columns,
                    body.Bands,
                    body.Delivery,
                    body.Remote),
                ct);
            return Results.Created($"/api/rate-cards/{result.RateCardId}", result);
        });
        cards.MapGet("/", async (
            string? q,
            string? partyType,
            bool? isActive,
            int? page,
            int? pageSize,
            string? transportMode,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListRateCardsQuery(q, partyType, isActive, page, pageSize, transportMode),
                ct);
            if (page is null && pageSize is null)
            {
                return Results.Ok(list.Items);
            }

            return Results.Ok(list);
        });
        cards.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var card = await sender.Send(new GetRateCardByIdQuery(id), ct);
            return Results.Ok(card);
        });
        cards.MapPut("/{id:guid}", async (
            Guid id,
            UpdateRateCardRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpdateRateCardCommand(
                    id,
                    body.Name,
                    body.PartyType,
                    body.CurrencyCode,
                    body.Description,
                    body.IsActive,
                    body.TransportMode,
                    body.RouteCode,
                    body.CarrierName),
                ct);
            return Results.NoContent();
        });
        cards.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SoftDeleteRateCardCommand(id), ct);
            return Results.NoContent();
        });
        cards.MapPost("/{rateCardId:guid}/versions", async (
            Guid rateCardId,
            CreateRateVersionRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateRateVersionCommand(
                    rateCardId,
                    body.EffectiveFrom,
                    body.EffectiveTo,
                    body.Note),
                ct);
            return Results.Created($"/api/rate-versions/{id}", new { id });
        });
        cards.MapGet("/{rateCardId:guid}/versions", async (
            Guid rateCardId,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(new ListRateVersionsQuery(rateCardId), ct);
            return Results.Ok(list);
        });

        var versions = app.MapGroup("/api/rate-versions").WithTags("RateVersions");
        versions.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var version = await sender.Send(new GetRateVersionByIdQuery(id), ct);
            return Results.Ok(version);
        });
        versions.MapPost("/{id:guid}/publish", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new PublishRateVersionCommand(id), ct);
            return Results.NoContent();
        });
        versions.MapPost("/{versionId:guid}/rules", async (
            Guid versionId,
            AddPricingRuleRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(
                new AddPricingRuleCommand(
                    versionId,
                    body.Code,
                    body.Name,
                    body.CalcMethod,
                    body.UnitAmount,
                    body.CurrencyCode,
                    body.Applicability,
                    body.ServiceTypeCode,
                    body.PartyTypeCode,
                    body.RouteCode,
                    body.MinAmount,
                    body.MaxAmount,
                    body.SortOrder ?? 0,
                    body.ChargeCode,
                    body.TransportMode,
                    body.OriginCode,
                    body.DestinationCode,
                    body.CommodityCode,
                    body.VolumetricFactor,
                    body.RoundingStep),
                ct);
            return Results.Created($"/api/pricing-rules/{id}", new { id });
        });
        versions.MapGet("/{versionId:guid}/rules", async (
            Guid versionId,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(new ListPricingRulesQuery(versionId), ct);
            return Results.Ok(list);
        });

        var rules = app.MapGroup("/api/pricing-rules").WithTags("PricingRules");
        rules.MapPost("/{ruleId:guid}/components", async (
            Guid ruleId,
            AddPricingRuleComponentRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(
                new AddPricingRuleComponentCommand(
                    ruleId,
                    body.Code,
                    body.Name,
                    body.FinancialNature,
                    body.CostTypeCode,
                    body.RevenueTypeCode,
                    body.Amount,
                    body.CurrencyCode,
                    body.SortOrder ?? 0,
                    body.CalcMethod,
                    body.DependsOnCode),
                ct);
            return Results.Created($"/api/pricing-rules/{ruleId}/components/{id}", new { id });
        });
        rules.MapPut("/components/{id:guid}", async (
            Guid id,
            UpdatePricingRuleComponentRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpdatePricingRuleComponentCommand(
                    id,
                    body.Name,
                    body.FinancialNature,
                    body.CostTypeCode,
                    body.RevenueTypeCode,
                    body.Amount,
                    body.CurrencyCode),
                ct);
            return Results.NoContent();
        });
        rules.MapDelete("/components/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeletePricingRuleComponentCommand(id), ct);
            return Results.NoContent();
        });
        rules.MapPost("/{ruleId:guid}/breaks", async (Guid ruleId, AddRateBreakRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new AddRateBreakCommand(ruleId, body.SequenceNo, body.MinQuantity, body.MaxQuantity, body.UnitAmount),
                ct);
            return Results.Created($"/api/pricing-rules/{ruleId}/breaks/{id}", new { id });
        });
        rules.MapPost("/{ruleId:guid}/container-rates", async (Guid ruleId, AddContainerRateRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new AddContainerRateCommand(ruleId, body.ContainerType, body.UnitAmount), ct);
            return Results.Created($"/api/pricing-rules/{ruleId}/container-rates/{id}", new { id });
        });

        var ratings = app.MapGroup("/api/ratings").WithTags("Ratings");
        ratings.MapPost("/", async (CreateRatingRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateRatingCommand(
                    body.BillId,
                    body.RateVersionId,
                    body.Quantity,
                    body.Weight,
                    body.ServiceTypeCode,
                    body.PartyTypeCode,
                    body.RouteCode,
                    body.BaseAmount,
                    body.SupersedesRatingId,
                    body.SeedExpectedCosts ?? false,
                    body.SeedExpectedRevenues ?? false,
                    body.RateDate,
                    body.OriginCode,
                    body.DestinationCode,
                    body.TransportMode,
                    body.CommodityCode,
                    body.GrossWeightKg,
                    body.VolumeCbm,
                    body.ChargeableOverrideReason,
                    body.TargetCurrency,
                    body.Containers?.Select(c => new RatingContainerQty(c.ContainerType, c.Quantity)).ToList()),
                ct);
            return Results.Created($"/api/ratings/{id}", new { id });
        });
        ratings.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var rating = await sender.Send(new GetRatingByIdQuery(id), ct);
            return Results.Ok(rating);
        });

        var bills = app.MapGroup("/api/bills").WithTags("Bills");
        bills.MapGet("/{billId:guid}/ratings", async (Guid billId, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListRatingsByBillQuery(billId), ct);
            return Results.Ok(list);
        });

        ratings.MapPost("/compare", async (CompareRatesRequest body, ISender sender, CancellationToken ct) =>
        {
            var quotes = await sender.Send(
                new CompareRatesQuery(
                    body.BillId,
                    body.PartyType,
                    body.TransportMode,
                    body.OriginCode,
                    body.DestinationCode,
                    body.RouteCode,
                    body.RateDate,
                    body.Quantity,
                    body.GrossWeightKg,
                    body.VolumeCbm),
                ct);
            return Results.Ok(quotes);
        });

        var imports = app.MapGroup("/api/rate-imports").WithTags("RateImports");
        imports.MapPost("/preview", async (ImportRateCardsRequest body, ISender sender, CancellationToken ct) =>
        {
            var preview = await sender.Send(new PreviewRateImportCommand(body.Cards), ct);
            return Results.Ok(preview);
        });
        imports.MapPost("/commit", async (ImportRateCardsRequest body, ISender sender, CancellationToken ct) =>
        {
            var count = await sender.Send(new CommitRateImportCommand(body.Cards), ct);
            return Results.Ok(new { count });
        });

        app.MapGet("/api/surcharges", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListSurchargesQuery(), ct))).WithTags("Surcharges");
        app.MapGet("/api/rate-appendices", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListRateAppendicesQuery(), ct))).WithTags("RateAppendices");
        app.MapGet("/api/rating-history", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListRatingHistoryQuery(), ct))).WithTags("RatingHistory");

        return app;
    }
}

public sealed record ComposeTariffRequest(
    string? Code,
    string? Name,
    string? PartyType,
    string? CurrencyCode,
    string? Description,
    string? TransportMode,
    string? RouteCode,
    string? CarrierName,
    DateTimeOffset? EffectiveFrom,
    string? Note,
    Guid? RateVersionId,
    decimal? MinimumQuantity,
    IReadOnlyList<ComposeTariffColumn> Columns,
    IReadOnlyList<ComposeTariffBand> Bands,
    ComposeDeliveryFee? Delivery,
    ComposeRemoteFee? Remote);

public sealed record CreateRateCardRequest(
    string Code,
    string Name,
    string PartyType,
    string CurrencyCode,
    string? Description,
    string? TransportMode = null,
    string? RouteCode = null,
    string? CarrierName = null);

public sealed record UpdateRateCardRequest(
    string Name,
    string PartyType,
    string CurrencyCode,
    string? Description,
    bool IsActive,
    string? TransportMode = null,
    string? RouteCode = null,
    string? CarrierName = null);

public sealed record CreateRateVersionRequest(
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Note);

public sealed record AddPricingRuleRequest(
    string Code,
    string Name,
    string CalcMethod,
    decimal UnitAmount,
    string CurrencyCode,
    string? Applicability,
    string? ServiceTypeCode,
    string? PartyTypeCode,
    string? RouteCode,
    decimal? MinAmount,
    decimal? MaxAmount,
    int? SortOrder,
    string? ChargeCode = null,
    string? TransportMode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? CommodityCode = null,
    decimal? VolumetricFactor = null,
    decimal? RoundingStep = null);

public sealed record AddPricingRuleComponentRequest(
    string Code,
    string Name,
    string FinancialNature,
    string? CostTypeCode,
    string? RevenueTypeCode,
    decimal Amount,
    string CurrencyCode,
    int? SortOrder,
    string? CalcMethod = null,
    string? DependsOnCode = null);

public sealed record UpdatePricingRuleComponentRequest(
    string Name,
    string FinancialNature,
    string? CostTypeCode,
    string? RevenueTypeCode,
    decimal Amount,
    string CurrencyCode);

public sealed record AddRateBreakRequest(int SequenceNo, decimal MinQuantity, decimal? MaxQuantity, decimal UnitAmount);

public sealed record AddContainerRateRequest(string ContainerType, decimal UnitAmount);

public sealed record CreateRatingRequest(
    Guid BillId,
    Guid RateVersionId,
    decimal? Quantity,
    decimal? Weight,
    string? ServiceTypeCode,
    string? PartyTypeCode,
    string? RouteCode,
    decimal? BaseAmount,
    Guid? SupersedesRatingId,
    bool? SeedExpectedCosts,
    bool? SeedExpectedRevenues,
    DateTimeOffset? RateDate = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? TransportMode = null,
    string? CommodityCode = null,
    decimal? GrossWeightKg = null,
    decimal? VolumeCbm = null,
    string? ChargeableOverrideReason = null,
    string? TargetCurrency = null,
    IReadOnlyList<RatingContainerRequest>? Containers = null);

public sealed record RatingContainerRequest(string ContainerType, int Quantity);

public sealed record CompareRatesRequest(
    Guid? BillId,
    string? PartyType,
    string? TransportMode,
    string? OriginCode,
    string? DestinationCode,
    string? RouteCode,
    DateTimeOffset? RateDate,
    decimal? Quantity,
    decimal? GrossWeightKg,
    decimal? VolumeCbm);
