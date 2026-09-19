using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Bills.Waybills;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Commands;

/// <summary>Upserts the waybill profile on an existing Bill (ADR-0018).</summary>
public sealed record UpsertBillWaybillCommand(Guid BillId, WaybillWriteBody Profile) : IRequest;

public sealed class UpsertBillWaybillCommandValidator : AbstractValidator<UpsertBillWaybillCommand>
{
    public UpsertBillWaybillCommandValidator()
    {
        RuleFor(x => x.BillId).NotEmpty();
        RuleFor(x => x.Profile)
            .Must(p => p.Sender is not null && !string.IsNullOrWhiteSpace(p.Sender.Name))
            .WithMessage("Họ tên người gửi không được để trống.");
        RuleFor(x => x.Profile)
            .Must(p => p.Consignee is not null && !string.IsNullOrWhiteSpace(p.Consignee.Name))
            .WithMessage("Họ tên người nhận không được để trống.");
    }
}

public sealed class UpsertBillWaybillCommandHandler : IRequestHandler<UpsertBillWaybillCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;
    private readonly WaybillEconomicSeeder _seeder;

    public UpsertBillWaybillCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IAuditWriter audit,
        WaybillEconomicSeeder seeder)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _audit = audit;
        _seeder = seeder;
    }

    public async Task Handle(UpsertBillWaybillCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var canUpdate = await _permissions.HasPermissionAsync(PermissionCodes.BillUpdate, cancellationToken);
        await _permissions.EnsureAsync(
            canUpdate ? PermissionCodes.BillUpdate : PermissionCodes.BillCreate,
            "Bạn không có quyền cập nhật vận đơn.",
            cancellationToken);

        var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        var waybill = await _db.BillWaybills.FirstOrDefaultAsync(
            w => w.BillId == request.BillId,
            cancellationToken);
        var created = waybill is null;
        if (waybill is null)
        {
            waybill = new BillWaybill { TenantId = bill.TenantId, BillId = bill.Id };
            _db.BillWaybills.Add(waybill);
        }

        BillWaybillMapper.Apply(waybill, request.Profile);
        WaybillChargeMath.EnsureValid(waybill);

        if (string.IsNullOrWhiteSpace(bill.Description) && !string.IsNullOrWhiteSpace(waybill.ContentsDescription))
        {
            bill.Description = waybill.ContentsDescription;
        }

        bill.EtdAt ??= waybill.SentAt ?? waybill.AcceptedAt;

        await _seeder.SeedAsync(bill, waybill, cancellationToken);
        _audit.Append(
            created ? AuditActions.WaybillCapture : AuditActions.WaybillUpdate,
            AuditObjectTypes.BillWaybill,
            waybill.Id,
            afterJson: AuditJson.Serialize(new
            {
                billId = bill.Id,
                sender = waybill.SenderName,
                consignee = waybill.ConsigneeName,
                grandTotal = waybill.GrandTotal
            }));

        await _db.SaveChangesAsync(cancellationToken);
    }
}
