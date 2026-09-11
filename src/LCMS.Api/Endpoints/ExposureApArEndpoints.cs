using LCMS.Application.Exposures.Commands;
using LCMS.Application.Exposures.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class ExposureApArEndpoints
{
    public static IEndpointRouteBuilder MapExposureApArEndpoints(this IEndpointRouteBuilder app)
    {
        var payableExposures = app.MapGroup("/api/payable-exposures").WithTags("PayableExposures");

        payableExposures.MapPost("/", async (CreatePayableExposureRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreatePayableExposureCommand(
                    body.Amount,
                    body.CurrencyCode,
                    body.EffectiveDate,
                    body.DueDate,
                    body.BillId,
                    body.CounterpartyId,
                    body.CostId,
                    body.FinancialDocumentId,
                    body.Notes,
                    body.SourceType,
                    body.SourceId),
                ct);
            return Results.Created($"/api/payable-exposures/{id}", new { id });
        });

        payableExposures.MapGet("/", async (string? status, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListPayableExposuresQuery(status), ct);
            return Results.Ok(list);
        });

        payableExposures.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetPayableExposureByIdQuery(id), ct);
            return Results.Ok(item);
        });

        payableExposures.MapPost("/{id:guid}/recognize", async (
            Guid id,
            RecognizeExposureRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var apId = await sender.Send(
                new RecognizePayableExposureCommand(id, body.Amount, body.DueDate, body.Notes),
                ct);
            return Results.Created($"/api/accounts-payable/{apId}", new { id = apId });
        });

        var receivableExposures = app.MapGroup("/api/receivable-exposures").WithTags("ReceivableExposures");

        receivableExposures.MapPost("/", async (
            CreateReceivableExposureRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateReceivableExposureCommand(
                    body.Amount,
                    body.CurrencyCode,
                    body.EffectiveDate,
                    body.DueDate,
                    body.BillId,
                    body.CounterpartyId,
                    body.RevenueId,
                    body.FinancialDocumentId,
                    body.Notes,
                    body.SourceType,
                    body.SourceId),
                ct);
            return Results.Created($"/api/receivable-exposures/{id}", new { id });
        });

        receivableExposures.MapGet("/", async (string? status, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListReceivableExposuresQuery(status), ct);
            return Results.Ok(list);
        });

        receivableExposures.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetReceivableExposureByIdQuery(id), ct);
            return Results.Ok(item);
        });

        receivableExposures.MapPost("/{id:guid}/recognize", async (
            Guid id,
            RecognizeExposureRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var arId = await sender.Send(
                new RecognizeReceivableExposureCommand(id, body.Amount, body.DueDate, body.Notes),
                ct);
            return Results.Created($"/api/accounts-receivable/{arId}", new { id = arId });
        });

        var ap = app.MapGroup("/api/accounts-payable").WithTags("AccountsPayable");

        ap.MapGet("/", async (string? settlementStatus, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListAccountsPayableQuery(settlementStatus), ct);
            return Results.Ok(list);
        });

        ap.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetAccountsPayableByIdQuery(id), ct);
            return Results.Ok(item);
        });

        ap.MapPost("/{id:guid}/adjust", async (
            Guid id,
            AdjustApArRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new AdjustAccountsPayableCommand(id, body.DeltaAmount, body.Reason), ct);
            return Results.NoContent();
        });

        var ar = app.MapGroup("/api/accounts-receivable").WithTags("AccountsReceivable");

        ar.MapGet("/", async (string? settlementStatus, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListAccountsReceivableQuery(settlementStatus), ct);
            return Results.Ok(list);
        });

        ar.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetAccountsReceivableByIdQuery(id), ct);
            return Results.Ok(item);
        });

        ar.MapPost("/{id:guid}/adjust", async (
            Guid id,
            AdjustApArRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new AdjustAccountsReceivableCommand(id, body.DeltaAmount, body.Reason), ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record CreatePayableExposureRequest(
    decimal Amount,
    string CurrencyCode,
    DateOnly? EffectiveDate,
    DateOnly? DueDate,
    Guid? BillId,
    Guid? CounterpartyId,
    Guid? CostId,
    Guid? FinancialDocumentId,
    string? Notes,
    string? SourceType,
    Guid? SourceId);

public sealed record CreateReceivableExposureRequest(
    decimal Amount,
    string CurrencyCode,
    DateOnly? EffectiveDate,
    DateOnly? DueDate,
    Guid? BillId,
    Guid? CounterpartyId,
    Guid? RevenueId,
    Guid? FinancialDocumentId,
    string? Notes,
    string? SourceType,
    Guid? SourceId);

/// <summary>Recognize body — deliberately has no Outstanding field (C-015).</summary>
public sealed record RecognizeExposureRequest(
    decimal Amount,
    DateOnly? DueDate,
    string? Notes);

public sealed record AdjustApArRequest(decimal DeltaAmount, string Reason);
