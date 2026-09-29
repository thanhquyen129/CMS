using LCMS.Application.OperationalReferences.Edit;
using MediatR;

namespace LCMS.Api.Endpoints;

/// <summary>Controlled edit of Bill / Order / Shipment / Leg / Movement reference fields (ADR-0039).</summary>
public static class OperationalReferenceEditEndpoints
{
    public static IEndpointRouteBuilder MapOperationalReferenceEditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/operational-references").WithTags("OperationalReferenceEdit");

        group.MapGet("/{type}/{id:guid}/edit", async (string type, Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetOperationalReferenceEditQuery(type, id), ct)));

        group.MapPatch("/{type}/{id:guid}", async (
            string type,
            Guid id,
            EditOperationalReferenceRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            var result = await sender.Send(
                new EditOperationalReferenceCommand(
                    type,
                    id,
                    body.Changes ?? new Dictionary<string, string?>(),
                    body.Reason,
                    body.RevertFields,
                    ifMatch.ToString()),
                ct);
            return Results.Ok(result);
        });

        group.MapPost("/{type}/{id:guid}/chargeable-weight/confirm", async (
            string type,
            Guid id,
            ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(new ConfirmChargeableWeightCommand(type, id), ct)));

        return app;
    }
}

public sealed record EditOperationalReferenceRequest(
    Dictionary<string, string?>? Changes,
    string? Reason,
    IReadOnlyList<string>? RevertFields);
