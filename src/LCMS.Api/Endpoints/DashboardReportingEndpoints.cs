using LCMS.Application.Dashboard.Queries;
using LCMS.Application.Exposures.Aging;
using LCMS.Application.Queues.Queries;
using LCMS.Application.Reports.Queries;
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

        var aging = app.MapGroup("/api/aging").WithTags("Aging");
        aging.MapGet("/summary", async (
            DateOnly? asOf,
            Guid? counterpartyId,
            string? currencyCode,
            bool? includeSettled,
            ISender sender,
            CancellationToken ct) =>
        {
            var report = await sender.Send(
                new GetAgingSummaryQuery(asOf, counterpartyId, currencyCode, includeSettled ?? false),
                ct);
            return Results.Ok(report);
        });
        aging.MapGet("/export", async (
            string side,
            DateOnly? asOf,
            Guid? counterpartyId,
            string? currencyCode,
            bool? includeSettled,
            ISender sender,
            CancellationToken ct) =>
        {
            var file = await sender.Send(
                new ExportAgingCsvQuery(side, asOf, counterpartyId, currencyCode, includeSettled ?? false),
                ct);
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(file.CsvContent),
                "text/csv; charset=utf-8",
                file.FileName);
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

        var reports = app.MapGroup("/api/reports").WithTags("Reports");
        reports.MapGet("/cash-settlement", async (
            DateOnly? asOf,
            ISender sender,
            CancellationToken ct) =>
        {
            var report = await sender.Send(new GetCashSettlementReportQuery(asOf), ct);
            return Results.Ok(report);
        });

        return app;
    }
}
