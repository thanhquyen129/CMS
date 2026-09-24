using LCMS.Application.Costs.Commands;
using LCMS.Application.Costs.Queries;
using LCMS.Application.Revenues.Commands;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class CostEndpoints
{
    public static IEndpointRouteBuilder MapCostEndpoints(this IEndpointRouteBuilder app)
    {
        var costs = app.MapGroup("/api/costs").WithTags("Costs");

        costs.MapPost("/", async (CreateCostRequest body, HttpRequest http, ISender sender, CancellationToken ct) =>
        {
            http.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);
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
                    body.SourceId,
                    body.OrganizationId,
                    idempotencyKey.ToString()),
                ct);
            return Results.Created($"/api/costs/{id}", new { id });
        });

        costs.MapGet("/", async (
            Guid? billId,
            string? financialMaturity,
            string? attributionType,
            Guid? vendorPartyId,
            DateOnly? fromDate,
            DateOnly? toDate,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListCostsQuery(
                    billId,
                    financialMaturity,
                    attributionType,
                    vendorPartyId,
                    fromDate,
                    toDate,
                    page,
                    pageSize),
                ct);
            if (page is null && pageSize is null)
            {
                return Results.Ok(list.Items);
            }

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
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);
            var adjId = await sender.Send(
                new AdjustCostCommand(
                    id,
                    body.AdjustmentType,
                    body.DeltaAmount,
                    body.Reason,
                    body.EffectiveDate,
                    idempotencyKey.ToString()),
                ct);
            return Results.Created($"/api/costs/{id}/adjustments/{adjId}", new { id = adjId });
        });

        costs.MapPost("/{id:guid}/allocations", async (
            Guid id,
            CreateAllocationRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);
            var details = (body.Details ?? [])
                .Select(d => new AllocationDetailInput(d.BillId, d.BasisValue, d.ManualOverrideAmount, d.OverrideReason))
                .ToList();
            var allocationId = await sender.Send(
                new CreateCostAllocationCommand(
                    id,
                    body.AllocationBasis,
                    details,
                    body.ApplicabilityMode,
                    body.ScopeId,
                    body.ConditionCode,
                    idempotencyKey.ToString()),
                ct);
            return Results.Created($"/api/cost-allocations/{allocationId}", new { id = allocationId });
        });

        var allocations = app.MapGroup("/api/cost-allocations").WithTags("CostAllocations");
        allocations.MapPost("/{id:guid}/finalize", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new FinalizeCostAllocationCommand(id), ct);
            return Results.NoContent();
        });
        allocations.MapPost("/{id:guid}/calculate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CalculateCostAllocationCommand(id), ct);
            return Results.NoContent();
        });
        allocations.MapPost("/{id:guid}/submit", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SubmitCostAllocationCommand(id), ct);
            return Results.NoContent();
        });
        allocations.MapPost("/{id:guid}/cancel", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelCostAllocationCommand(id), ct);
            return Results.NoContent();
        });

        var ratings = app.MapGroup("/api/ratings").WithTags("Ratings");
        ratings.MapPost("/{id:guid}/seed-expected-costs", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SeedExpectedCostsFromRatingCommand(id), ct);
            return Results.Ok(result);
        });
        ratings.MapPost("/{id:guid}/seed-expected-revenues", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new SeedExpectedRevenuesFromRatingCommand(id), ct);
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
    Guid? SourceId,
    Guid? OrganizationId = null);

public sealed record ConfirmCostRequest(decimal? ConfirmedAmount);

public sealed record ActualizeCostRequest(decimal? ActualAmount);

public sealed record AdjustCostRequest(
    string AdjustmentType,
    decimal DeltaAmount,
    string Reason,
    DateOnly? EffectiveDate);

public sealed record AllocationDetailRequest(
    Guid BillId,
    decimal? BasisValue,
    decimal? ManualOverrideAmount,
    string? OverrideReason);

public sealed record CreateAllocationRequest(
    string AllocationBasis,
    List<AllocationDetailRequest>? Details,
    string? ApplicabilityMode = null,
    Guid? ScopeId = null,
    string? ConditionCode = null);
