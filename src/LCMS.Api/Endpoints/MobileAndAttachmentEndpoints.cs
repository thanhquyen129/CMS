using LCMS.Application.Attachments;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Mobile;
using LCMS.Application.Notifications;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LCMS.Api.Endpoints;

public static class MobileAndAttachmentEndpoints
{
    public static IEndpointRouteBuilder MapMobileAndAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var mobile = app.MapGroup("/api/mobile").WithTags("Mobile");

        mobile.MapGet("/bootstrap", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetMobileBootstrapQuery(), ct)))
            .WithSummary("Role-adaptive bootstrap for LCMS Mobile App (user, roles, permissions, SoD visibility, tabs, modules, terminology, badges)");

        var devices = app.MapGroup("/api/notifications/devices").WithTags("Notifications");

        devices.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListUserPushDevicesQuery(), ct)));

        devices.MapPost("/", async (
            [FromBody] RegisterPushDeviceRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            var dto = await sender.Send(
                new RegisterPushDeviceCommand(
                    body.DeviceToken,
                    body.Platform,
                    body.DeviceName,
                    body.AppVersion),
                ct);
            return Results.Ok(dto);
        });

        devices.MapDelete("/", async (
            [FromBody] UnregisterPushDeviceRequest? body,
            [FromQuery] string? deviceToken,
            ISender sender,
            CancellationToken ct) =>
        {
            var token = body?.DeviceToken ?? deviceToken ?? string.Empty;
            await sender.Send(new UnregisterPushDeviceCommand(token), ct);
            return Results.NoContent();
        });

        var attachments = app.MapGroup("/api/attachments").WithTags("Attachments")
            .DisableAntiforgery();

        attachments.MapGet("/", async (
            [FromQuery] string objectType,
            [FromQuery] Guid objectId,
            ISender sender,
            CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListAttachmentsQuery(objectType, objectId), ct)));

        attachments.MapPost("/", async (
            HttpRequest request,
            ISender sender,
            CancellationToken ct) =>
        {
            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(ct);
                var file = form.Files.FirstOrDefault()
                    ?? throw new ValidationAppException(new Dictionary<string, string[]>
                    {
                        ["file"] = ["Chưa chọn tệp đính kèm."]
                    });
                var objectType = form["objectType"].ToString();
                if (!Guid.TryParse(form["objectId"].ToString(), out var objectId))
                {
                    throw new ValidationAppException(new Dictionary<string, string[]>
                    {
                        ["objectId"] = ["objectId không hợp lệ."]
                    });
                }

                var notes = form["notes"].ToString();
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                var dto = await sender.Send(
                    new UploadAttachmentCommand(
                        objectType,
                        objectId,
                        file.FileName,
                        file.ContentType,
                        ms.ToArray(),
                        string.IsNullOrWhiteSpace(notes) ? null : notes),
                    ct);
                return Results.Created($"/api/attachments/{dto.Id}/content", dto);
            }
            else
            {
                var body = await request.ReadFromJsonAsync<UploadAttachmentJsonRequest>(cancellationToken: ct)
                    ?? throw new ValidationAppException(new Dictionary<string, string[]>
                    {
                        ["body"] = ["Dữ liệu tải lên không hợp lệ."]
                    });
                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(body.Base64 ?? string.Empty);
                }
                catch (FormatException)
                {
                    throw new ValidationAppException(new Dictionary<string, string[]>
                    {
                        ["base64"] = ["Nội dung Base64 của tệp đính kèm không hợp lệ."]
                    });
                }

                var dto = await sender.Send(
                    new UploadAttachmentCommand(
                        body.ObjectType,
                        body.ObjectId,
                        body.FileName,
                        body.ContentType,
                        bytes,
                        body.Notes),
                    ct);
                return Results.Created($"/api/attachments/{dto.Id}/content", dto);
            }
        });

        attachments.MapGet("/{id:guid}/content", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(new GetAttachmentContentQuery(id), ct);
            return Results.File(result.Bytes, result.ContentType, result.FileName);
        });

        attachments.MapDelete("/{id:guid}", async (
            Guid id,
            [FromBody] DeleteAttachmentRequest? body,
            [FromQuery] string? reason,
            ISender sender,
            CancellationToken ct) =>
        {
            var effectiveReason = body?.Reason ?? reason;
            await sender.Send(new DeleteAttachmentCommand(id, effectiveReason), ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record RegisterPushDeviceRequest(
    string DeviceToken,
    string Platform,
    string? DeviceName = null,
    string? AppVersion = null);

public sealed record UnregisterPushDeviceRequest(string DeviceToken);

public sealed record UploadAttachmentJsonRequest(
    string ObjectType,
    Guid ObjectId,
    string FileName,
    string ContentType,
    string Base64,
    string? Notes = null);

public sealed record DeleteAttachmentRequest(string? Reason);
