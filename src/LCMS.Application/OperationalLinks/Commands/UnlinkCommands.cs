using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalLinks.Commands;

/// <summary>W-K3 — soft-delete operational links; history via audit (no hard delete).</summary>
public sealed record UnlinkOrderBillCommand(Guid OrderId, Guid BillId, string? Reason) : IRequest;

public sealed class UnlinkOrderBillCommandValidator : AbstractValidator<UnlinkOrderBillCommand>
{
    public UnlinkOrderBillCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty().WithMessage("Đơn hàng không hợp lệ.");
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.Reason).MaximumLength(512).When(x => x.Reason is not null);
    }
}

public sealed class UnlinkOrderBillCommandHandler : IRequestHandler<UnlinkOrderBillCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public UnlinkOrderBillCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(UnlinkOrderBillCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var link = await _db.OrderBillLinks.FirstOrDefaultAsync(
            l => l.OrderId == request.OrderId && l.BillId == request.BillId,
            cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy liên kết đơn hàng–Bill.");

        link.SoftDelete(_user.UserId);
        _audit.Append(
            AuditActions.LinkRemove,
            AuditObjectTypes.OperationalLink,
            link.Id,
            beforeJson: $"{request.OrderId}:{request.BillId}",
            reason: request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record UnlinkBillShipmentCommand(Guid BillId, Guid ShipmentId, string? Reason) : IRequest;

public sealed class UnlinkBillShipmentCommandValidator : AbstractValidator<UnlinkBillShipmentCommand>
{
    public UnlinkBillShipmentCommandValidator()
    {
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.ShipmentId).NotEmpty().WithMessage("Lô hàng không hợp lệ.");
        RuleFor(x => x.Reason).MaximumLength(512).When(x => x.Reason is not null);
    }
}

public sealed class UnlinkBillShipmentCommandHandler : IRequestHandler<UnlinkBillShipmentCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public UnlinkBillShipmentCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(UnlinkBillShipmentCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var link = await _db.BillShipmentLinks.FirstOrDefaultAsync(
            l => l.BillId == request.BillId && l.ShipmentId == request.ShipmentId,
            cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy liên kết Bill–lô hàng.");

        link.SoftDelete(_user.UserId);
        _audit.Append(
            AuditActions.LinkRemove,
            AuditObjectTypes.OperationalLink,
            link.Id,
            beforeJson: $"{request.BillId}:{request.ShipmentId}",
            afterJson: null,
            reason: request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record UnlinkBillLegCommand(Guid BillId, Guid TransportLegId, string? Reason) : IRequest;

public sealed class UnlinkBillLegCommandValidator : AbstractValidator<UnlinkBillLegCommand>
{
    public UnlinkBillLegCommandValidator()
    {
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.TransportLegId).NotEmpty().WithMessage("Chặng không hợp lệ.");
        RuleFor(x => x.Reason).MaximumLength(512).When(x => x.Reason is not null);
    }
}

public sealed class UnlinkBillLegCommandHandler : IRequestHandler<UnlinkBillLegCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public UnlinkBillLegCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(UnlinkBillLegCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var link = await _db.BillLegLinks.FirstOrDefaultAsync(
            l => l.BillId == request.BillId && l.TransportLegId == request.TransportLegId,
            cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy liên kết Bill–chặng trực tiếp.");

        link.SoftDelete(_user.UserId);
        _audit.Append(
            AuditActions.LinkRemove,
            AuditObjectTypes.OperationalLink,
            link.Id,
            beforeJson: $"{request.BillId}:{request.TransportLegId}",
            afterJson: null,
            reason: request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record UnlinkBillMovementCommand(Guid BillId, Guid TransportMovementId, string? Reason) : IRequest;

public sealed class UnlinkBillMovementCommandValidator : AbstractValidator<UnlinkBillMovementCommand>
{
    public UnlinkBillMovementCommandValidator()
    {
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.TransportMovementId).NotEmpty().WithMessage("Chuyến không hợp lệ.");
        RuleFor(x => x.Reason).MaximumLength(512).When(x => x.Reason is not null);
    }
}

public sealed class UnlinkBillMovementCommandHandler : IRequestHandler<UnlinkBillMovementCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public UnlinkBillMovementCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _audit = audit;
    }

    public async Task Handle(UnlinkBillMovementCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var link = await _db.BillMovementLinks.FirstOrDefaultAsync(
            l => l.BillId == request.BillId && l.TransportMovementId == request.TransportMovementId,
            cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy liên kết Bill–chuyến trực tiếp.");

        link.SoftDelete(_user.UserId);
        _audit.Append(
            AuditActions.LinkRemove,
            AuditObjectTypes.OperationalLink,
            link.Id,
            beforeJson: $"{request.BillId}:{request.TransportMovementId}",
            afterJson: null,
            reason: request.Reason);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
