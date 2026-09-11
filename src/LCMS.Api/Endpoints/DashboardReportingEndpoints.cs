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

        dashboard.MapGet("/summary", async (ISender sender, CancellationToken ct) =>
        {
            var summary = await sender.Send(new GetDashboardSummaryQuery(), ct);
            return Results.Ok(summary);
        });

        var queues = app.MapGroup("/api/queues").WithTags("ControlQueues");

        queues.MapGet("/exceptions", async (string? severity, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListOpenExceptionQueueQuery(severity), ct);
            return Results.Ok(list);
        });

        queues.MapGet("/approvals", async (string? objectType, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListPendingApprovalQueueQuery(objectType), ct);
            return Results.Ok(list);
        });

        return app;
    }
}
