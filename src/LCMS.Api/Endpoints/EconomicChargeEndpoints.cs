using LCMS.Application.EconomicCharges;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class EconomicChargeEndpoints
{
    public static IEndpointRouteBuilder MapEconomicChargeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/economic-charge-types").WithTags("EconomicChargeTypes");
        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListEconomicChargeTypesQuery(), ct)));
        group.MapPost("/", async (UpsertEconomicChargeTypeRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new UpsertEconomicChargeTypeCommand(body.Code, body.Name), ct);
            return Results.Created($"/api/economic-charge-types/{id}", new { id });
        });
        group.MapPost("/mappings", async (MapChargeTypeRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new MapChargeTypeCommand(body.SourceKind, body.SourceCode, body.EconomicChargeTypeId), ct);
            return Results.Created($"/api/economic-charge-types/mappings/{id}", new { id });
        });
        return app;
    }
}

public sealed record UpsertEconomicChargeTypeRequest(string Code, string Name);

public sealed record MapChargeTypeRequest(string SourceKind, string SourceCode, Guid EconomicChargeTypeId);
