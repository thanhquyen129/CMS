using LCMS.Application.ReferenceMasters;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class ReferenceMasterEndpoints
{
    public static IEndpointRouteBuilder MapReferenceMasterEndpoints(this IEndpointRouteBuilder app)
    {
        var locations = app.MapGroup("/api/locations").WithTags("Locations");
        locations.MapGet("/", async (string? q, string? locationType, bool? activeOnly, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListLocationsQuery(q, locationType, activeOnly ?? false), ct);
            return Results.Ok(list);
        });
        locations.MapGet("/resolve", async (string code, ISender sender, CancellationToken ct) =>
        {
            var row = await sender.Send(new ResolveLocationQuery(code), ct);
            return Results.Ok(row);
        });
        locations.MapPut("/", async (UpsertLocationRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new UpsertLocationCommand(
                body.Code,
                body.Name,
                body.LocationType,
                body.CountryCode,
                body.Subdivision,
                body.City,
                body.IataCode,
                body.Unlocode,
                body.TerminalCode,
                body.IsActive ?? true,
                body.Aliases?.Select(a => new LocationAliasInput(a.AliasCode, a.SourceSystem)).ToList()), ct);
            return Results.Ok(new { id });
        });

        var routes = app.MapGroup("/api/routes").WithTags("Routes");
        routes.MapGet("/", async (bool? activeOnly, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListRoutesQuery(activeOnly ?? false), ct);
            return Results.Ok(list);
        });
        routes.MapPut("/", async (UpsertRouteRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new UpsertRouteCommand(
                body.Code,
                body.Name,
                body.OriginLocationId,
                body.DestinationLocationId,
                body.TransportModeCode,
                body.ServiceTypeCode,
                body.IsActive ?? true,
                body.IntermediateLocationIds), ct);
            return Results.Ok(new { id });
        });

        var commodities = app.MapGroup("/api/commodities").WithTags("Commodities");
        commodities.MapGet("/", async (bool? activeOnly, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListCommoditiesQuery(activeOnly ?? false), ct);
            return Results.Ok(list);
        });
        commodities.MapPut("/", async (UpsertCommodityRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new UpsertCommodityCommand(
                body.Code,
                body.Name,
                body.Category,
                body.ParentId,
                body.IsDangerousGoods ?? false,
                body.IsTemperatureControlled ?? false,
                body.IsOversize ?? false,
                body.IsOverweight ?? false,
                body.IsHighValue ?? false,
                body.SpecialHandling,
                body.IsActive ?? true), ct);
            return Results.Ok(new { id });
        });

        var snapshots = app.MapGroup("/api/party-snapshots").WithTags("PartySnapshots");
        snapshots.MapGet("/", async (string objectType, Guid objectId, bool? currentOnly, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListPartySnapshotsQuery(objectType, objectId, currentOnly ?? true), ct);
            return Results.Ok(list);
        });
        snapshots.MapPost("/", async (CapturePartySnapshotRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CapturePartySnapshotCommand(
                body.ObjectType,
                body.ObjectId,
                body.RoleCode,
                body.PartyId,
                body.WalkInName,
                body.Phone,
                body.Email,
                body.Address), ct);
            return Results.Ok(new { id });
        });

        var policy = app.MapGroup("/api/bill-party-policy").WithTags("BillPartyPolicy");
        policy.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetBillPartyPolicyQuery(), ct)));
        policy.MapPut("/", async (SaveBillPartyPolicyRequest body, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SaveBillPartyPolicyCommand(body.RequiredRoles ?? [], body.AllowWalkIn ?? false), ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record LocationAliasRequest(string AliasCode, string? SourceSystem);

public sealed record UpsertLocationRequest(
    string Code,
    string Name,
    string LocationType,
    string? CountryCode,
    string? Subdivision,
    string? City,
    string? IataCode,
    string? Unlocode,
    string? TerminalCode,
    bool? IsActive,
    IReadOnlyList<LocationAliasRequest>? Aliases);

public sealed record UpsertRouteRequest(
    string Code,
    string Name,
    Guid OriginLocationId,
    Guid DestinationLocationId,
    string? TransportModeCode,
    string? ServiceTypeCode,
    bool? IsActive,
    IReadOnlyList<Guid>? IntermediateLocationIds);

public sealed record UpsertCommodityRequest(
    string Code,
    string Name,
    string? Category,
    Guid? ParentId,
    bool? IsDangerousGoods,
    bool? IsTemperatureControlled,
    bool? IsOversize,
    bool? IsOverweight,
    bool? IsHighValue,
    string? SpecialHandling,
    bool? IsActive);

public sealed record CapturePartySnapshotRequest(
    string ObjectType,
    Guid ObjectId,
    string RoleCode,
    Guid? PartyId,
    string? WalkInName,
    string? Phone,
    string? Email,
    string? Address);

public sealed record SaveBillPartyPolicyRequest(IReadOnlyList<string>? RequiredRoles, bool? AllowWalkIn);
