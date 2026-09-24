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
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            var apId = await sender.Send(
                new RecognizePayableExposureCommand(id, body.Amount, body.DueDate, body.Notes, idempotencyKey.ToString(), ifMatch.ToString()),
                ct);
            return Results.Created($"/api/accounts-payable/{apId}", new { id = apId });
        });

        payableExposures.MapPost("/{id:guid}/link-document", async (
            Guid id,
            LinkDocumentRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new LinkDocumentToPayableExposureCommand(id, body.FinancialDocumentId), ct);
            return Results.NoContent();
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
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey);
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            var arId = await sender.Send(
                new RecognizeReceivableExposureCommand(id, body.Amount, body.DueDate, body.Notes, idempotencyKey.ToString(), ifMatch.ToString()),
                ct);
            return Results.Created($"/api/accounts-receivable/{arId}", new { id = arId });
        });

        receivableExposures.MapPost("/{id:guid}/link-document", async (
            Guid id,
            LinkDocumentRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new LinkDocumentToReceivableExposureCommand(id, body.FinancialDocumentId), ct);
            return Results.NoContent();
        });

        var ap = app.MapGroup("/api/accounts-payable").WithTags("AccountsPayable");

        ap.MapGet("/aging", async (
            DateOnly? asOf,
            Guid? counterpartyId,
            string? currencyCode,
            bool? includeSettled,
            ISender sender,
            CancellationToken ct) =>
        {
            var report = await sender.Send(
                new GetAccountsPayableAgingQuery(asOf, counterpartyId, currencyCode, includeSettled ?? false),
                ct);
            return Results.Ok(report);
        });

        ap.MapGet("/", async (string? settlementStatus, DateOnly? asOf, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListAccountsPayableQuery(settlementStatus, asOf), ct);
            return Results.Ok(list);
        });

        ap.MapGet("/{id:guid}", async (Guid id, DateOnly? asOf, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetAccountsPayableByIdQuery(id, asOf), ct);
            return Results.Ok(item);
        });

        ap.MapPost("/{id:guid}/adjust", async (
            Guid id,
            AdjustApArRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            var adjId = await sender.Send(new AdjustAccountsPayableCommand(id, body.DeltaAmount, body.Reason, ifMatch.ToString()), ct);
            return Results.Created($"/api/accounts-payable/{id}/adjustments/{adjId}", new { id = adjId });
        });

        ap.MapGet("/{id:guid}/adjustments", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListAccountsPayableAdjustmentsQuery(id), ct);
            return Results.Ok(list);
        });

        ap.MapPost("/{id:guid}/reverse-recognize", async (
            Guid id,
            ReverseRecognizeRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            await sender.Send(new ReverseRecognizeAccountsPayableCommand(id, body.Reason, ifMatch.ToString()), ct);
            return Results.NoContent();
        });

        ap.MapPost("/{id:guid}/write-off", async (
            Guid id,
            WriteOffRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            var result = await sender.Send(
                new LCMS.Application.Settlements.Commands.WriteOffAccountsPayableCommand(
                    id, body.Amount, body.Reason, ifMatch.ToString()),
                ct);
            if (!result.AppliedImmediately)
            {
                return Results.Json(
                    new
                    {
                        requiresApproval = true,
                        approvalId = result.ApprovalId,
                        requiredLevel = result.RequiredLevel,
                        message = "Số xóa nợ vượt trần áp dụng ngay; đã gửi yêu cầu phê duyệt. Xóa nợ chỉ ghi khi được duyệt."
                    },
                    statusCode: StatusCodes.Status202Accepted);
            }

            return Results.NoContent();
        });

        var ar = app.MapGroup("/api/accounts-receivable").WithTags("AccountsReceivable");

        ar.MapGet("/aging", async (
            DateOnly? asOf,
            Guid? counterpartyId,
            string? currencyCode,
            bool? includeSettled,
            ISender sender,
            CancellationToken ct) =>
        {
            var report = await sender.Send(
                new GetAccountsReceivableAgingQuery(asOf, counterpartyId, currencyCode, includeSettled ?? false),
                ct);
            return Results.Ok(report);
        });

        ar.MapGet("/", async (string? settlementStatus, DateOnly? asOf, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListAccountsReceivableQuery(settlementStatus, asOf), ct);
            return Results.Ok(list);
        });

        ar.MapGet("/{id:guid}", async (Guid id, DateOnly? asOf, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetAccountsReceivableByIdQuery(id, asOf), ct);
            return Results.Ok(item);
        });

        ar.MapPost("/{id:guid}/adjust", async (
            Guid id,
            AdjustApArRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            var adjId = await sender.Send(new AdjustAccountsReceivableCommand(id, body.DeltaAmount, body.Reason, ifMatch.ToString()), ct);
            return Results.Created($"/api/accounts-receivable/{id}/adjustments/{adjId}", new { id = adjId });
        });

        ar.MapGet("/{id:guid}/adjustments", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListAccountsReceivableAdjustmentsQuery(id), ct);
            return Results.Ok(list);
        });

        ar.MapPost("/{id:guid}/reverse-recognize", async (
            Guid id,
            ReverseRecognizeRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            await sender.Send(new ReverseRecognizeAccountsReceivableCommand(id, body.Reason, ifMatch.ToString()), ct);
            return Results.NoContent();
        });

        ar.MapPost("/{id:guid}/write-off", async (
            Guid id,
            WriteOffRequest body,
            HttpRequest http,
            ISender sender,
            CancellationToken ct) =>
        {
            http.Headers.TryGetValue("If-Match", out var ifMatch);
            var result = await sender.Send(
                new LCMS.Application.Settlements.Commands.WriteOffAccountsReceivableCommand(
                    id, body.Amount, body.Reason, ifMatch.ToString()),
                ct);
            if (!result.AppliedImmediately)
            {
                return Results.Json(
                    new
                    {
                        requiresApproval = true,
                        approvalId = result.ApprovalId,
                        requiredLevel = result.RequiredLevel,
                        message = "Số xóa nợ vượt trần áp dụng ngay; đã gửi yêu cầu phê duyệt. Xóa nợ chỉ ghi khi được duyệt."
                    },
                    statusCode: StatusCodes.Status202Accepted);
            }

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

public sealed record ReverseRecognizeRequest(string Reason);

public sealed record WriteOffRequest(decimal Amount, string Reason);

public sealed record LinkDocumentRequest(Guid FinancialDocumentId);
