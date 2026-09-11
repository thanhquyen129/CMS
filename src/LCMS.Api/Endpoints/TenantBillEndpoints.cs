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
        bills.MapGet("/", async (string? q, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListBillsQuery(q), ct);
            return Results.Ok(list);
        });
        bills.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var bill = await sender.Send(new GetBillByIdQuery(id), ct);
            return Results.Ok(bill);
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
