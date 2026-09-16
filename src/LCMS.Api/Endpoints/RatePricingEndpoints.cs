using LCMS.Application.PricingRules.Commands;
using LCMS.Application.RateCards.Commands;
using LCMS.Application.RateCards.Queries;
using LCMS.Application.RateVersions.Commands;
using LCMS.Application.RateVersions.Queries;
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
                    body.Description),
                ct);
            return Results.Created($"/api/rate-cards/{id}", new { id });
        });
        cards.MapGet("/", async (
            string? q,
            string? partyType,
            bool? isActive,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListRateCardsQuery(q, partyType, isActive, page, pageSize),
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
                    body.IsActive),
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
                    body.SortOrder ?? 0),
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
                    body.SortOrder ?? 0),
                ct);
            return Results.Created($"/api/pricing-rules/{ruleId}/components/{id}", new { id });
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
                    body.SeedExpectedCosts ?? false),
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

        return app;
    }
}

public sealed record CreateRateCardRequest(
    string Code,
    string Name,
    string PartyType,
    string CurrencyCode,
    string? Description);

public sealed record UpdateRateCardRequest(
    string Name,
    string PartyType,
    string CurrencyCode,
    string? Description,
    bool IsActive);

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
    int? SortOrder);

public sealed record AddPricingRuleComponentRequest(
    string Code,
    string Name,
    string FinancialNature,
    string? CostTypeCode,
    string? RevenueTypeCode,
    decimal Amount,
    string CurrencyCode,
    int? SortOrder);

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
    bool? SeedExpectedCosts);
