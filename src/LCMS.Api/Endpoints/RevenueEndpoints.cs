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
                    body.RecognitionPolicyVersion,
                    body.ActualRevenueOwner),
                ct);
            return Results.Created($"/api/revenues/{id}", new { id });
        });

        revenues.MapGet("/", async (
            Guid? billId,
            string? financialMaturity,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListRevenuesQuery(billId, financialMaturity, page, pageSize),
                ct);
            if (page is null && pageSize is null)
            {
                return Results.Ok(list.Items);
            }

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
            await sender.Send(new ActualizeRevenueCommand(id, body?.ActualAmount, body?.SourceSystem, body?.OverrideReason), ct);
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
            string? reportingCurrency,
            ISender sender,
            CancellationToken ct) =>
        {
            var profitability = await sender.Send(
                new GetBillProfitabilityQuery(id, string.IsNullOrWhiteSpace(view) ? "best" : view, reportingCurrency),
                ct);
            return Results.Ok(profitability);
        });

        revenues.MapPost("/{id:guid}/mappings", async (Guid id, CreateRevenueMappingRequest body, ISender sender, CancellationToken ct) =>
        {
            var mappingId = await sender.Send(
                new CreateRevenueMappingCommand(
                    id,
                    body.AllocationBasis,
                    (body.Details ?? []).Select(d => new RevenueMappingLineInput(d.BillId, d.BasisValue, d.ManualOverrideAmount, d.OverrideReason)).ToList(),
                    body.ApplicabilityMode,
                    body.ScopeId,
                    body.ConditionCode),
                ct);
            return Results.Created($"/api/revenue-mappings/{mappingId}", new { id = mappingId });
        });

        var mappings = app.MapGroup("/api/revenue-mappings").WithTags("Revenues");
        mappings.MapPost("/{id:guid}/finalize", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new FinalizeRevenueMappingCommand(id), ct);
            return Results.NoContent();
        });
        mappings.MapPost("/{id:guid}/cancel", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CancelRevenueMappingCommand(id), ct);
            return Results.NoContent();
        });

        var board = app.MapGroup("/api/profitability").WithTags("Profitability");
        board.MapGet("/bills", async (string? view, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListBillProfitQuery(string.IsNullOrWhiteSpace(view) ? "actual" : view.Trim().ToLowerInvariant(), page, pageSize),
                ct);
            return Results.Ok(list);
        });
        board.MapGet("/groups", async (string? groupBy, string? view, ISender sender, CancellationToken ct) =>
        {
            var rows = await sender.Send(
                new GetProfitabilityGroupsQuery(
                    string.IsNullOrWhiteSpace(groupBy) ? "customer" : groupBy.Trim().ToLowerInvariant(),
                    string.IsNullOrWhiteSpace(view) ? "actual" : view.Trim().ToLowerInvariant()),
                ct);
            return Results.Ok(rows);
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
    string? RecognitionPolicyVersion,
    string? ActualRevenueOwner = null);

public sealed record ConfirmRevenueRequest(decimal? ConfirmedAmount);

public sealed record ActualizeRevenueRequest(decimal? ActualAmount, string? SourceSystem = null, string? OverrideReason = null);

public sealed record CreateRevenueMappingRequest(
    string AllocationBasis,
    List<RevenueMappingLineRequest>? Details,
    string? ApplicabilityMode = null,
    Guid? ScopeId = null,
    string? ConditionCode = null);

public sealed record RevenueMappingLineRequest(
    Guid BillId,
    decimal? BasisValue,
    decimal? ManualOverrideAmount,
    string? OverrideReason);

public sealed record AdjustRevenueRequest(
    string AdjustmentType,
    decimal DeltaAmount,
    string Reason,
    DateOnly? EffectiveDate);
