using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Shipments.Commands;

/// <summary>
/// Idempotent upsert by (tenant_id, source_system, external_id) — C-002.
/// </summary>
public sealed record UpsertShipmentCommand(
    string ShipmentNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool IsActive = true) : IRequest<Guid>;

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
    }
}

public sealed class UpsertShipmentCommandHandler : IRequestHandler<UpsertShipmentCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpsertShipmentCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(UpsertShipmentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

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
            _db.Shipments.Add(shipment);
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
        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }
}
