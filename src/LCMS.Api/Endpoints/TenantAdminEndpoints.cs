using LCMS.Application.Backups;
using LCMS.Application.Licenses;
using LCMS.Application.Notifications;
using LCMS.Application.Tenants.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LCMS.Api.Endpoints;

public static class TenantAdminEndpoints
{
    public static IEndpointRouteBuilder MapTenantAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var profile = app.MapGroup("/api/tenant-profile").WithTags("TenantProfile");
        profile.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetTenantProfileQuery(), ct)));
        profile.MapPut("/", async (UpdateTenantProfileRequest body, ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(
                new UpdateTenantProfileCommand(
                    body.Name,
                    body.LegalName,
                    body.TaxId,
                    body.Phone,
                    body.Email,
                    body.Website,
                    body.AddressLine1,
                    body.AddressLine2,
                    body.Ward,
                    body.District,
                    body.City,
                    body.Province,
                    body.CountryCode,
                    body.PostalCode,
                    body.TimeZoneId,
                    body.DateFormat,
                    body.DefaultCurrencyCode),
                ct);
            return Results.Ok(dto);
        });
        profile.MapGet("/logo", async (ISender sender, CancellationToken ct) =>
        {
            var logo = await sender.Send(new GetTenantLogoQuery(), ct);
            return logo is null
                ? Results.NotFound()
                : Results.File(logo.Value.Bytes, logo.Value.ContentType);
        });
        profile.MapPut("/logo", async (SetTenantLogoRequest body, ISender sender, CancellationToken ct) =>
        {
            var bytes = Convert.FromBase64String(body.Base64);
            await sender.Send(new SetTenantLogoCommand(body.ContentType, bytes), ct);
            return Results.NoContent();
        });
        profile.MapDelete("/logo", async (ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ClearTenantLogoCommand(), ct);
            return Results.NoContent();
        });

        var license = app.MapGroup("/api/tenant-license").WithTags("TenantLicense");
        license.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetTenantLicenseQuery(), ct)));
        license.MapPut("/modules/{moduleCode}", async (
            string moduleCode,
            SetLicenseModuleRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var dto = await sender.Send(new SetLicenseModuleEnabledCommand(moduleCode, body.IsEnabled), ct);
            return Results.Ok(dto);
        });

        var notify = app.MapGroup("/api/notifications").WithTags("Notifications");
        notify.MapGet("/settings", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetNotificationSettingsQuery(), ct)));
        notify.MapPut("/settings", async (
            UpdateNotificationSettingsCommand body,
            ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(body, ct)));
        notify.MapGet("/inbox", async (bool? unreadOnly, int? take, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListInAppNotificationsQuery(unreadOnly == true, take ?? 50), ct)));
        notify.MapPost("/inbox/{id:guid}/read", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MarkNotificationReadCommand(id), ct);
            return Results.NoContent();
        });
        notify.MapPost("/inbox/read-all", async (ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new MarkAllNotificationsReadCommand(), ct);
            return Results.NoContent();
        });

        var backups = app.MapGroup("/api/tenant-backups").WithTags("TenantBackups");
        backups.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListTenantBackupsQuery(), ct)));
        backups.MapPost("/", async (CreateTenantBackupRequest? body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateTenantBackupCommand(body?.Note), ct);
            return Results.Created($"/api/tenant-backups/{id}", new { id });
        });
        backups.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetTenantBackupQuery(id), ct)));
        backups.MapPost("/{id:guid}/restore", async (
            Guid id,
            RestoreTenantBackupRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new RestoreTenantBackupCommand(id, body.ConfirmPhrase), ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record UpdateTenantProfileRequest(
    string Name,
    string? LegalName,
    string? TaxId,
    string? Phone,
    string? Email,
    string? Website,
    string? AddressLine1,
    string? AddressLine2,
    string? Ward,
    string? District,
    string? City,
    string? Province,
    string? CountryCode,
    string? PostalCode,
    string TimeZoneId,
    string DateFormat,
    string DefaultCurrencyCode);

public sealed record SetTenantLogoRequest(string ContentType, string Base64);
public sealed record SetLicenseModuleRequest(bool IsEnabled);
public sealed record CreateTenantBackupRequest(string? Note);
public sealed record RestoreTenantBackupRequest(string ConfirmPhrase);
