using LCMS.Application.Bills.Queries;
using LCMS.Application.OperationalLinks.Commands;
using LCMS.Application.Orders.Commands;
using LCMS.Application.Orders.Queries;
using LCMS.Application.Shipments.Commands;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class OperationalReferenceEndpoints
{
    public static IEndpointRouteBuilder MapOperationalReferenceEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Orders");
        orders.MapPut("/", async (UpsertOrderRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertOrderCommand(
                    body.OrderNo,
                    body.SourceSystem,
                    body.ExternalId,
                    body.ExternalVersion,
                    body.OperationalStatus,
                    body.IsActive ?? true),
                ct);
            return Results.Ok(new { id });
        });
        orders.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListOrdersQuery(), ct);
            return Results.Ok(list);
        });
        orders.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var order = await sender.Send(new GetOrderByIdQuery(id), ct);
            return Results.Ok(order);
        });
        orders.MapPost("/{orderId:guid}/bills/{billId:guid}", async (
            Guid orderId,
            Guid billId,
            ISender sender,
            CancellationToken ct) =>
        {
            var linkId = await sender.Send(new LinkOrderToBillCommand(orderId, billId), ct);
            return Results.Ok(new { id = linkId });
        });

        var shipments = app.MapGroup("/api/shipments").WithTags("Shipments");
        shipments.MapPut("/", async (UpsertShipmentRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertShipmentCommand(
                    body.ShipmentNo,
                    body.SourceSystem,
                    body.ExternalId,
                    body.ExternalVersion,
                    body.OperationalStatus,
                    body.IsActive ?? true),
                ct);
            return Results.Ok(new { id });
        });
        shipments.MapPost("/{shipmentId:guid}/bills/{billId:guid}", async (
            Guid shipmentId,
            Guid billId,
            ISender sender,
            CancellationToken ct) =>
        {
            var linkId = await sender.Send(new LinkBillToShipmentCommand(billId, shipmentId), ct);
            return Results.Ok(new { id = linkId });
        });

        // Alternate path matching kickoff: bill → shipment link
        var bills = app.MapGroup("/api/bills").WithTags("Bills");
        bills.MapPost("/{billId:guid}/shipments/{shipmentId:guid}", async (
            Guid billId,
            Guid shipmentId,
            ISender sender,
            CancellationToken ct) =>
        {
            var linkId = await sender.Send(new LinkBillToShipmentCommand(billId, shipmentId), ct);
            return Results.Ok(new { id = linkId });
        });
        bills.MapGet("/{id:guid}/graph", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var graph = await sender.Send(new GetBillGraphQuery(id), ct);
            return Results.Ok(graph);
        });

        return app;
    }
}

public sealed record UpsertOrderRequest(
    string OrderNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool? IsActive);

public sealed record UpsertShipmentRequest(
    string ShipmentNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool? IsActive);
