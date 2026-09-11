using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.TransportMovements.Commands;

/// <summary>
/// Idempotent upsert by (tenant_id, source_system, external_id) — C-002.
/// </summary>
public sealed record UpsertTransportMovementCommand(
    string MovementNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool IsActive = true) : IRequest<Guid>;

public sealed class UpsertTransportMovementCommandValidator : AbstractValidator<UpsertTransportMovementCommand>
{
    public UpsertTransportMovementCommandValidator()
    {
        RuleFor(x => x.MovementNo)
            .NotEmpty().WithMessage("Số chuyến vận chuyển không được để trống.")
            .MaximumLength(64).WithMessage("Số chuyến vận chuyển không được vượt quá 64 ký tự.");

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

public sealed class UpsertTransportMovementCommandHandler
    : IRequestHandler<UpsertTransportMovementCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpsertTransportMovementCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(UpsertTransportMovementCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var sourceSystem = request.SourceSystem.Trim();
        var externalId = request.ExternalId.Trim();
        var movementNo = request.MovementNo.Trim();
        var status = string.IsNullOrWhiteSpace(request.OperationalStatus)
            ? "active"
            : request.OperationalStatus.Trim();
        var externalVersion = string.IsNullOrWhiteSpace(request.ExternalVersion)
            ? null
            : request.ExternalVersion.Trim();

        var existing = await _db.TransportMovements.FirstOrDefaultAsync(
            m => m.TenantId == tenantId
                 && m.SourceSystem == sourceSystem
                 && m.ExternalId == externalId,
            cancellationToken);

        if (existing is null)
        {
            var movement = new TransportMovement
            {
                TenantId = tenantId,
                MovementNo = movementNo,
                SourceSystem = sourceSystem,
                ExternalId = externalId,
                ExternalVersion = externalVersion,
                OperationalStatus = status,
                IsActive = request.IsActive
            };
            _db.TransportMovements.Add(movement);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                existing = await _db.TransportMovements.FirstOrDefaultAsync(
                    m => m.TenantId == tenantId
                         && m.SourceSystem == sourceSystem
                         && m.ExternalId == externalId,
                    cancellationToken);
                if (existing is null)
                {
                    throw new ConflictAppException(
                        "Chuyến vận chuyển với mã tham chiếu ngoài này đã tồn tại trong thuê bao (C-002).");
                }
            }

            if (existing is null)
            {
                return movement.Id;
            }
        }

        existing.MovementNo = movementNo;
        existing.ExternalVersion = externalVersion;
        existing.OperationalStatus = status;
        existing.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }
}
