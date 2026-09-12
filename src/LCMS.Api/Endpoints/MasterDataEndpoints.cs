using LCMS.Application.BusinessParties.Commands;
using LCMS.Application.BusinessParties.Queries;
using LCMS.Application.Currencies.Commands;
using LCMS.Application.Currencies.Queries;
using LCMS.Application.Fx.Commands;
using LCMS.Application.Fx.Queries;
using LCMS.Application.Organizations.Commands;
using LCMS.Application.Organizations.Queries;
using LCMS.Application.PartyRoles.Commands;
using LCMS.Application.PartyRoles.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class MasterDataEndpoints
{
    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var orgs = app.MapGroup("/api/organizations").WithTags("Organizations");
        orgs.MapPost("/", async (CreateOrganizationRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateOrganizationCommand(body.Code, body.Name, body.ParentId),
                ct);
            return Results.Created($"/api/organizations/{id}", new { id });
        });
        orgs.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListOrganizationsQuery(), ct);
            return Results.Ok(list);
        });
        orgs.MapGet("/tree", async (ISender sender, CancellationToken ct) =>
        {
            var tree = await sender.Send(new GetOrganizationTreeQuery(), ct);
            return Results.Ok(tree);
        });
        orgs.MapGet("/{id:guid}/children", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var children = await sender.Send(new ListOrganizationChildrenQuery(id), ct);
            return Results.Ok(children);
        });
        orgs.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var org = await sender.Send(new GetOrganizationByIdQuery(id), ct);
            return Results.Ok(org);
        });
        orgs.MapPut("/{id:guid}", async (
            Guid id,
            UpdateOrganizationRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpdateOrganizationCommand(id, body.Name, body.ParentId, body.IsActive),
                ct);
            return Results.NoContent();
        });
        orgs.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SoftDeleteOrganizationCommand(id), ct);
            return Results.NoContent();
        });

        var parties = app.MapGroup("/api/business-parties").WithTags("BusinessParties");
        parties.MapPost("/", async (CreateBusinessPartyRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateBusinessPartyCommand(body.Code, body.Name), ct);
            return Results.Created($"/api/business-parties/{id}", new { id });
        });
        parties.MapGet("/", async (ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListBusinessPartiesQuery(), ct);
            return Results.Ok(list);
        });
        parties.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var party = await sender.Send(new GetBusinessPartyByIdQuery(id), ct);
            return Results.Ok(party);
        });
        parties.MapPut("/{id:guid}", async (
            Guid id,
            UpdateBusinessPartyRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new UpdateBusinessPartyCommand(id, body.Name, body.IsActive), ct);
            return Results.NoContent();
        });
        parties.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SoftDeleteBusinessPartyCommand(id), ct);
            return Results.NoContent();
        });
        parties.MapPost("/{id:guid}/roles", async (
            Guid id,
            AssignPartyRoleRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var roleId = await sender.Send(new AssignPartyRoleCommand(id, body.RoleCode), ct);
            return Results.Created($"/api/business-parties/{id}/roles/{body.RoleCode}", new { id = roleId });
        });
        parties.MapGet("/{id:guid}/roles", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListPartyRolesQuery(id), ct);
            return Results.Ok(list);
        });
        parties.MapDelete("/{id:guid}/roles/{roleCode}", async (
            Guid id,
            string roleCode,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new RevokePartyRoleCommand(id, roleCode), ct);
            return Results.NoContent();
        });

        var currencies = app.MapGroup("/api/currencies").WithTags("Currencies");
        currencies.MapGet("/", async (bool? activeOnly, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListCurrenciesQuery(activeOnly), ct);
            return Results.Ok(list);
        });
        currencies.MapGet("/{code}", async (string code, ISender sender, CancellationToken ct) =>
        {
            var currency = await sender.Send(new GetCurrencyByCodeQuery(code), ct);
            return Results.Ok(currency);
        });
        currencies.MapPut("/", async (UpsertCurrencyRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertCurrencyCommand(body.Code, body.Name, body.DecimalPlaces, body.IsActive),
                ct);
            return Results.Ok(new { id });
        });

        var fxRates = app.MapGroup("/api/fx-rates").WithTags("FxRates");
        fxRates.MapGet("/", async (
            string? fromCurrencyCode,
            string? toCurrencyCode,
            DateOnly? fromDate,
            DateOnly? toDate,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListFxRatesQuery(fromCurrencyCode, toCurrencyCode, fromDate, toDate),
                ct);
            return Results.Ok(list);
        });
        fxRates.MapGet("/resolve", async (
            string fromCurrencyCode,
            string toCurrencyCode,
            DateOnly asOf,
            ISender sender,
            CancellationToken ct) =>
        {
            var row = await sender.Send(
                new ResolveFxRateQuery(fromCurrencyCode, toCurrencyCode, asOf),
                ct);
            return row is null ? Results.NotFound() : Results.Ok(row);
        });
        fxRates.MapPost("/", async (UpsertFxRateRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertFxRateCommand(
                    body.FromCurrencyCode,
                    body.ToCurrencyCode,
                    body.RateDate,
                    body.Rate,
                    body.Source,
                    body.Version,
                    body.Note),
                ct);
            return Results.Created($"/api/fx-rates/{id}", new { id });
        });
        fxRates.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SoftDeleteFxRateCommand(id), ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record UpsertFxRateRequest(
    string FromCurrencyCode,
    string ToCurrencyCode,
    DateOnly RateDate,
    decimal Rate,
    string? Source,
    int? Version,
    string? Note);

public sealed record CreateOrganizationRequest(string Code, string Name, Guid? ParentId);
public sealed record UpdateOrganizationRequest(string Name, Guid? ParentId, bool IsActive);
public sealed record CreateBusinessPartyRequest(string Code, string Name);
public sealed record UpdateBusinessPartyRequest(string Name, bool IsActive);
public sealed record AssignPartyRoleRequest(string RoleCode);
public sealed record UpsertCurrencyRequest(string Code, string Name, int DecimalPlaces, bool IsActive);
