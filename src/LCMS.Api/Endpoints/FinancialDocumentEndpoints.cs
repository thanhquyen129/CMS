using LCMS.Application.Bills.Queries;
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

        docs.MapPost("/", async (ReceiveFinancialDocumentRequest body, HttpRequest http, ISender sender, CancellationToken ct) =>
        {
            http.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);
            var billId = await sender.Send(new ResolveBillReferenceQuery(body.BillId), ct);
            var id = await sender.Send(
                new ReceiveFinancialDocumentCommand(
                    body.DocumentType,
                    body.DocumentNo,
                    body.Direction,
                    body.TotalAmount,
                    body.CurrencyCode,
                    body.DocumentDate,
                    body.CounterpartyId,
                    billId,
                    body.Notes,
                    body.SourceSystem,
                    body.ExternalId,
                    idempotencyKey.ToString()),
                ct);
            return Results.Created($"/api/financial-documents/{id}", new { id });
        });

        docs.MapGet("/", async (
            string? documentType,
            string? receiptStatus,
            string? acceptanceStatus,
            string? matchingStatus,
            Guid? billId,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListFinancialDocumentsQuery(
                    documentType,
                    receiptStatus,
                    acceptanceStatus,
                    matchingStatus,
                    billId,
                    page,
                    pageSize),
                ct);
            if (page is null && pageSize is null)
            {
                return Results.Ok(list.Items);
            }

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

        docs.MapPost("/{id:guid}/correct-header", async (
            Guid id,
            CorrectFinancialDocumentHeaderRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new CorrectFinancialDocumentHeaderCommand(
                    id,
                    body.Reason,
                    body.CurrencyCode,
                    body.BillId),
                ct);
            return Results.NoContent();
        });

        docs.MapPost("/{id:guid}/lines", async (
            Guid id,
            AddFinancialDocumentLineRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var lineBillId = await sender.Send(new ResolveBillReferenceQuery(body.BillId), ct);
            var lineId = await sender.Send(
                new AddFinancialDocumentLineCommand(
                    id,
                    body.Amount,
                    body.Description,
                    lineBillId,
                    body.CostTypeCode,
                    body.RevenueTypeCode,
                    body.CurrencyCode),
                ct);
            return Results.Created($"/api/financial-documents/{id}/lines/{lineId}", new { id = lineId });
        });

        docs.MapPut("/{id:guid}/lines/{lineId:guid}", async (
            Guid id,
            Guid lineId,
            UpdateFinancialDocumentLineRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var lineBillId = await sender.Send(new ResolveBillReferenceQuery(body.BillId), ct);
            await sender.Send(
                new UpdateFinancialDocumentLineCommand(
                    id,
                    lineId,
                    body.Amount,
                    body.Description,
                    lineBillId,
                    body.CostTypeCode,
                    body.RevenueTypeCode),
                ct);
            return Results.NoContent();
        });

        docs.MapDelete("/{id:guid}/lines/{lineId:guid}", async (
            Guid id,
            Guid lineId,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new DeleteFinancialDocumentLineCommand(id, lineId), ct);
            return Results.NoContent();
        });

        var matches = app.MapGroup("/api/document-matches").WithTags("DocumentMatches");

        matches.MapPost("/", async (StartDocumentMatchRequest body, HttpRequest http, ISender sender, CancellationToken ct) =>
        {
            http.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);
            var id = await sender.Send(
                new StartDocumentMatchCommand(
                    body.PrimaryDocumentId,
                    body.MatchMethod,
                    body.Notes,
                    body.ToleranceAmount,
                    body.TolerancePercent,
                    idempotencyKey.ToString()),
                ct);
            return Results.Created($"/api/document-matches/{id}", new { id });
        });

        matches.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var match = await sender.Send(new GetDocumentMatchByIdQuery(id), ct);
            return Results.Ok(match);
        });

        matches.MapPost("/{id:guid}/confirm", async (Guid id, HttpRequest http, ISender sender, CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            await sender.Send(new ConfirmDocumentMatchCommand(id, ifMatch.ToString()), ct);
            return Results.NoContent();
        });

        matches.MapPost("/{id:guid}/cancel", async (
            Guid id,
            CancelDocumentMatchRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            await sender.Send(new CancelDocumentMatchCommand(id, body.Reason, ifMatch.ToString()), ct);
            return Results.NoContent();
        });

        matches.MapGet("/{id:guid}/suggestions", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new SuggestMatchCandidatesQuery(id), ct);
            return Results.Ok(list);
        });

        matches.MapPost("/{id:guid}/resolve", async (
            Guid id,
            ResolveDocumentMatchRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            var detailId = await sender.Send(new ResolveDocumentMatchCommand(id, body.SourceLineId, ifMatch.ToString()), ct);
            return Results.Created($"/api/document-matches/{id}/details/{detailId}", new { id = detailId });
        });

        matches.MapPost("/{id:guid}/details", async (
            Guid id,
            AddDocumentMatchDetailRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            var detailId = await sender.Send(
                new AddDocumentMatchDetailCommand(
                    id,
                    body.SourceLineId,
                    body.TargetLineId,
                    body.TargetCostId,
                    body.TargetRevenueId,
                    body.MatchedAmount,
                    ifMatch.ToString()),
                ct);
            return Results.Created($"/api/document-matches/{id}/details/{detailId}", new { id = detailId });
        });

        matches.MapPost("/{id:guid}/details/{detailId:guid}/reverse", async (
            Guid id,
            Guid detailId,
            ReverseDocumentMatchDetailRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            await sender.Send(new ReverseDocumentMatchDetailCommand(id, detailId, body.Reason, ifMatch.ToString()), ct);
            return Results.NoContent();
        });

        matches.MapGet("/{id:guid}/exposure-proposals", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ProposeExposuresFromMatchQuery(id), ct);
            return Results.Ok(list);
        });

        matches.MapPost("/{id:guid}/create-exposures", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new CreateExposuresFromMatchCommand(id), ct);
            return Results.Ok(result);
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
    string? BillId,
    string? Notes,
    string? SourceSystem,
    string? ExternalId);

public sealed record AddFinancialDocumentLineRequest(
    decimal Amount,
    string? Description,
    string? BillId,
    string? CostTypeCode,
    string? RevenueTypeCode,
    string? CurrencyCode);

public sealed record UpdateFinancialDocumentLineRequest(
    decimal Amount,
    string? Description,
    string? BillId,
    string? CostTypeCode,
    string? RevenueTypeCode);

public sealed record CorrectFinancialDocumentHeaderRequest(
    string Reason,
    string? CurrencyCode,
    string? BillId);

public sealed record CancelFinancialDocumentRequest(string Reason, bool Void = false);

public sealed record StartDocumentMatchRequest(
    Guid? PrimaryDocumentId,
    string? MatchMethod,
    string? Notes,
    decimal? ToleranceAmount,
    decimal? TolerancePercent);

public sealed record ResolveDocumentMatchRequest(Guid SourceLineId);

public sealed record AddDocumentMatchDetailRequest(
    Guid SourceLineId,
    Guid? TargetLineId,
    Guid? TargetCostId,
    Guid? TargetRevenueId,
    decimal MatchedAmount);

public sealed record ReverseDocumentMatchDetailRequest(string Reason);

public sealed record CancelDocumentMatchRequest(string Reason);
