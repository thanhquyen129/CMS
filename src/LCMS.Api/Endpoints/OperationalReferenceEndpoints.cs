using LCMS.Application.Bills.Queries;
using LCMS.Application.OperationalLinks.Commands;
using LCMS.Application.OperationalReferences;
using LCMS.Application.Orders.Commands;
using LCMS.Application.Orders.Queries;
using LCMS.Application.Search.Queries;
using LCMS.Application.Shipments.Commands;
using LCMS.Application.Shipments.Queries;
using LCMS.Application.TransportLegs.Commands;
using LCMS.Application.TransportLegs.Queries;
using LCMS.Application.TransportMovements.Commands;
using LCMS.Application.TransportMovements.Queries;
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
                    body.IsActive ?? true,
                    body.CustomerPartyId,
                    body.AssignedUserId,
                    body.TransportMode,
                    body.OriginCode,
                    body.DestinationCode,
                    body.RouteCode,
                    body.EtdAt,
                    body.EtaAt,
                    body.CustomerReference,
                    body.Description,
                    body.Context,
                    ApplyContext: body.ApplyContext
                        || body.CustomerPartyId is not null
                        || body.AssignedUserId is not null
                        || body.TransportMode is not null
                        || body.OriginCode is not null
                        || body.DestinationCode is not null
                        || body.RouteCode is not null
                        || body.EtdAt is not null
                        || body.EtaAt is not null
                        || body.CustomerReference is not null
                        || body.Description is not null
                        || body.Context is not null),
                ct);
            return Results.Ok(new { id });
        });
        orders.MapGet("/", async (string? q, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListOrdersQuery(q), ct);
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
                    body.IsActive ?? true,
                    body.AssignedUserId,
                    body.TransportMode,
                    body.OriginCode,
                    body.DestinationCode,
                    body.RouteCode,
                    body.EtdAt,
                    body.EtaAt,
                    body.CustomerReference,
                    body.Description,
                    body.Context,
                    ApplyContext: body.ApplyContext
                        || body.AssignedUserId is not null
                        || body.TransportMode is not null
                        || body.OriginCode is not null
                        || body.DestinationCode is not null
                        || body.RouteCode is not null
                        || body.EtdAt is not null
                        || body.EtaAt is not null
                        || body.CustomerReference is not null
                        || body.Description is not null
                        || body.Context is not null),
                ct);
            return Results.Ok(new { id });
        });
        shipments.MapGet("/", async (string? q, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListShipmentsQuery(q), ct);
            return Results.Ok(list);
        });
        shipments.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var shipment = await sender.Send(new GetShipmentByIdQuery(id), ct);
            return Results.Ok(shipment);
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

        var legs = app.MapGroup("/api/transport-legs").WithTags("TransportLegs");
        legs.MapPut("/", async (UpsertTransportLegRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertTransportLegCommand(
                    body.LegNo,
                    body.ShipmentId,
                    body.SourceSystem,
                    body.ExternalId,
                    body.ExternalVersion,
                    body.OperationalStatus,
                    body.IsActive ?? true),
                ct);
            return Results.Ok(new { id });
        });
        legs.MapGet("/", async (string? q, Guid? shipmentId, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListTransportLegsQuery(q, shipmentId), ct);
            return Results.Ok(list);
        });
        legs.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var leg = await sender.Send(new GetTransportLegByIdQuery(id), ct);
            return Results.Ok(leg);
        });
        legs.MapPost("/{legId:guid}/bills/{billId:guid}", async (
            Guid legId,
            Guid billId,
            ISender sender,
            CancellationToken ct) =>
        {
            var linkId = await sender.Send(new LinkBillToLegCommand(billId, legId), ct);
            return Results.Ok(new { id = linkId });
        });
        legs.MapPost("/{legId:guid}/movements/{movementId:guid}", async (
            Guid legId,
            Guid movementId,
            ISender sender,
            CancellationToken ct) =>
        {
            var linkId = await sender.Send(new LinkLegToMovementCommand(legId, movementId), ct);
            return Results.Ok(new { id = linkId });
        });

        var movements = app.MapGroup("/api/transport-movements").WithTags("TransportMovements");
        movements.MapPut("/", async (UpsertTransportMovementRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertTransportMovementCommand(
                    body.MovementNo,
                    body.SourceSystem,
                    body.ExternalId,
                    body.ExternalVersion,
                    body.OperationalStatus,
                    body.IsActive ?? true),
                ct);
            return Results.Ok(new { id });
        });
        movements.MapGet("/", async (string? q, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListTransportMovementsQuery(q), ct);
            return Results.Ok(list);
        });
        movements.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var movement = await sender.Send(new GetTransportMovementByIdQuery(id), ct);
            return Results.Ok(movement);
        });
        movements.MapPost("/{movementId:guid}/bills/{billId:guid}", async (
            Guid movementId,
            Guid billId,
            ISender sender,
            CancellationToken ct) =>
        {
            var linkId = await sender.Send(new LinkBillToMovementCommand(billId, movementId), ct);
            return Results.Ok(new { id = linkId });
        });

        // Alternate path matching kickoff: bill → shipment / leg / movement link
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
        bills.MapPost("/{billId:guid}/legs/{legId:guid}", async (
            Guid billId,
            Guid legId,
            ISender sender,
            CancellationToken ct) =>
        {
            var linkId = await sender.Send(new LinkBillToLegCommand(billId, legId), ct);
            return Results.Ok(new { id = linkId });
        });
        bills.MapPost("/{billId:guid}/movements/{movementId:guid}", async (
            Guid billId,
            Guid movementId,
            ISender sender,
            CancellationToken ct) =>
        {
            var linkId = await sender.Send(new LinkBillToMovementCommand(billId, movementId), ct);
            return Results.Ok(new { id = linkId });
        });
        bills.MapGet("/{id:guid}/graph", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var graph = await sender.Send(new GetBillGraphQuery(id), ct);
            return Results.Ok(graph);
        });

        var search = app.MapGroup("/api/search").WithTags("Search");
        search.MapGet("/operational", async (string? q, ISender sender, CancellationToken ct) =>
        {
            var hits = await sender.Send(new SearchOperationalQuery(q ?? string.Empty), ct);
            return Results.Ok(hits);
        });
        app.MapGet("/api/search", async (string? q, ISender sender, CancellationToken ct) =>
        {
            var hits = await sender.Send(new SearchGlobalQuery(q ?? string.Empty), ct);
            return Results.Ok(hits);
        }).WithTags("Search");

        return app;
    }
}

public sealed record UpsertOrderRequest(
    string OrderNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool? IsActive,
    Guid? CustomerPartyId = null,
    Guid? AssignedUserId = null,
    string? TransportMode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? RouteCode = null,
    DateTimeOffset? EtdAt = null,
    DateTimeOffset? EtaAt = null,
    string? CustomerReference = null,
    string? Description = null,
    OperationalContextDocument? Context = null,
    bool ApplyContext = false);

public sealed record UpsertShipmentRequest(
    string ShipmentNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool? IsActive,
    Guid? AssignedUserId = null,
    string? TransportMode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? RouteCode = null,
    DateTimeOffset? EtdAt = null,
    DateTimeOffset? EtaAt = null,
    string? CustomerReference = null,
    string? Description = null,
    OperationalContextDocument? Context = null,
    bool ApplyContext = false);

public sealed record UpsertTransportLegRequest(
    string LegNo,
    Guid ShipmentId,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool? IsActive);

public sealed record UpsertTransportMovementRequest(
    string MovementNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool? IsActive);
