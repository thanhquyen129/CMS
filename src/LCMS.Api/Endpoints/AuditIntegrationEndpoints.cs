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
            int? take,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListAuditEventsQuery(objectType, objectId, action, correlationId, take ?? 100),
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
