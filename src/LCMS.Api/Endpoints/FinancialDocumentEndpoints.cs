using LCMS.Application.DocumentMatches.Commands;
using LCMS.Application.DocumentMatches.Queries;
using LCMS.Application.FinancialDocuments.Commands;
using LCMS.Application.FinancialDocuments.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class FinancialDocumentEndpoints
{
    public static IEndpointRouteBuilder MapFinancialDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var docs = app.MapGroup("/api/financial-documents").WithTags("FinancialDocuments");

        docs.MapPost("/", async (ReceiveFinancialDocumentRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new ReceiveFinancialDocumentCommand(
                    body.DocumentType,
                    body.DocumentNo,
                    body.Direction,
                    body.TotalAmount,
                    body.CurrencyCode,
                    body.DocumentDate,
                    body.CounterpartyId,
                    body.BillId,
                    body.Notes,
                    body.SourceSystem,
                    body.ExternalId),
                ct);
            return Results.Created($"/api/financial-documents/{id}", new { id });
        });

        docs.MapGet("/", async (
            string? documentType,
            string? receiptStatus,
            string? acceptanceStatus,
            string? matchingStatus,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListFinancialDocumentsQuery(documentType, receiptStatus, acceptanceStatus, matchingStatus),
                ct);
            return Results.Ok(list);
        });

        docs.MapGet("/open-amounts", async (
            Guid? documentId,
            bool? onlyOpen,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListOpenMatchAmountsQuery(documentId, onlyOpen ?? true),
                ct);
            return Results.Ok(list);
        });

        docs.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var document = await sender.Send(new GetFinancialDocumentByIdQuery(id), ct);
            return Results.Ok(document);
        });

        docs.MapPost("/{id:guid}/accept", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new AcceptFinancialDocumentCommand(id), ct);
            return Results.NoContent();
        });

        docs.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelFinancialDocumentRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new CancelFinancialDocumentCommand(id, body.Reason, body.Void), ct);
            return Results.NoContent();
        });

        docs.MapPost("/{id:guid}/lines", async (
            Guid id,
            AddFinancialDocumentLineRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var lineId = await sender.Send(
                new AddFinancialDocumentLineCommand(
                    id,
                    body.Amount,
                    body.Description,
                    body.BillId,
                    body.CostTypeCode,
                    body.RevenueTypeCode,
                    body.CurrencyCode),
                ct);
            return Results.Created($"/api/financial-documents/{id}/lines/{lineId}", new { id = lineId });
        });

        var matches = app.MapGroup("/api/document-matches").WithTags("DocumentMatches");

        matches.MapPost("/", async (StartDocumentMatchRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new StartDocumentMatchCommand(
                    body.PrimaryDocumentId,
                    body.MatchMethod,
                    body.Notes,
                    body.ToleranceAmount,
                    body.TolerancePercent),
                ct);
            return Results.Created($"/api/document-matches/{id}", new { id });
        });

        matches.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var match = await sender.Send(new GetDocumentMatchByIdQuery(id), ct);
            return Results.Ok(match);
        });

        matches.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelDocumentMatchRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new CancelDocumentMatchCommand(id, body.Reason), ct);
            return Results.NoContent();
        });

        matches.MapPost("/{id:guid}/details", async (
            Guid id,
            AddDocumentMatchDetailRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var detailId = await sender.Send(
                new AddDocumentMatchDetailCommand(
                    id,
                    body.SourceLineId,
                    body.TargetLineId,
                    body.TargetCostId,
                    body.TargetRevenueId,
                    body.MatchedAmount),
                ct);
            return Results.Created($"/api/document-matches/{id}/details/{detailId}", new { id = detailId });
        });

        matches.MapPost("/{id:guid}/details/{detailId:guid}/reverse", async (
            Guid id,
            Guid detailId,
            ReverseDocumentMatchDetailRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ReverseDocumentMatchDetailCommand(id, detailId, body.Reason), ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record ReceiveFinancialDocumentRequest(
    string DocumentType,
    string DocumentNo,
    string Direction,
    decimal TotalAmount,
    string CurrencyCode,
    DateOnly? DocumentDate,
    Guid? CounterpartyId,
    Guid? BillId,
    string? Notes,
    string? SourceSystem,
    string? ExternalId);

public sealed record AddFinancialDocumentLineRequest(
    decimal Amount,
    string? Description,
    Guid? BillId,
    string? CostTypeCode,
    string? RevenueTypeCode,
    string? CurrencyCode);

public sealed record CancelFinancialDocumentRequest(string Reason, bool Void = false);

public sealed record StartDocumentMatchRequest(
    Guid? PrimaryDocumentId,
    string? MatchMethod,
    string? Notes,
    decimal? ToleranceAmount,
    decimal? TolerancePercent);

public sealed record AddDocumentMatchDetailRequest(
    Guid SourceLineId,
    Guid? TargetLineId,
    Guid? TargetCostId,
    Guid? TargetRevenueId,
    decimal MatchedAmount);

public sealed record ReverseDocumentMatchDetailRequest(string Reason);

public sealed record CancelDocumentMatchRequest(string Reason);
