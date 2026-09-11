using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Orders.Commands;

/// <summary>
/// Idempotent upsert by (tenant_id, source_system, external_id) — C-002.
/// </summary>
public sealed record UpsertOrderCommand(
    string OrderNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string? OperationalStatus,
    bool IsActive = true) : IRequest<Guid>;

public sealed class UpsertOrderCommandValidator : AbstractValidator<UpsertOrderCommand>
{
    public UpsertOrderCommandValidator()
    {
        RuleFor(x => x.OrderNo)
            .NotEmpty().WithMessage("Số đơn hàng không được để trống.")
            .MaximumLength(64).WithMessage("Số đơn hàng không được vượt quá 64 ký tự.");

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

public sealed class UpsertOrderCommandHandler : IRequestHandler<UpsertOrderCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public UpsertOrderCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(UpsertOrderCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var sourceSystem = request.SourceSystem.Trim();
        var externalId = request.ExternalId.Trim();
        var orderNo = request.OrderNo.Trim();
        var status = string.IsNullOrWhiteSpace(request.OperationalStatus)
            ? "active"
            : request.OperationalStatus.Trim();
        var externalVersion = string.IsNullOrWhiteSpace(request.ExternalVersion)
            ? null
            : request.ExternalVersion.Trim();

        var existing = await _db.Orders.FirstOrDefaultAsync(
            o => o.TenantId == tenantId
                 && o.SourceSystem == sourceSystem
                 && o.ExternalId == externalId,
            cancellationToken);

        if (existing is null)
        {
            var order = new Order
            {
                TenantId = tenantId,
                OrderNo = orderNo,
                SourceSystem = sourceSystem,
                ExternalId = externalId,
                ExternalVersion = externalVersion,
                OperationalStatus = status,
                IsActive = request.IsActive
            };
            _db.Orders.Add(order);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Concurrent insert — re-read and treat as idempotent update.
                existing = await _db.Orders.FirstOrDefaultAsync(
                    o => o.TenantId == tenantId
                         && o.SourceSystem == sourceSystem
                         && o.ExternalId == externalId,
                    cancellationToken);
                if (existing is null)
                {
                    throw new ConflictAppException(
                        "Đơn hàng với mã tham chiếu ngoài này đã tồn tại trong thuê bao.");
                }
            }

            if (existing is null)
            {
                return order.Id;
            }
        }

        existing.OrderNo = orderNo;
        existing.ExternalVersion = externalVersion;
        existing.OperationalStatus = status;
        existing.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        return existing.Id;
    }
}
