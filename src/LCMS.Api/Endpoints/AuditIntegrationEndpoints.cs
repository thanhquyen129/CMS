using LCMS.Application.Audit.Queries;
using LCMS.Application.Integrations.Commands;
using LCMS.Application.Integrations.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class AuditIntegrationEndpoints
{
    public static IEndpointRouteBuilder MapAuditIntegrationEndpoints(this IEndpointRouteBuilder app)
    {
        var audit = app.MapGroup("/api/audit-events").WithTags("AuditEvents");

        audit.MapGet("/", async (
            string? objectType,
            Guid? objectId,
            string? action,
            string? correlationId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? take,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListAuditEventsQuery(objectType, objectId, action, correlationId, from, to, take ?? 100),
                ct);
            return Results.Ok(list);
        });

        var integrations = app.MapGroup("/api/integration-records").WithTags("IntegrationRecords");

        integrations.MapPost("/", async (
            UpsertIntegrationRecordRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(
                new UpsertIntegrationRecordCommand(
                    body.SourceSystem,
                    body.ExternalObjectType,
                    body.ExternalId,
                    body.ExternalVersion,
                    body.Status,
                    body.LocalObjectType,
                    body.LocalObjectId,
                    body.PayloadHash,
                    body.Notes),
                ct);
            return Results.Created($"/api/integration-records/{id}", new { id });
        });

        integrations.MapGet("/", async (
            string? sourceSystem,
            string? externalObjectType,
            string? status,
            int? take,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListIntegrationRecordsQuery(sourceSystem, externalObjectType, status, take ?? 100),
                ct);
            return Results.Ok(list);
        });

        var errors = app.MapGroup("/api/integration-errors").WithTags("IntegrationErrors");

        errors.MapPost("/", async (
            RecordIntegrationErrorRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(
                new RecordIntegrationErrorCommand(
                    body.IntegrationRecordId,
                    body.ErrorCode,
                    body.Message,
                    body.Detail,
                    body.NextRetryAt),
                ct);
            return Results.Created($"/api/integration-errors/{id}", new { id });
        });

        errors.MapGet("/", async (
            Guid? integrationRecordId,
            string? recoveryStatus,
            int? take,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListIntegrationErrorsQuery(integrationRecordId, recoveryStatus, take ?? 100),
                ct);
            return Results.Ok(list);
        });

        errors.MapPost("/{id:guid}/mark-retried", async (
            Guid id,
            MarkIntegrationErrorRequest? body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new MarkIntegrationErrorRetriedCommand(id, body?.Note), ct);
            return Results.NoContent();
        });

        errors.MapPost("/{id:guid}/dead-letter", async (
            Guid id,
            MarkIntegrationErrorRequest? body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new DeadLetterIntegrationErrorCommand(id, body?.Note), ct);
            return Results.NoContent();
        });

        var outbox = app.MapGroup("/api/outbox").WithTags("Outbox");

        outbox.MapPost("/enqueue", async (
            EnqueueOutboxRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var id = await sender.Send(new EnqueueOutboxMessageCommand(body.Topic, body.PayloadJson), ct);
            return Results.Created($"/api/outbox/{id}", new { id });
        });

        outbox.MapPost("/process-once", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ProcessOutboxOnceCommand(), ct);
            return Results.Ok(result);
        });

        outbox.MapGet("/", async (
            string? status,
            int? take,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(new ListOutboxMessagesQuery(status, take ?? 100), ct);
            return Results.Ok(list);
        });

        return app;
    }
}

public sealed record UpsertIntegrationRecordRequest(
    string SourceSystem,
    string ExternalObjectType,
    string ExternalId,
    string? ExternalVersion,
    string? Status,
    string? LocalObjectType,
    Guid? LocalObjectId,
    string? PayloadHash,
    string? Notes);

public sealed record RecordIntegrationErrorRequest(
    Guid IntegrationRecordId,
    string ErrorCode,
    string Message,
    string? Detail,
    DateTimeOffset? NextRetryAt);

public sealed record MarkIntegrationErrorRequest(string? Note);

public sealed record EnqueueOutboxRequest(string Topic, string PayloadJson);
