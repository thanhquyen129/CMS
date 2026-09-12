using LCMS.Application.Bills.Queries;
using LCMS.Application.Dashboard.Queries;
using LCMS.Application.Queues.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class DashboardReportingEndpoints
{
    public static IEndpointRouteBuilder MapDashboardReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var dashboard = app.MapGroup("/api/dashboard").WithTags("Dashboard");

        dashboard.MapGet("/summary", async (
            bool? includeBaseCurrencyRollUp,
            ISender sender,
            CancellationToken ct) =>
        {
            var summary = await sender.Send(
                new GetDashboardSummaryQuery(includeBaseCurrencyRollUp ?? true),
                ct);
            return Results.Ok(summary);
        });

        var queues = app.MapGroup("/api/queues").WithTags("ControlQueues");

        queues.MapGet("/exceptions", async (
            string? status,
            string? severity,
            bool? overdueOnly,
            string? objectType,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListOpenExceptionQueueQuery(status, severity, overdueOnly, objectType),
                ct);
            return Results.Ok(list);
        });

        queues.MapGet("/approvals", async (
            string? status,
            string? objectType,
            int? requiredLevel,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(
                new ListPendingApprovalQueueQuery(status, objectType, requiredLevel),
                ct);
            return Results.Ok(list);
        });

        queues.MapGet("/reconciliations", async (
            string? status,
            ISender sender,
            CancellationToken ct) =>
        {
            var list = await sender.Send(new ListOpenReconciliationQueueQuery(status), ct);
            return Results.Ok(list);
        });

        return app;
    }
}
