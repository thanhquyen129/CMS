using LCMS.Application.Settlements.Commands;
using LCMS.Application.Settlements.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class SettlementEndpoints
{
    public static IEndpointRouteBuilder MapSettlementEndpoints(this IEndpointRouteBuilder app)
    {
        var payments = app.MapGroup("/api/payments").WithTags("Payments");

        payments.MapPost("/", async (CreatePaymentRequest body, HttpRequest http, ISender sender, CancellationToken ct) =>
        {
            http.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);
            var id = await sender.Send(
                new CreatePaymentCommand(
                    body.Amount,
                    body.CurrencyCode,
                    body.ValueDate,
                    body.CounterpartyId,
                    body.BillId,
                    body.ReferenceNo,
                    body.Notes,
                    idempotencyKey.ToString()),
                ct);
            return Results.Created($"/api/payments/{id}", new { id });
        });

        payments.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListPaymentsQuery(), ct);
            return Results.Ok(list);
        });

        payments.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetPaymentByIdQuery(id), ct);
            return Results.Ok(item);
        });

        payments.MapPost("/{id:guid}/allocations", async (
            Guid id,
            AllocatePaymentRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var allocationId = await sender.Send(
                new AllocatePaymentCommand(id, body.AccountsPayableId, body.Amount, body.Notes),
                ct);
            return Results.Created($"/api/payment-allocations/{allocationId}", new { id = allocationId });
        });

        var paymentAllocations = app.MapGroup("/api/payment-allocations").WithTags("PaymentAllocations");

        paymentAllocations.MapPost("/{id:guid}/finalize", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new FinalizePaymentAllocationCommand(id), ct);
            return Results.NoContent();
        });

        paymentAllocations.MapPost("/{id:guid}/reverse", async (
            Guid id,
            ReverseAllocationRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ReversePaymentAllocationCommand(id, body.Reason), ct);
            return Results.NoContent();
        });

        var collections = app.MapGroup("/api/collections").WithTags("Collections");

        collections.MapPost("/", async (CreateCollectionRequest body, HttpRequest http, ISender sender, CancellationToken ct) =>
        {
            http.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);
            var id = await sender.Send(
                new CreateCollectionCommand(
                    body.Amount,
                    body.CurrencyCode,
                    body.ValueDate,
                    body.CounterpartyId,
                    body.BillId,
                    body.ReferenceNo,
                    body.Notes,
                    idempotencyKey.ToString()),
                ct);
            return Results.Created($"/api/collections/{id}", new { id });
        });

        collections.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListCollectionsQuery(), ct);
            return Results.Ok(list);
        });

        collections.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetCollectionByIdQuery(id), ct);
            return Results.Ok(item);
        });

        collections.MapPost("/{id:guid}/allocations", async (
            Guid id,
            AllocateCollectionRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var allocationId = await sender.Send(
                new AllocateCollectionCommand(id, body.AccountsReceivableId, body.Amount, body.Notes),
                ct);
            return Results.Created($"/api/collection-allocations/{allocationId}", new { id = allocationId });
        });

        var collectionAllocations = app.MapGroup("/api/collection-allocations").WithTags("CollectionAllocations");

        collectionAllocations.MapPost("/{id:guid}/finalize", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new FinalizeCollectionAllocationCommand(id), ct);
            return Results.NoContent();
        });

        collectionAllocations.MapPost("/{id:guid}/reverse", async (
            Guid id,
            ReverseAllocationRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ReverseCollectionAllocationCommand(id, body.Reason), ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record CreatePaymentRequest(
    decimal Amount,
    string CurrencyCode,
    DateOnly? ValueDate,
    Guid? CounterpartyId,
    Guid? BillId,
    string? ReferenceNo,
    string? Notes);

public sealed record CreateCollectionRequest(
    decimal Amount,
    string CurrencyCode,
    DateOnly? ValueDate,
    Guid? CounterpartyId,
    Guid? BillId,
    string? ReferenceNo,
    string? Notes);

public sealed record AllocatePaymentRequest(Guid AccountsPayableId, decimal Amount, string? Notes);

public sealed record AllocateCollectionRequest(Guid AccountsReceivableId, decimal Amount, string? Notes);

public sealed record ReverseAllocationRequest(string Reason);
