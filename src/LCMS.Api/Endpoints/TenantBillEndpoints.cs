using LCMS.Application.Bills.Commands;
using LCMS.Application.Bills.Queries;
using LCMS.Application.Tenants.Commands;
using LCMS.Application.Tenants.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class TenantBillEndpoints
{
    public static IEndpointRouteBuilder MapTenantBillEndpoints(this IEndpointRouteBuilder app)
    {
        var tenants = app.MapGroup("/api/tenants").WithTags("Tenants");
        tenants.MapPost("/", async (CreateTenantRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateTenantCommand(body.Code, body.Name), ct);
            return Results.Created($"/api/tenants/{id}", new { id });
        });
        tenants.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var tenant = await sender.Send(new GetTenantByIdQuery(id), ct);
            return Results.Ok(tenant);
        });

        var bills = app.MapGroup("/api/bills").WithTags("Bills");
        bills.MapPost("/", async (CreateBillRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateBillCommand(
                    body.BillNo,
                    body.BillType,
                    body.SourceSystem,
                    body.ExternalId,
                    body.OrganizationId),
                ct);
            return Results.Created($"/api/bills/{id}", new { id });
        });
        bills.MapGet("/", async (string? q, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListBillsQuery(q, page, pageSize), ct);
            // Legacy callers (no paging) expect a bare array.
            if (page is null && pageSize is null)
            {
                return Results.Ok(list.Items);
            }

            return Results.Ok(list);
        });
        bills.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var bill = await sender.Send(new GetBillByIdQuery(id), ct);
            return Results.Ok(bill);
        });
        bills.MapGet("/{id:guid}/financial-view", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var view = await sender.Send(new GetBillFinancialViewQuery(id), ct);
            return Results.Ok(view);
        });
        bills.MapPatch("/{id:guid}/context", async (
            Guid id,
            UpdateBillContextRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpdateBillContextCommand(
                    id,
                    body.CustomerPartyId,
                    body.RouteCode,
                    body.EtdAt,
                    body.EtaAt,
                    body.AssignedUserId,
                    body.Description,
                    body.InternalNote),
                ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record CreateTenantRequest(string Code, string Name);

public sealed record CreateBillRequest(
    string BillNo,
    string BillType,
    string? SourceSystem,
    string? ExternalId,
    Guid? OrganizationId = null);

public sealed record UpdateBillContextRequest(
    Guid? CustomerPartyId,
    string? RouteCode,
    DateTimeOffset? EtdAt,
    DateTimeOffset? EtaAt,
    Guid? AssignedUserId,
    string? Description,
    string? InternalNote);
