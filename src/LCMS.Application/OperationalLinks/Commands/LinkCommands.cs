using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalLinks.Commands;

public sealed record LinkOrderToBillCommand(Guid OrderId, Guid BillId) : IRequest<Guid>;

public sealed class LinkOrderToBillCommandValidator : AbstractValidator<LinkOrderToBillCommand>
{
    public LinkOrderToBillCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("Đơn hàng không hợp lệ.");
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
    }
}

public sealed class LinkOrderToBillCommandHandler : IRequestHandler<LinkOrderToBillCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public LinkOrderToBillCommandHandler(ILcmsDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<Guid> Handle(LinkOrderToBillCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        var orderExists = await _db.Orders.AnyAsync(o => o.Id == request.OrderId, cancellationToken);
        if (!orderExists)
        {
            throw new NotFoundAppException("Không tìm thấy đơn hàng.");
        }

        var billExists = await _db.Bills.AnyAsync(b => b.Id == request.BillId, cancellationToken);
        if (!billExists)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        var existing = await _db.OrderBillLinks.FirstOrDefaultAsync(
            l => l.TenantId == tenantId
                 && l.OrderId == request.OrderId
                 && l.BillId == request.BillId,
            cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var link = new OrderBillLink
        {
            TenantId = tenantId,
            OrderId = request.OrderId,
            BillId = request.BillId
        };
        _db.OrderBillLinks.Add(link);
        _audit.Append(AuditActions.LinkCreate, AuditObjectTypes.OperationalLink, link.Id, afterJson: $"{request.OrderId}:{request.BillId}");
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            existing = await _db.OrderBillLinks.FirstOrDefaultAsync(
                l => l.TenantId == tenantId
                     && l.OrderId == request.OrderId
                     && l.BillId == request.BillId,
                cancellationToken);
            if (existing is null)
            {
                throw new ConflictAppException("Liên kết đơn hàng–Bill đã tồn tại.");
            }

            return existing.Id;
        }

        return link.Id;
    }
}

public sealed record LinkBillToShipmentCommand(Guid BillId, Guid ShipmentId) : IRequest<Guid>;

public sealed class LinkBillToShipmentCommandValidator : AbstractValidator<LinkBillToShipmentCommand>
{
    public LinkBillToShipmentCommandValidator()
    {
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.ShipmentId).NotEmpty().WithMessage("Lô hàng không hợp lệ.");
    }
}

public sealed class LinkBillToShipmentCommandHandler : IRequestHandler<LinkBillToShipmentCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditWriter _audit;

    public LinkBillToShipmentCommandHandler(ILcmsDbContext db, ITenantContext tenantContext, IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _audit = audit;
    }

    public async Task<Guid> Handle(LinkBillToShipmentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        var billExists = await _db.Bills.AnyAsync(b => b.Id == request.BillId, cancellationToken);
        if (!billExists)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        var shipmentExists = await _db.Shipments.AnyAsync(s => s.Id == request.ShipmentId, cancellationToken);
        if (!shipmentExists)
        {
            throw new NotFoundAppException("Không tìm thấy lô hàng.");
        }

        var existing = await _db.BillShipmentLinks.FirstOrDefaultAsync(
            l => l.TenantId == tenantId
                 && l.BillId == request.BillId
                 && l.ShipmentId == request.ShipmentId,
            cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var link = new BillShipmentLink
        {
            TenantId = tenantId,
            BillId = request.BillId,
            ShipmentId = request.ShipmentId
        };
        _db.BillShipmentLinks.Add(link);
        _audit.Append(AuditActions.LinkCreate, AuditObjectTypes.OperationalLink, link.Id, afterJson: $"{request.BillId}:{request.ShipmentId}");
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            existing = await _db.BillShipmentLinks.FirstOrDefaultAsync(
                l => l.TenantId == tenantId
                     && l.BillId == request.BillId
                     && l.ShipmentId == request.ShipmentId,
                cancellationToken);
            if (existing is null)
            {
                throw new ConflictAppException("Liên kết Bill–lô hàng đã tồn tại.");
            }

            return existing.Id;
        }

        return link.Id;
    }
}
