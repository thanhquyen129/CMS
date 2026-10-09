using LCMS.Application.Surcharges;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class SurchargeEndpoints
{
    public static IEndpointRouteBuilder MapSurchargeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/surcharges").WithTags("Surcharges");
        group.MapPost("/", async (SaveSurchargeRequest body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(
                new CreateSurchargeCommand(
                    body.Code,
                    body.Name,
                    body.Direction,
                    body.ValidFrom,
                    body.ValidTo,
                    body.Publish,
                    ToRule(body),
                    body.VatRate),
                ct);
            return Results.Created($"/api/surcharges/{result.SurchargeId}", result);
        });
        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSurchargeQuery(id), ct)));
        group.MapPut("/{id:guid}/versions/{versionId:guid}", async (
            Guid id,
            Guid versionId,
            SaveSurchargeRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpdateSurchargeVersionCommand(id, versionId, body.Name, body.Direction, body.ValidFrom, body.ValidTo, ToRule(body), body.VatRate),
                ct);
            return Results.NoContent();
        });
        group.MapPost("/{id:guid}/versions/{versionId:guid}/publish", async (
            Guid id,
            Guid versionId,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new PublishSurchargeVersionCommand(id, versionId), ct);
            return Results.NoContent();
        });
        group.MapPost("/{id:guid}/versions", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var versionId = await sender.Send(new CreateSurchargeVersionCommand(id), ct);
            return Results.Created($"/api/surcharges/{id}/versions/{versionId}", new { id = versionId });
        });
        group.MapPost("/versions/{versionId:guid}/rules", async (
            Guid versionId,
            SaveSurchargeRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var ruleId = await sender.Send(new AddSurchargeRuleCommand(versionId, ToRule(body)), ct);
            return Results.Created($"/api/surcharges/versions/{versionId}/rules/{ruleId}", new { id = ruleId });
        });
        group.MapPost("/migrate-legacy", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new MigrateLegacySurchargesCommand(), ct)));
        return app;
    }

    private static SurchargeRuleInput ToRule(SaveSurchargeRequest body) =>
        new(
            body.CalculationMode,
            body.Basis,
            body.CurrencyCode,
            body.RateAmountPercent,
            body.Priority,
            body.MinAmount,
            body.MaxAmount,
            body.ContainerType,
            body.TransportMode,
            body.ServiceTypeCode,
            body.RouteCode,
            body.OriginCode,
            body.DestinationCode,
            body.CommodityCode,
            body.DangerousGoods,
            body.WeightFrom,
            body.WeightTo,
            body.RateCardId,
            body.RateVersionId,
            body.VendorPartyId,
            body.CustomerPartyId,
            body.Breaks,
            body.CustomerGroupCode);
}

public sealed record SaveSurchargeRequest(
    string Code,
    string Name,
    string Direction,
    string CalculationMode,
    string CurrencyCode,
    decimal RateAmountPercent,
    string? Basis = null,
    int? Priority = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    string? ContainerType = null,
    string? TransportMode = null,
    string? ServiceTypeCode = null,
    string? RouteCode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? CommodityCode = null,
    bool? DangerousGoods = null,
    decimal? WeightFrom = null,
    decimal? WeightTo = null,
    Guid? RateCardId = null,
    Guid? RateVersionId = null,
    Guid? VendorPartyId = null,
    Guid? CustomerPartyId = null,
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidTo = null,
    bool Publish = false,
    IReadOnlyList<SurchargeBreakInput>? Breaks = null,
    decimal? VatRate = null,
    string? CustomerGroupCode = null);
