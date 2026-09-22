using LCMS.Application.FinancialCloses.Commands;
using LCMS.Application.FinancialCloses.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class FinancialCloseEndpoints
{
    public static IEndpointRouteBuilder MapFinancialCloseEndpoints(this IEndpointRouteBuilder app)
    {
        var closes = app.MapGroup("/api/financial-closes").WithTags("FinancialCloses");

        closes.MapPost("/", async (StartFinancialCloseRequest body, HttpRequest http, ISender sender, CancellationToken ct) =>
        {
            http.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);
            var id = await sender.Send(
                new StartFinancialCloseCommand(
                    body.ScopeType,
                    body.ScopeId,
                    body.PeriodFrom,
                    body.PeriodTo,
                    body.PolicyVersion,
                    body.BaseCurrency,
                    body.Notes,
                    body.SupersedesCloseId,
                    idempotencyKey.ToString()),
                ct);
            return Results.Created($"/api/financial-closes/{id}", new { id });
        });

        closes.MapGet("/", async (string? status, string? scopeType, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListFinancialClosesQuery(status, scopeType), ct);
            return Results.Ok(list);
        });

        closes.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetFinancialCloseByIdQuery(id), ct);
            return Results.Ok(item);
        });

        closes.MapPost("/{id:guid}/snapshot", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var snapshotId = await sender.Send(new CreateFinancialCloseSnapshotCommand(id), ct);
            return Results.Created($"/api/financial-close-snapshots/{snapshotId}", new { id = snapshotId });
        });

        closes.MapPost("/{id:guid}/reopen", async (
            Guid id,
            ReopenFinancialCloseRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ReopenFinancialCloseCommand(id, body.Reason), ct);
            return Results.NoContent();
        });

        closes.MapGet("/{id:guid}/snapshots", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListFinancialCloseSnapshotsQuery(id), ct);
            return Results.Ok(list);
        });

        closes.MapGet("/{id:guid}/pnl", async (
            Guid id,
            Guid? snapshotId,
            ISender sender,
            CancellationToken ct) =>
        {
            var pnl = await sender.Send(new GetFinancialClosePnlQuery(id, snapshotId), ct);
            return Results.Ok(pnl);
        });

        var snapshots = app.MapGroup("/api/financial-close-snapshots").WithTags("FinancialCloseSnapshots");

        snapshots.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetFinancialCloseSnapshotByIdQuery(id), ct);
            return Results.Ok(item);
        });

        return app;
    }
}

public sealed record StartFinancialCloseRequest(
    string? ScopeType,
    Guid? ScopeId,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    string? PolicyVersion,
    string? BaseCurrency,
    string? Notes,
    Guid? SupersedesCloseId);

public sealed record ReopenFinancialCloseRequest(string? Reason);
