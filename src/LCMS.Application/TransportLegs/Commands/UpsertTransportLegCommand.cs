using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.TransportLegs.Commands;

/// <summary>
/// Idempotent upsert by (tenant_id, source_system, external_id) — C-002.
/// Shipment 1:N required (TD1).
/// </summary>
public sealed record UpsertTransportLegCommand(
    string LegNo,
    Guid ShipmentId,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool IsActive = true,
    int? SequenceNo = null,
    string? OriginCode = null,
    string? DestinationCode = null,
    Guid? OriginLocationId = null,
    Guid? DestinationLocationId = null) : IRequest<Guid>;

public sealed class UpsertTransportLegCommandValidator : AbstractValidator<UpsertTransportLegCommand>
{
    public UpsertTransportLegCommandValidator()
    {
        RuleFor(x => x.LegNo)
            .NotEmpty().WithMessage("Số chặng vận chuyển không được để trống.")
            .MaximumLength(64).WithMessage("Số chặng vận chuyển không được vượt quá 64 ký tự.");

        RuleFor(x => x.ShipmentId)
            .NotEmpty().WithMessage("Lô hàng không hợp lệ.");

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

public sealed class UpsertTransportLegCommandHandler : IRequestHandler<UpsertTransportLegCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpsertTransportLegCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(UpsertTransportLegCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var shipmentExists = await _db.Shipments.AnyAsync(s => s.Id == request.ShipmentId, cancellationToken);
        if (!shipmentExists)
        {
            throw new NotFoundAppException("Không tìm thấy lô hàng.");
        }

        var sourceSystem = request.SourceSystem.Trim();
        var externalId = request.ExternalId.Trim();
        var legNo = request.LegNo.Trim();
        var status = string.IsNullOrWhiteSpace(request.OperationalStatus)
            ? "active"
            : request.OperationalStatus.Trim();
        var externalVersion = string.IsNullOrWhiteSpace(request.ExternalVersion)
            ? null
            : request.ExternalVersion.Trim();

        var existing = await _db.TransportLegs.FirstOrDefaultAsync(
            l => l.TenantId == tenantId
                 && l.SourceSystem == sourceSystem
                 && l.ExternalId == externalId,
            cancellationToken);

        if (existing is null)
        {
            var leg = new TransportLeg
            {
                TenantId = tenantId,
                LegNo = legNo,
                ShipmentId = request.ShipmentId,
                SourceSystem = sourceSystem,
                ExternalId = externalId,
                ExternalVersion = externalVersion,
                OperationalStatus = status,
                IsActive = request.IsActive,
                SequenceNo = request.SequenceNo ?? 0,
                OriginCode = request.OriginCode,
                DestinationCode = request.DestinationCode,
                OriginLocationId = request.OriginLocationId,
                DestinationLocationId = request.DestinationLocationId
            };
            _db.TransportLegs.Add(leg);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                existing = await _db.TransportLegs.FirstOrDefaultAsync(
                    l => l.TenantId == tenantId
                         && l.SourceSystem == sourceSystem
                         && l.ExternalId == externalId,
                    cancellationToken);
                if (existing is null)
                {
                    throw new ConflictAppException(
                        "Chặng vận chuyển với mã tham chiếu ngoài này đã tồn tại trong thuê bao (C-002).");
                }
            }

            if (existing is null)
            {
                return leg.Id;
            }
        }

        existing.LegNo = legNo;
        existing.ShipmentId = request.ShipmentId;
        existing.ExternalVersion = externalVersion;
        existing.OperationalStatus = status;
        existing.IsActive = request.IsActive;
        if (request.SequenceNo is int sequence)
        {
            existing.SequenceNo = sequence;
        }

        existing.OriginCode = request.OriginCode ?? existing.OriginCode;
        existing.DestinationCode = request.DestinationCode ?? existing.DestinationCode;
        existing.OriginLocationId = request.OriginLocationId ?? existing.OriginLocationId;
        existing.DestinationLocationId = request.DestinationLocationId ?? existing.DestinationLocationId;
        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }
}
