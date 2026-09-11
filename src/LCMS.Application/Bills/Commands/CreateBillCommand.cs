using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Commands;

/// <summary>Creates Bill financial anchor (TD1). Tenant lấy từ session (C-001).</summary>
public sealed record CreateBillCommand(
    string BillNo,
    string BillType,
    string? SourceSystem,
    string? ExternalId) : IRequest<Guid>;

public sealed class CreateBillCommandValidator : AbstractValidator<CreateBillCommand>
{
    public CreateBillCommandValidator()
    {
        RuleFor(x => x.BillNo)
            .NotEmpty()
            .WithMessage("Số Bill không được để trống.")
            .MaximumLength(64)
            .WithMessage("Số Bill không được vượt quá 64 ký tự.");

        RuleFor(x => x.BillType)
            .NotEmpty()
            .WithMessage("Loại Bill không được để trống.")
            .MaximumLength(64)
            .WithMessage("Loại Bill không được vượt quá 64 ký tự.");

        RuleFor(x => x.SourceSystem)
            .MaximumLength(64)
            .When(x => x.SourceSystem is not null);

        RuleFor(x => x.ExternalId)
            .MaximumLength(128)
            .When(x => x.ExternalId is not null);
    }
}

public sealed class CreateBillCommandHandler : IRequestHandler<CreateBillCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateBillCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateBillCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive, cancellationToken);
        if (!tenantExists)
        {
            throw new NotFoundAppException("Không tìm thấy thuê bao hoặc thuê bao không còn hiệu lực.");
        }

        var billNo = request.BillNo.Trim();
        var duplicate = await _db.Bills.AnyAsync(
            b => b.TenantId == tenantId && b.BillNo == billNo,
            cancellationToken);
        if (duplicate)
        {
            throw new ConflictAppException("Số Bill đã tồn tại trong thuê bao này.");
        }

        var bill = new Bill
        {
            TenantId = tenantId,
            BillNo = billNo,
            BillType = request.BillType.Trim(),
            SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? null : request.SourceSystem.Trim(),
            ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim(),
            OperationalStatus = "active",
            IsActive = true
        };

        _db.Bills.Add(bill);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Số Bill đã tồn tại trong thuê bao này.");
        }

        return bill.Id;
    }
}
