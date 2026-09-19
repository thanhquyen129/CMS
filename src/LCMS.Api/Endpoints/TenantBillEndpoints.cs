using LCMS.Application.Bills.Commands;
using LCMS.Application.Bills.Queries;
using LCMS.Application.Bills.Waybills;
using LCMS.Application.Tenants.Commands;
using LCMS.Application.Tenants.Queries;
using MediatR;

namespace LCMS.Api.Endpoints;

public static class TenantBillEndpoints
{
    public static IEndpointRouteBuilder MapTenantBillEndpoints(this IEndpointRouteBuilder app)
    {
        var tenants = app.MapGroup("/api/tenants").WithTags("Tenants");
        tenants.MapPost("/", async (CreateTenantRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateTenantCommand(body.Code, body.Name), ct);
            return Results.Created($"/api/tenants/{id}", new { id });
        });
        tenants.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var tenant = await sender.Send(new GetTenantByIdQuery(id), ct);
            return Results.Ok(tenant);
        });

        var bills = app.MapGroup("/api/bills").WithTags("Bills");
        bills.MapPost("/", async (CreateBillRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CreateBillCommand(
                    body.BillNo,
                    body.BillType,
                    body.SourceSystem,
                    body.ExternalId,
                    body.OrganizationId),
                ct);
            return Results.Created($"/api/bills/{id}", new { id });
        });
        bills.MapGet("/", async (string? q, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var list = await sender.Send(new ListBillsQuery(q, page, pageSize), ct);
            // Legacy callers (no paging) expect a bare array.
            if (page is null && pageSize is null)
            {
                return Results.Ok(list.Items);
            }

            return Results.Ok(list);
        });
        bills.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var bill = await sender.Send(new GetBillByIdQuery(id), ct);
            return Results.Ok(bill);
        });
        bills.MapGet("/{id:guid}/financial-view", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var view = await sender.Send(new GetBillFinancialViewQuery(id), ct);
            return Results.Ok(view);
        });
        bills.MapPatch("/{id:guid}/context", async (
            Guid id,
            UpdateBillContextRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(
                new UpdateBillContextCommand(
                    id,
                    body.CustomerPartyId,
                    body.RouteCode,
                    body.EtdAt,
                    body.EtaAt,
                    body.AssignedUserId,
                    body.Description,
                    body.InternalNote),
                ct);
            return Results.NoContent();
        });
        bills.MapPost("/waybills", async (CaptureWaybillRequest body, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(
                new CaptureWaybillCommand(
                    body.BillNo,
                    body.BillType,
                    body.SourceSystem,
                    body.ExternalId,
                    body.OrganizationId,
                    body.ToWriteBody()),
                ct);
            return Results.Created($"/api/bills/{id}", new { id });
        });
        bills.MapGet("/{id:guid}/waybill", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var waybill = await sender.Send(new GetBillWaybillQuery(id), ct);
            return Results.Ok(waybill);
        });
        bills.MapPut("/{id:guid}/waybill", async (
            Guid id,
            CaptureWaybillProfileRequest body,
            ISender sender,
            CancellationToken ct) =>
        {
            await sender.Send(new UpsertBillWaybillCommand(id, body.ToWriteBody()), ct);
            return Results.NoContent();
        });

        return app;
    }
}

public sealed record CreateTenantRequest(string Code, string Name);

public sealed record CreateBillRequest(
    string BillNo,
    string BillType,
    string? SourceSystem,
    string? ExternalId,
    Guid? OrganizationId = null);

public sealed record UpdateBillContextRequest(
    Guid? CustomerPartyId,
    string? RouteCode,
    DateTimeOffset? EtdAt,
    DateTimeOffset? EtaAt,
    Guid? AssignedUserId,
    string? Description,
    string? InternalNote);

public sealed record CaptureWaybillPartyRequest(
    string? Name,
    string? Phone,
    string? Email,
    string? Address,
    string? CustomerCode,
    string? PostalCode,
    string? DeliveryCode = null);

public sealed record CaptureWaybillProfileRequest(
    string? CarrierName,
    string? ItemFormCode,
    CaptureWaybillPartyRequest? Sender,
    CaptureWaybillPartyRequest? Consignee,
    string? PackageKind,
    string? ContentsDescription,
    int? ContentsQuantity,
    decimal? DeclaredValue,
    string? AccompanyingDocs,
    string? VatServicesNote,
    string? NonDeliveryAction,
    bool SenderCommitAccepted = false,
    DateTimeOffset? SentAt = null,
    int? ParcelCount = null,
    decimal? ActualWeightKg = null,
    decimal? ChargeableWeightKg = null,
    decimal? BasePostage = null,
    decimal? VatPostage = null,
    decimal? Surcharge = null,
    decimal? CodFee = null,
    decimal? OtherFee = null,
    decimal? TotalPostageInclVat = null,
    decimal? TotalCollect = null,
    decimal? GrandTotal = null,
    string? CurrencyCode = null,
    string? PostagePayer = null,
    string? ChargeEconomicRole = null,
    decimal? CodCollectAmount = null,
    string? OperationsNote = null,
    string? AcceptingOffice = null,
    DateTimeOffset? AcceptedAt = null,
    string? AcceptedBy = null,
    DateTimeOffset? ReceivedAt = null,
    string? ReceivedBy = null)
{
    /// <summary>Maps HTTP body to the application write model.</summary>
    public WaybillWriteBody ToWriteBody() =>
        new(
            CarrierName,
            ItemFormCode,
            Sender is null
                ? null
                : new WaybillPartyDto(
                    Sender.Name,
                    Sender.Phone,
                    Sender.Email,
                    Sender.Address,
                    Sender.CustomerCode,
                    Sender.PostalCode),
            Consignee is null
                ? null
                : new WaybillPartyDto(
                    Consignee.Name,
                    Consignee.Phone,
                    Consignee.Email,
                    Consignee.Address,
                    Consignee.CustomerCode,
                    Consignee.PostalCode,
                    Consignee.DeliveryCode),
            PackageKind,
            ContentsDescription,
            ContentsQuantity,
            DeclaredValue,
            AccompanyingDocs,
            VatServicesNote,
            NonDeliveryAction,
            SenderCommitAccepted,
            SentAt,
            ParcelCount,
            ActualWeightKg,
            ChargeableWeightKg,
            BasePostage,
            VatPostage,
            Surcharge,
            CodFee,
            OtherFee,
            TotalPostageInclVat,
            TotalCollect,
            GrandTotal,
            CurrencyCode,
            PostagePayer,
            ChargeEconomicRole,
            CodCollectAmount,
            OperationsNote,
            AcceptingOffice,
            AcceptedAt,
            AcceptedBy,
            ReceivedAt,
            ReceivedBy);
}

public sealed record CaptureWaybillRequest(
    string BillNo,
    string? BillType,
    string? SourceSystem,
    string? ExternalId,
    Guid? OrganizationId,
    string? CarrierName,
    string? ItemFormCode,
    CaptureWaybillPartyRequest? Sender,
    CaptureWaybillPartyRequest? Consignee,
    string? PackageKind,
    string? ContentsDescription,
    int? ContentsQuantity,
    decimal? DeclaredValue,
    string? AccompanyingDocs,
    string? VatServicesNote,
    string? NonDeliveryAction,
    bool SenderCommitAccepted = false,
    DateTimeOffset? SentAt = null,
    int? ParcelCount = null,
    decimal? ActualWeightKg = null,
    decimal? ChargeableWeightKg = null,
    decimal? BasePostage = null,
    decimal? VatPostage = null,
    decimal? Surcharge = null,
    decimal? CodFee = null,
    decimal? OtherFee = null,
    decimal? TotalPostageInclVat = null,
    decimal? TotalCollect = null,
    decimal? GrandTotal = null,
    string? CurrencyCode = null,
    string? PostagePayer = null,
    string? ChargeEconomicRole = null,
    decimal? CodCollectAmount = null,
    string? OperationsNote = null,
    string? AcceptingOffice = null,
    DateTimeOffset? AcceptedAt = null,
    string? AcceptedBy = null,
    DateTimeOffset? ReceivedAt = null,
    string? ReceivedBy = null)
{
    /// <summary>Maps HTTP body to the application write model.</summary>
    public WaybillWriteBody ToWriteBody() =>
        new CaptureWaybillProfileRequest(
            CarrierName,
            ItemFormCode,
            Sender,
            Consignee,
            PackageKind,
            ContentsDescription,
            ContentsQuantity,
            DeclaredValue,
            AccompanyingDocs,
            VatServicesNote,
            NonDeliveryAction,
            SenderCommitAccepted,
            SentAt,
            ParcelCount,
            ActualWeightKg,
            ChargeableWeightKg,
            BasePostage,
            VatPostage,
            Surcharge,
            CodFee,
            OtherFee,
            TotalPostageInclVat,
            TotalCollect,
            GrandTotal,
            CurrencyCode,
            PostagePayer,
            ChargeEconomicRole,
            CodCollectAmount,
            OperationsNote,
            AcceptingOffice,
            AcceptedAt,
            AcceptedBy,
            ReceivedAt,
            ReceivedBy).ToWriteBody();
}
