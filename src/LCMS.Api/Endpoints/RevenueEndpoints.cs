using LCMS.Application.Bills.Queries;
using LCMS.Application.Revenues.Commands;
using LCMS.Application.Revenues.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class RevenueEndpoints
{
    public static IEndpointRouteBuilder MapRevenueEndpoints(this IEndpointRouteBuilder app)
    {
        var revenues = app.MapGroup("/api/revenues").WithTags("Revenues");

        revenues.MapPost("/", async (CreateRevenueRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateRevenueCommand(
                    body.BillId,
                    body.Amount,
                    body.CurrencyCode,
                    body.EffectiveDate,
                    body.RevenueTypeCode,
                    body.CustomerPartyId,
                    body.SourceType,
                    body.SourceId,
                    body.RecognitionPolicyVersion),
                ct);
            return Results.Created($"/api/revenues/{id}", new { id });
        });

        revenues.MapGet("/", async (
            Guid? billId,
            string? financialMaturity,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(new ListRevenuesQuery(billId, financialMaturity), ct);
            return Results.Ok(list);
        });

        revenues.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var revenue = await sender.Send(new GetRevenueByIdQuery(id), ct);
            return Results.Ok(revenue);
        });

        revenues.MapPost("/{id:guid}/confirm", async (
            Guid id,
            ConfirmRevenueRequest? body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ConfirmRevenueCommand(id, body?.ConfirmedAmount), ct);
            return Results.NoContent();
        });

        revenues.MapPost("/{id:guid}/actualize", async (
            Guid id,
            ActualizeRevenueRequest? body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ActualizeRevenueCommand(id, body?.ActualAmount), ct);
            return Results.NoContent();
        });

        revenues.MapPost("/{id:guid}/adjustments", async (
            Guid id,
            AdjustRevenueRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var adjId = await sender.Send(
                new AdjustRevenueCommand(
                    id,
                    body.AdjustmentType,
                    body.DeltaAmount,
                    body.Reason,
                    body.EffectiveDate),
                ct);
            return Results.Created($"/api/revenues/{id}/adjustments/{adjId}", new { id = adjId });
        });

        var bills = app.MapGroup("/api/bills").WithTags("Bills");
        bills.MapGet("/{id:guid}/financial-profile", async (
            Guid id,
            DateOnly? asOf,
            ISender sender,
            CancellationToken ct) =>
        {
            var profile = await sender.Send(new GetBillFinancialProfileQuery(id, asOf), ct);
            return Results.Ok(profile);
        });

        bills.MapGet("/{id:guid}/profitability", async (
            Guid id,
            string? view,
            ISender sender,
            CancellationToken ct) =>
        {
            var profitability = await sender.Send(
                new GetBillProfitabilityQuery(id, string.IsNullOrWhiteSpace(view) ? "best" : view),
                ct);
            return Results.Ok(profitability);
        });

        return app;
    }
}

public sealed record CreateRevenueRequest(
    Guid BillId,
    decimal Amount,
    string CurrencyCode,
    DateOnly? EffectiveDate,
    string? RevenueTypeCode,
    Guid? CustomerPartyId,
    string? SourceType,
    Guid? SourceId,
    string? RecognitionPolicyVersion);

public sealed record ConfirmRevenueRequest(decimal? ConfirmedAmount);

public sealed record ActualizeRevenueRequest(decimal? ActualAmount);

public sealed record AdjustRevenueRequest(
    string AdjustmentType,
    decimal DeltaAmount,
    string Reason,
    DateOnly? EffectiveDate);
