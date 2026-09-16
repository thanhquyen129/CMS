using LCMS.Application.Approvals.Commands;
using LCMS.Application.Approvals.Queries;
using LCMS.Application.Exceptions.Commands;
using LCMS.Application.Exceptions.Queries;
using LCMS.Application.Reconciliations.Commands;
using LCMS.Application.Reconciliations.Queries;
using LCMS.Application.Variances.Commands;
using LCMS.Application.Variances.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class FinancialControlEndpoints
{
    public static IEndpointRouteBuilder MapFinancialControlEndpoints(this IEndpointRouteBuilder app)
    {
        var reconciliations = app.MapGroup("/api/reconciliations").WithTags("Reconciliations");

        reconciliations.MapPost("/", async (StartReconciliationRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new StartReconciliationCommand(body.ReconciliationType, body.RuleCode, body.BillId, body.Notes),
                ct);
            return Results.Created($"/api/reconciliations/{id}", new { id });
        });

        reconciliations.MapGet("/", async (string? status, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListReconciliationsQuery(status), ct);
            return Results.Ok(list);
        });

        reconciliations.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetReconciliationByIdQuery(id), ct);
            return Results.Ok(item);
        });

        reconciliations.MapPost("/{id:guid}/details", async (
            Guid id,
            AddReconciliationDetailRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var detailId = await sender.Send(
                new AddReconciliationDetailCommand(
                    id,
                    body.SourceType,
                    body.SourceId,
                    body.TargetType,
                    body.TargetId,
                    body.SourceAmount,
                    body.TargetAmount,
                    body.MatchedAmount,
                    body.CurrencyCode,
                    body.Notes),
                ct);
            return Results.Created($"/api/reconciliations/{id}/details/{detailId}", new { id = detailId });
        });

        reconciliations.MapPost("/{id:guid}/details/batch", async (
            Guid id,
            AddReconciliationDetailBatchRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var lines = (body.Details ?? Array.Empty<AddReconciliationDetailRequest>())
                .Select(d => new ReconciliationDetailLine(
                    d.SourceType,
                    d.SourceId,
                    d.TargetType,
                    d.TargetId,
                    d.SourceAmount,
                    d.TargetAmount,
                    d.MatchedAmount,
                    d.CurrencyCode,
                    d.Notes))
                .ToList();
            var ids = await sender.Send(new AddReconciliationDetailBatchCommand(id, lines), ct);
            return Results.Created($"/api/reconciliations/{id}/details/batch", new { ids });
        });

        reconciliations.MapPost("/{id:guid}/complete", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CompleteReconciliationCommand(id), ct);
            return Results.NoContent();
        });

        var variances = app.MapGroup("/api/variances").WithTags("Variances");

        variances.MapGet("/", async (string? status, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListVariancesQuery(status), ct);
            return Results.Ok(list);
        });

        variances.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetVarianceByIdQuery(id), ct);
            return Results.Ok(item);
        });

        variances.MapPost("/{id:guid}/accept", async (
            Guid id,
            TransitionVarianceBody? body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new TransitionVarianceCommand(id, "accepted", body?.Explanation),
                ct);
            return Results.NoContent();
        });

        variances.MapPost("/{id:guid}/clear", async (
            Guid id,
            TransitionVarianceBody? body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new TransitionVarianceCommand(id, "cleared", body?.Explanation),
                ct);
            return Results.NoContent();
        });

        variances.MapPost("/{id:guid}/write-off", async (
            Guid id,
            TransitionVarianceBody? body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new TransitionVarianceCommand(id, "written_off", body?.Explanation),
                ct);
            return Results.NoContent();
        });

        var exceptions = app.MapGroup("/api/exceptions").WithTags("Exceptions");

        exceptions.MapPost("/", async (OpenExceptionRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new OpenExceptionCommand(
                    body.RuleCode,
                    body.Severity,
                    body.Title,
                    body.Description,
                    body.OwnerId,
                    body.DueAt,
                    body.BillId,
                    body.ReconciliationId,
                    body.VarianceId,
                    body.ObjectType,
                    body.ObjectId),
                ct);
            return Results.Created($"/api/exceptions/{id}", new { id });
        });

        exceptions.MapGet("/", async (
            string? status,
            string? severity,
            string? objectType,
            bool? overdueOnly,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListExceptionsQuery(status, severity, objectType, overdueOnly),
                ct);
            return Results.Ok(list);
        });

        exceptions.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetExceptionByIdQuery(id), ct);
            return Results.Ok(item);
        });

        exceptions.MapPost("/{id:guid}/resolve", async (
            Guid id,
            ResolveExceptionRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new ResolveExceptionCommand(id, body.ResolutionNotes), ct);
            return Results.NoContent();
        });

        exceptions.MapPost("/{id:guid}/close", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new CloseExceptionCommand(id), ct);
            return Results.NoContent();
        });

        exceptions.MapPost("/{id:guid}/escalate", async (
            Guid id,
            EscalateExceptionRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new EscalateExceptionCommand(id, body.EscalationReason), ct);
            return Results.NoContent();
        });

        var approvals = app.MapGroup("/api/approvals").WithTags("Approvals");

        approvals.MapPost("/", async (RequestApprovalRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new RequestApprovalCommand(
                    body.ObjectType,
                    body.ObjectId,
                    body.RequestReason,
                    body.Notes,
                    body.RequiredLevel),
                ct);
            return Results.Created($"/api/approvals/{id}", new { id });
        });

        approvals.MapGet("/", async (string? status, string? objectType, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListApprovalsQuery(status, objectType), ct);
            return Results.Ok(list);
        });

        approvals.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var item = await sender.Send(new GetApprovalByIdQuery(id), ct);
            return Results.Ok(item);
        });

        approvals.MapPost("/{id:guid}/approve", async (
            Guid id,
            DecideApprovalRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new DecideApprovalCommand(id, Approve: true, body.DecisionReason), ct);
            return Results.NoContent();
        });

        approvals.MapPost("/{id:guid}/reject", async (
            Guid id,
            DecideApprovalRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new DecideApprovalCommand(id, Approve: false, body.DecisionReason), ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record StartReconciliationRequest(
    string? ReconciliationType,
    string? RuleCode,
    Guid? BillId,
    string? Notes);

public sealed record AddReconciliationDetailRequest(
    string SourceType,
    Guid SourceId,
    string? TargetType,
    Guid? TargetId,
    decimal SourceAmount,
    decimal TargetAmount,
    decimal MatchedAmount,
    string CurrencyCode,
    string? Notes);

public sealed record AddReconciliationDetailBatchRequest(
    IReadOnlyList<AddReconciliationDetailRequest>? Details);

public sealed record OpenExceptionRequest(
    string RuleCode,
    string Severity,
    string Title,
    string? Description,
    Guid? OwnerId,
    DateTimeOffset? DueAt,
    Guid? BillId,
    Guid? ReconciliationId,
    Guid? VarianceId,
    string? ObjectType,
    Guid? ObjectId);

public sealed record ResolveExceptionRequest(string? ResolutionNotes);

public sealed record EscalateExceptionRequest(string EscalationReason);

public sealed record RequestApprovalRequest(
    string ObjectType,
    Guid ObjectId,
    string? RequestReason,
    string? Notes,
    int? RequiredLevel);

public sealed record DecideApprovalRequest(string? DecisionReason);

public sealed record TransitionVarianceBody(string? Explanation);
