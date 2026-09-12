using LCMS.Application.BankFeed.Commands;
using LCMS.Application.BankFeed.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class BankFeedEndpoints
{
    public static IEndpointRouteBuilder MapBankFeedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bank-feed/lines").WithTags("BankFeed");

        group.MapPost("/", async (CreateBankFeedLineRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateBankFeedLineCommand(
                    body.ValueDate,
                    body.Amount,
                    body.CurrencyCode,
                    body.Direction,
                    body.BankReference,
                    body.CounterpartyName,
                    body.Description),
                ct);
            return Results.Created($"/api/bank-feed/lines/{id}", new { id });
        });

        group.MapGet("/", async (string? status, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListBankFeedLinesQuery(status), ct);
            return Results.Ok(list);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetBankFeedLineByIdQuery(id), ct);
            return Results.Ok(item);
        });

        group.MapPost("/{id:guid}/ignore", async (
            Guid id,
            IgnoreBankFeedLineRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new IgnoreBankFeedLineCommand(id, body.IgnoreReason), ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record CreateBankFeedLineRequest(
    DateOnly ValueDate,
    decimal Amount,
    string CurrencyCode,
    string? Direction,
    string? BankReference,
    string? CounterpartyName,
    string? Description);

public sealed record IgnoreBankFeedLineRequest(string? IgnoreReason);
