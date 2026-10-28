using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Catalog;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using LCMS.Application.OperationalReferences.Edit;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Shipments.Commands;

/// <summary>
/// Idempotent upsert by (tenant_id, source_system, external_id) — C-002.
/// Optional operational context is applied only when <see cref="ApplyContext"/> is true.
/// </summary>
public sealed record UpsertShipmentCommand(
    string ShipmentNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool IsActive = true,
    Guid? AssignedUserId = null,
    string? TransportMode = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    string? RouteCode = null,
    DateTimeOffset? EtdAt = null,
    DateTimeOffset? EtaAt = null,
    string? CustomerReference = null,
    string? Description = null,
    OperationalContextDocument? Context = null,
    bool ApplyContext = false,
    string? EntryChannel = null,
    Guid? CarrierPartyId = null) : IRequest<Guid>;

public sealed class UpsertShipmentCommandValidator : AbstractValidator<UpsertShipmentCommand>
{
    public UpsertShipmentCommandValidator()
    {
        RuleFor(x => x.ShipmentNo)
            .NotEmpty().WithMessage("Số lô hàng không được để trống.")
            .MaximumLength(64).WithMessage("Số lô hàng không được vượt quá 64 ký tự.");

        RuleFor(x => x.SourceSystem)
            .NotEmpty().WithMessage("Hệ thống nguồn không được để trống.")
            .MaximumLength(64).WithMessage("Hệ thống nguồn không được vượt quá 64 ký tự.");

        RuleFor(x => x.ExternalId)
            .NotEmpty().WithMessage("Mã tham chiếu ngoài không được để trống.")
            .MaximumLength(128).WithMessage("Mã tham chiếu ngoài không được vượt quá 128 ký tự.");

        RuleFor(x => x.ExternalVersion)
            .MaximumLength(64)
            .When(x => x.ExternalVersion is not null);

        RuleFor(x => x.OperationalStatus)
            .MaximumLength(64)
            .When(x => x.OperationalStatus is not null);

        RuleFor(x => x.TransportMode).MaximumLength(32).When(x => x.TransportMode is not null);
        RuleFor(x => x.OriginCode).MaximumLength(64).When(x => x.OriginCode is not null);
        RuleFor(x => x.DestinationCode).MaximumLength(64).When(x => x.DestinationCode is not null);
        RuleFor(x => x.RouteCode).MaximumLength(128).When(x => x.RouteCode is not null);
        RuleFor(x => x.CustomerReference).MaximumLength(128).When(x => x.CustomerReference is not null);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x)
            .Must(x => x.EtdAt is null || x.EtaAt is null || x.EtdAt <= x.EtaAt)
            .WithMessage("ETD không được sau ETA.");
    }
}

public sealed class UpsertShipmentCommandHandler : IRequestHandler<UpsertShipmentCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IOperationalCargoStore _cargo;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;
    private readonly ICurrentUserContext _user;

    public UpsertShipmentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IOperationalCargoStore cargo,
        IPermissionService permissions,
        IAuditWriter audit,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _cargo = cargo;
        _permissions = permissions;
        _audit = audit;
        _user = user;
    }

    public async Task<Guid> Handle(UpsertShipmentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.ShipmentCreate,
            "Bạn không có quyền tạo Shipment.",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var sourceSystem = request.SourceSystem.Trim();
        var externalId = request.ExternalId.Trim();
        var shipmentNo = request.ShipmentNo.Trim();
        var status = string.IsNullOrWhiteSpace(request.OperationalStatus)
            ? "active"
            : request.OperationalStatus.Trim();
        var externalVersion = string.IsNullOrWhiteSpace(request.ExternalVersion)
            ? null
            : request.ExternalVersion.Trim();

        var existing = await _db.Shipments.FirstOrDefaultAsync(
            s => s.TenantId == tenantId
                 && s.SourceSystem == sourceSystem
                 && s.ExternalId == externalId,
            cancellationToken);

        if (existing is null)
        {
            var shipment = new Shipment
            {
                TenantId = tenantId,
                ShipmentNo = shipmentNo,
                SourceSystem = sourceSystem,
                ExternalId = externalId,
                ExternalVersion = externalVersion,
                OperationalStatus = status,
                IsActive = request.IsActive
            };
            await ApplyContextAsync(shipment, request, cancellationToken);
            await ApplyCarrierAsync(shipment, request.CarrierPartyId, tenantId, cancellationToken);
            _db.Shipments.Add(shipment);
            AuditQuickCreate(shipment.Id, shipmentNo, externalId, request);
            if (request.ApplyContext)
            {
                await _cargo.ApplyAsync(OperationalObjectTypes.Shipment, shipment.Id, request.Context, sourceSystem, request.OriginCode is not null, cancellationToken);
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                existing = await _db.Shipments.FirstOrDefaultAsync(
                    s => s.TenantId == tenantId
                         && s.SourceSystem == sourceSystem
                         && s.ExternalId == externalId,
                    cancellationToken);
                if (existing is null)
                {
                    throw new ConflictAppException(
                        "Lô hàng với mã tham chiếu ngoài này đã tồn tại trong thuê bao.");
                }
            }

            if (existing is null)
            {
                return shipment.Id;
            }
        }

        existing.ShipmentNo = shipmentNo;
        existing.ExternalVersion = externalVersion;
        existing.OperationalStatus = status;
        existing.IsActive = request.IsActive;
        await ApplyContextAsync(existing, request, cancellationToken);
        await ApplyCarrierAsync(existing, request.CarrierPartyId, tenantId, cancellationToken);
        if (request.ApplyContext)
        {
            await _cargo.ApplyAsync(OperationalObjectTypes.Shipment, existing.Id, request.Context, sourceSystem, request.OriginCode is not null, cancellationToken);
        }

        await OperationalOverrideGuard.ReapplyEntityOverridesAsync(_db, OperationalObjectTypes.Shipment, existing.Id, cancellationToken);
        AuditQuickCreate(existing.Id, shipmentNo, externalId, request);
        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }

    private void AuditQuickCreate(Guid shipmentId, string shipmentNo, string externalId, UpsertShipmentCommand request)
    {
        if (!string.Equals(request.EntryChannel?.Trim(), "quick_create_modal", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _audit.Append(
            AuditActions.ShipmentCreate,
            AuditObjectTypes.Shipment,
            shipmentId,
            afterJson: AuditJson.Serialize(new
            {
                source = "quick_create_modal",
                userId = _user.UserId,
                timestamp = DateTimeOffset.UtcNow,
                shipmentNo,
                externalId,
                transportMode = request.TransportMode,
                originCode = request.OriginCode,
                destinationCode = request.DestinationCode
            }),
            reason: "quick_create_modal");
    }

    private async Task ApplyCarrierAsync(
        Shipment shipment,
        Guid? carrierPartyId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (carrierPartyId is not Guid carrierId)
        {
            return;
        }

        var exists = await _db.BusinessParties.AnyAsync(
            p => p.Id == carrierId && p.TenantId == tenantId,
            cancellationToken);
        if (!exists)
        {
            throw new ConflictAppException("Hãng vận chuyển không thuộc thuê bao.", "invalid_carrier");
        }

        shipment.CarrierPartyId = carrierId;
    }

    private async Task ApplyContextAsync(Shipment shipment, UpsertShipmentCommand request, CancellationToken cancellationToken)
    {
        if (!request.ApplyContext)
        {
            return;
        }

        shipment.AssignedUserId = request.AssignedUserId;
        shipment.TransportMode = await TransportModeCatalog.ResolveAsync(
            _db, request.TransportMode, allowMultimodal: true, cancellationToken, shipment.TransportMode);
        shipment.OriginCode = OperationalContextJson.TrimOrNull(request.OriginCode);
        shipment.DestinationCode = OperationalContextJson.TrimOrNull(request.DestinationCode);
        var route = OperationalContextJson.TrimOrNull(request.RouteCode);
        if (route is null
            && !string.IsNullOrWhiteSpace(request.OriginCode)
            && !string.IsNullOrWhiteSpace(request.DestinationCode))
        {
            route = $"{request.OriginCode.Trim()} → {request.DestinationCode.Trim()}";
        }

        shipment.RouteCode = route;
        shipment.EtdAt = request.EtdAt;
        shipment.EtaAt = request.EtaAt;
        shipment.CustomerReference = OperationalContextJson.TrimOrNull(request.CustomerReference);
        shipment.Description = OperationalContextJson.TrimOrNull(request.Description);
        shipment.ContextJson = OperationalContextJson.Serialize(request.Context);
    }
}
