using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Waybills;

/// <summary>Creates Bill + waybill profile + thin Shipment link + Expected postage lines (ADR-0018).</summary>
public sealed record CaptureWaybillCommand(
    string BillNo,
    string? BillType,
    string? SourceSystem,
    string? ExternalId,
    Guid? OrganizationId,
    WaybillWriteBody Profile) : IRequest<Guid>;

public sealed class CaptureWaybillCommandValidator : AbstractValidator<CaptureWaybillCommand>
{
    public CaptureWaybillCommandValidator()
    {
        RuleFor(x => x.BillNo)
            .NotEmpty().WithMessage("Số vận đơn không được để trống.")
            .MaximumLength(64).WithMessage("Số vận đơn không được vượt quá 64 ký tự.");
        RuleFor(x => x.BillType).MaximumLength(64).When(x => x.BillType is not null);
        RuleFor(x => x.SourceSystem).MaximumLength(64).When(x => x.SourceSystem is not null);
        RuleFor(x => x.ExternalId).MaximumLength(128).When(x => x.ExternalId is not null);
        RuleFor(x => x.Profile)
            .Must(p => p.Sender is not null && !string.IsNullOrWhiteSpace(p.Sender.Name))
            .WithMessage("Họ tên người gửi không được để trống.");
        RuleFor(x => x.Profile)
            .Must(p => p.Consignee is not null && !string.IsNullOrWhiteSpace(p.Consignee.Name))
            .WithMessage("Họ tên người nhận không được để trống.");
        RuleFor(x => x.Profile.ParcelCount)
            .GreaterThan(0).WithMessage("Số lượng bưu gửi phải lớn hơn 0.")
            .When(x => x.Profile.ParcelCount is not null);
        RuleFor(x => x.Profile.ActualWeightKg)
            .GreaterThanOrEqualTo(0).When(x => x.Profile.ActualWeightKg is not null);
        RuleFor(x => x.Profile.ChargeableWeightKg)
            .GreaterThanOrEqualTo(0).When(x => x.Profile.ChargeableWeightKg is not null);
    }
}

public sealed class CaptureWaybillCommandHandler : IRequestHandler<CaptureWaybillCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;
    private readonly WaybillEconomicSeeder _seeder;

    public CaptureWaybillCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissions,
        IAuditWriter audit,
        WaybillEconomicSeeder seeder)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissions = permissions;
        _audit = audit;
        _seeder = seeder;
    }

    public async Task<Guid> Handle(CaptureWaybillCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.BillCreate,
            "Bạn không có quyền tạo vận đơn (Bill).",
            cancellationToken);

        var tenantId = _tenantContext.TenantId!.Value;
        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive, cancellationToken);
        if (!tenantExists)
        {
            throw new NotFoundAppException("Không tìm thấy thuê bao hoặc thuê bao không còn hiệu lực.");
        }

        Guid? organizationId = request.OrganizationId;
        if (organizationId is Guid orgId)
        {
            var orgExists = await _db.Organizations.AnyAsync(o => o.Id == orgId, cancellationToken);
            if (!orgExists)
            {
                throw new NotFoundAppException("Không tìm thấy tổ chức.");
            }
        }
        else if (_userContext.HasUser)
        {
            var actor = await _db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _userContext.UserId, cancellationToken);
            organizationId = actor?.OrganizationId;
        }

        var billNo = request.BillNo.Trim();
        var duplicate = await _db.Bills.AnyAsync(
            b => b.TenantId == tenantId && b.BillNo == billNo,
            cancellationToken);
        if (duplicate)
        {
            throw new ConflictAppException("Số vận đơn đã tồn tại trong thuê bao này.");
        }

        var sourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem)
            ? "lcms_waybill"
            : request.SourceSystem.Trim();
        var billType = string.IsNullOrWhiteSpace(request.BillType) ? "parcel" : request.BillType.Trim();

        var bill = new Bill
        {
            TenantId = tenantId,
            BillNo = billNo,
            BillType = billType,
            SourceSystem = sourceSystem,
            ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? billNo : request.ExternalId.Trim(),
            OperationalStatus = "active",
            IsActive = true,
            OrganizationId = organizationId,
            Description = TrimOrNull(request.Profile.ContentsDescription),
            EtdAt = request.Profile.SentAt?.ToUniversalTime() ?? request.Profile.AcceptedAt?.ToUniversalTime()
        };

        var waybill = new BillWaybill { TenantId = tenantId, BillId = bill.Id };
        BillWaybillMapper.Apply(waybill, request.Profile);
        WaybillChargeMath.EnsureValid(waybill);

        _db.Bills.Add(bill);
        waybill.BillId = bill.Id;
        _db.BillWaybills.Add(waybill);

        await EnsureShipmentLinkAsync(tenantId, bill, sourceSystem, cancellationToken);
        await _seeder.SeedAsync(bill, waybill, cancellationToken);

        _audit.Append(
            AuditActions.WaybillCapture,
            AuditObjectTypes.BillWaybill,
            waybill.Id,
            afterJson: AuditJson.Serialize(new
            {
                billId = bill.Id,
                billNo,
                sender = waybill.SenderName,
                consignee = waybill.ConsigneeName,
                currency = waybill.CurrencyCode,
                grandTotal = waybill.GrandTotal,
                chargeRole = waybill.ChargeEconomicRole
            }));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictAppException("Số vận đơn đã tồn tại trong thuê bao này.");
        }

        return bill.Id;
    }

    private async Task EnsureShipmentLinkAsync(
        Guid tenantId,
        Bill bill,
        string sourceSystem,
        CancellationToken cancellationToken)
    {
        var existing = await _db.Shipments.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.SourceSystem == sourceSystem && s.ExternalId == bill.BillNo,
            cancellationToken);

        Shipment shipment;
        if (existing is null)
        {
            shipment = new Shipment
            {
                TenantId = tenantId,
                ShipmentNo = bill.BillNo,
                SourceSystem = sourceSystem,
                ExternalId = bill.BillNo,
                OperationalStatus = "active",
                IsActive = true
            };
            _db.Shipments.Add(shipment);
        }
        else
        {
            shipment = existing;
        }

        var linked = await _db.BillShipmentLinks.AnyAsync(
            l => l.BillId == bill.Id && l.ShipmentId == shipment.Id,
            cancellationToken);
        if (!linked)
        {
            _db.BillShipmentLinks.Add(new BillShipmentLink
            {
                TenantId = tenantId,
                BillId = bill.Id,
                ShipmentId = shipment.Id
            });
        }
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
