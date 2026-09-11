using LCMS.Application.Costs.Commands;
using LCMS.Application.Costs.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class CostEndpoints
{
    public static IEndpointRouteBuilder MapCostEndpoints(this IEndpointRouteBuilder app)
    {
        var costs = app.MapGroup("/api/costs").WithTags("Costs");

        costs.MapPost("/", async (CreateCostRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateCostCommand(
                    body.BillId,
                    body.AttributionType,
                    body.Amount,
                    body.CurrencyCode,
                    body.EffectiveDate,
                    body.CostTypeCode,
                    body.VendorPartyId,
                    body.SourceType,
                    body.SourceId),
                ct);
            return Results.Created($"/api/costs/{id}", new { id });
        });

        costs.MapGet("/", async (
            Guid? billId,
            string? financialMaturity,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(new ListCostsQuery(billId, financialMaturity), ct);
            return Results.Ok(list);
        });

        costs.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var cost = await sender.Send(new GetCostByIdQuery(id), ct);
            return Results.Ok(cost);
        });

        costs.MapPost("/{id:guid}/confirm", async (
            Guid id,
            ConfirmCostRequest? body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ConfirmCostCommand(id, body?.ConfirmedAmount), ct);
            return Results.NoContent();
        });

        costs.MapPost("/{id:guid}/actualize", async (
            Guid id,
            ActualizeCostRequest? body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ActualizeCostCommand(id, body?.ActualAmount), ct);
            return Results.NoContent();
        });

        costs.MapPost("/{id:guid}/adjustments", async (
            Guid id,
            AdjustCostRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var adjId = await sender.Send(
                new AdjustCostCommand(
                    id,
                    body.AdjustmentType,
                    body.DeltaAmount,
                    body.Reason,
                    body.EffectiveDate),
                ct);
            return Results.Created($"/api/costs/{id}/adjustments/{adjId}", new { id = adjId });
        });

        costs.MapPost("/{id:guid}/allocations", async (
            Guid id,
            CreateAllocationRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var details = (body.Details ?? [])
                .Select(d => new AllocationDetailInput(d.BillId, d.BasisValue, d.ManualOverrideAmount, d.OverrideReason))
                .ToList();
            var allocationId = await sender.Send(
                new CreateCostAllocationCommand(id, body.AllocationBasis, details),
                ct);
            return Results.Created($"/api/cost-allocations/{allocationId}", new { id = allocationId });
        });

        var allocations = app.MapGroup("/api/cost-allocations").WithTags("CostAllocations");
        allocations.MapPost("/{id:guid}/finalize", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new FinalizeCostAllocationCommand(id), ct);
            return Results.NoContent();
        });

        var ratings = app.MapGroup("/api/ratings").WithTags("Ratings");
        ratings.MapPost("/{id:guid}/seed-expected-costs", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SeedExpectedCostsFromRatingCommand(id), ct);
            return Results.Ok(result);
        });

        return app;
    }
}

public sealed record CreateCostRequest(
    Guid? BillId,
    string AttributionType,
    decimal Amount,
    string CurrencyCode,
    DateOnly? EffectiveDate,
    string? CostTypeCode,
    Guid? VendorPartyId,
    string? SourceType,
    Guid? SourceId);

public sealed record ConfirmCostRequest(decimal? ConfirmedAmount);

public sealed record ActualizeCostRequest(decimal? ActualAmount);

public sealed record AdjustCostRequest(
    string AdjustmentType,
    decimal DeltaAmount,
    string Reason,
    DateOnly? EffectiveDate);

public sealed record AllocationDetailRequest(
    Guid BillId,
    decimal BasisValue,
    decimal? ManualOverrideAmount,
    string? OverrideReason);

public sealed record CreateAllocationRequest(
    string AllocationBasis,
    List<AllocationDetailRequest>? Details);
