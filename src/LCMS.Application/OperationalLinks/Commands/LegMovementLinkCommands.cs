using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalLinks.Commands;

public sealed record LinkBillToLegCommand(Guid BillId, Guid TransportLegId) : IRequest<Guid>;

public sealed class LinkBillToLegCommandValidator : AbstractValidator<LinkBillToLegCommand>
{
    public LinkBillToLegCommandValidator()
    {
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.TransportLegId).NotEmpty().WithMessage("Chặng vận chuyển không hợp lệ.");
    }
}

public sealed class LinkBillToLegCommandHandler : IRequestHandler<LinkBillToLegCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public LinkBillToLegCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(LinkBillToLegCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        if (!await _db.Bills.AnyAsync(b => b.Id == request.BillId, cancellationToken))
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        if (!await _db.TransportLegs.AnyAsync(l => l.Id == request.TransportLegId, cancellationToken))
        {
            throw new NotFoundAppException("Không tìm thấy chặng vận chuyển.");
        }

        var existing = await _db.BillLegLinks.FirstOrDefaultAsync(
            l => l.TenantId == tenantId
                 && l.BillId == request.BillId
                 && l.TransportLegId == request.TransportLegId,
            cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var link = new BillLegLink
        {
            TenantId = tenantId,
            BillId = request.BillId,
            TransportLegId = request.TransportLegId
        };
        _db.BillLegLinks.Add(link);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            existing = await _db.BillLegLinks.FirstOrDefaultAsync(
                l => l.TenantId == tenantId
                     && l.BillId == request.BillId
                     && l.TransportLegId == request.TransportLegId,
                cancellationToken);
            if (existing is null)
            {
                throw new ConflictAppException("Liên kết Bill–chặng vận chuyển đã tồn tại.");
            }

            return existing.Id;
        }

        return link.Id;
    }
}

public sealed record LinkLegToMovementCommand(Guid TransportLegId, Guid TransportMovementId) : IRequest<Guid>;

public sealed class LinkLegToMovementCommandValidator : AbstractValidator<LinkLegToMovementCommand>
{
    public LinkLegToMovementCommandValidator()
    {
        RuleFor(x => x.TransportLegId).NotEmpty().WithMessage("Chặng vận chuyển không hợp lệ.");
        RuleFor(x => x.TransportMovementId).NotEmpty().WithMessage("Chuyến vận chuyển không hợp lệ.");
    }
}

public sealed class LinkLegToMovementCommandHandler : IRequestHandler<LinkLegToMovementCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public LinkLegToMovementCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(LinkLegToMovementCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        if (!await _db.TransportLegs.AnyAsync(l => l.Id == request.TransportLegId, cancellationToken))
        {
            throw new NotFoundAppException("Không tìm thấy chặng vận chuyển.");
        }

        if (!await _db.TransportMovements.AnyAsync(m => m.Id == request.TransportMovementId, cancellationToken))
        {
            throw new NotFoundAppException("Không tìm thấy chuyến vận chuyển.");
        }

        var existing = await _db.LegMovementLinks.FirstOrDefaultAsync(
            l => l.TenantId == tenantId
                 && l.TransportLegId == request.TransportLegId
                 && l.TransportMovementId == request.TransportMovementId,
            cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var link = new LegMovementLink
        {
            TenantId = tenantId,
            TransportLegId = request.TransportLegId,
            TransportMovementId = request.TransportMovementId
        };
        _db.LegMovementLinks.Add(link);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            existing = await _db.LegMovementLinks.FirstOrDefaultAsync(
                l => l.TenantId == tenantId
                     && l.TransportLegId == request.TransportLegId
                     && l.TransportMovementId == request.TransportMovementId,
                cancellationToken);
            if (existing is null)
            {
                throw new ConflictAppException("Liên kết chặng–chuyến vận chuyển đã tồn tại.");
            }

            return existing.Id;
        }

        return link.Id;
    }
}

public sealed record LinkBillToMovementCommand(Guid BillId, Guid TransportMovementId) : IRequest<Guid>;

public sealed class LinkBillToMovementCommandValidator : AbstractValidator<LinkBillToMovementCommand>
{
    public LinkBillToMovementCommandValidator()
    {
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.TransportMovementId).NotEmpty().WithMessage("Chuyến vận chuyển không hợp lệ.");
    }
}

public sealed class LinkBillToMovementCommandHandler : IRequestHandler<LinkBillToMovementCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public LinkBillToMovementCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(LinkBillToMovementCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        if (!await _db.Bills.AnyAsync(b => b.Id == request.BillId, cancellationToken))
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        if (!await _db.TransportMovements.AnyAsync(m => m.Id == request.TransportMovementId, cancellationToken))
        {
            throw new NotFoundAppException("Không tìm thấy chuyến vận chuyển.");
        }

        var existing = await _db.BillMovementLinks.FirstOrDefaultAsync(
            l => l.TenantId == tenantId
                 && l.BillId == request.BillId
                 && l.TransportMovementId == request.TransportMovementId,
            cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var link = new BillMovementLink
        {
            TenantId = tenantId,
            BillId = request.BillId,
            TransportMovementId = request.TransportMovementId
        };
        _db.BillMovementLinks.Add(link);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            existing = await _db.BillMovementLinks.FirstOrDefaultAsync(
                l => l.TenantId == tenantId
                     && l.BillId == request.BillId
                     && l.TransportMovementId == request.TransportMovementId,
                cancellationToken);
            if (existing is null)
            {
                throw new ConflictAppException("Liên kết Bill–chuyến vận chuyển đã tồn tại.");
            }

            return existing.Id;
        }

        return link.Id;
    }
}
