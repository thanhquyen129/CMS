using LCMS.Application.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace LCMS.Api.Endpoints;

public static class TenantSettingsEndpoints
{
    public static IEndpointRouteBuilder MapTenantSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenant-settings").WithTags("TenantSettings");

        group.MapGet("/", async (ITenantSettingsService settings, CancellationToken ct) =>
        {
            var dto = await settings.GetAsync(ct);
            return Results.Ok(dto);
        });

        group.MapPut("/", async (
            [FromBody] UpsertTenantSettingsRequest body,
            ITenantSettingsService settings,
            CancellationToken ct) =>
        {
            var dto = await settings.UpsertAsync(body.UiJson, body.FinancialJson, ct);
            return Results.Ok(dto);
        });

        return app;
    }
}

public sealed record UpsertTenantSettingsRequest(string? UiJson, string? FinancialJson);
