using LCMS.Application.Demo;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class SampleDataEndpoints
{
    public static IEndpointRouteBuilder MapSampleDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sample-data").WithTags("SampleData");
        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetSampleDataStatusQuery(), ct)));
        group.MapPost("/ensure", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new EnsureSampleDataCommand(), ct);
            return Results.Ok(new
            {
                tenantId = result.TenantId,
                skipped = result.Skipped,
                summary = result.Summary,
                counts = result.Counts,
                targetCount = DemoVolumeCatalogSeeder.TargetCount
            });
        });
        return app;
    }
}
