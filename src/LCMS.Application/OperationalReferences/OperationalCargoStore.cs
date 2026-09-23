using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalReferences;

/// <summary>Writes typed cargo measures and parent fields from the create-form context.</summary>
public interface IOperationalCargoStore
{
    Task ApplyAsync(
        string objectType,
        Guid objectId,
        OperationalContextDocument? context,
        string? sourceSystem,
        bool externalFieldsTouched,
        CancellationToken cancellationToken);
}

public sealed class OperationalCargoStore : IOperationalCargoStore
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditWriter _audit;

    public OperationalCargoStore(ILcmsDbContext db, ITenantContext tenant, IAuditWriter audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task ApplyAsync(
        string objectType,
        Guid objectId,
        OperationalContextDocument? context,
        string? sourceSystem,
        bool externalFieldsTouched,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            return;
        }

        var type = objectType.Trim().ToLowerInvariant();
        if (!OperationalObjectTypes.CargoParents.Contains(type))
        {
            throw new ConflictAppException("Đo lường chỉ gắn trên Bill, đơn hàng hoặc Shipment.");
        }

        var owner = string.IsNullOrWhiteSpace(sourceSystem) ? OperationalSourceSystems.LcmsManual : sourceSystem.Trim();
        if (context is null)
        {
            if (externalFieldsTouched && type == OperationalObjectTypes.Bill)
            {
                await GuardAsync(type, objectId, "origin_code", owner, null, cancellationToken);
            }

            return;
        }

        await ApplyParentAsync(type, objectId, context, owner, externalFieldsTouched, cancellationToken);
        await WriteMeasure(type, objectId, MeasureCodes.PackageCount, context.PackageCount is int pcs ? (decimal?)pcs : null, "pcs", false, owner, context.ChargeableOverrideReason, cancellationToken);
        await WriteMeasure(type, objectId, MeasureCodes.GrossWeightKg, context.GrossWeightKg, "kg", false, owner, context.ChargeableOverrideReason, cancellationToken);
        await WriteMeasure(type, objectId, MeasureCodes.VolumeCbm, context.VolumeCbm, "cbm", false, owner, context.ChargeableOverrideReason, cancellationToken);
        await WriteMeasure(type, objectId, MeasureCodes.ChargeableWeightKg, context.ChargeableWeightKg, "kg", context.ChargeableConfirmed == true, owner, context.ChargeableOverrideReason, cancellationToken);
        await WriteMeasure(type, objectId, MeasureCodes.ContainerCount, context.ContainerCount is int cnt ? (decimal?)cnt : null, "cnt", false, owner, context.ChargeableOverrideReason, cancellationToken);
        await WriteMeasure(type, objectId, MeasureCodes.Teu, context.Teu, "teu", false, owner, context.ChargeableOverrideReason, cancellationToken);

        if (context.CommodityTypeId is Guid commodityId)
        {
            var exists = await _db.CommodityTypes.AsNoTracking().AnyAsync(c => c.Id == commodityId && c.IsActive, cancellationToken);
            if (!exists)
            {
                throw new NotFoundAppException("Không tìm thấy loại hàng.");
            }
        }
    }

    private async Task ApplyParentAsync(
        string type,
        Guid objectId,
        OperationalContextDocument context,
        string owner,
        bool externalFieldsTouched,
        CancellationToken cancellationToken)
    {
        if (type == OperationalObjectTypes.Bill)
        {
            // Create upserts Add the parent before SaveChanges. A database query misses that row and surfaces as HTTP 500.
            var bill = _db.Bills.Local.FirstOrDefault(b => b.Id == objectId)
                ?? await _db.Bills.FirstAsync(b => b.Id == objectId, cancellationToken);
            bill.BillDate ??= DateTimeOffset.UtcNow;
            bill.ServiceTypeCode = Trim(context.ServiceType) ?? bill.ServiceTypeCode;
            bill.IncotermCode = Trim(context.Incoterm) ?? bill.IncotermCode;
            bill.PreferredCurrency = Trim(context.PreferredCurrency) ?? bill.PreferredCurrency;
            bill.MasterBillNo = Trim(context.MasterBillNo) ?? bill.MasterBillNo;
            bill.RateDatePolicy = Trim(context.RateDatePolicy) ?? bill.RateDatePolicy;
            bill.VendorPartyId = context.VendorPartyId ?? bill.VendorPartyId;
            bill.CommodityTypeId = context.CommodityTypeId ?? bill.CommodityTypeId;
            if (externalFieldsTouched)
            {
                await GuardAsync(type, objectId, "origin_code", owner, context.ChargeableOverrideReason, cancellationToken);
            }

            return;
        }

        if (type == OperationalObjectTypes.Order)
        {
            var order = _db.Orders.Local.FirstOrDefault(o => o.Id == objectId)
                ?? await _db.Orders.FirstAsync(o => o.Id == objectId, cancellationToken);
            order.OrderDate ??= DateTimeOffset.UtcNow;
            order.ServiceTypeCode = Trim(context.ServiceType) ?? order.ServiceTypeCode;
            order.IncotermCode = Trim(context.Incoterm) ?? order.IncotermCode;
            order.RequestedAt = context.RequestedAt ?? order.RequestedAt;
            order.CommodityTypeId = context.CommodityTypeId ?? order.CommodityTypeId;
            order.ContactName = Trim(context.ContactName) ?? order.ContactName;
            order.ContactChannel = Trim(context.ContactChannel) ?? order.ContactChannel;
            order.PickupPlace = Trim(context.PickupLocation) ?? order.PickupPlace;
            order.DeliveryPlace = Trim(context.DeliveryLocation) ?? order.DeliveryPlace;
            return;
        }

        var shipment = _db.Shipments.Local.FirstOrDefault(s => s.Id == objectId)
            ?? await _db.Shipments.FirstAsync(s => s.Id == objectId, cancellationToken);
        shipment.ServiceTypeCode = Trim(context.ServiceType) ?? shipment.ServiceTypeCode;
        shipment.CommodityTypeId = context.CommodityTypeId ?? shipment.CommodityTypeId;
        shipment.CarrierName = Trim(context.CarrierName) ?? shipment.CarrierName;
        shipment.CarrierPartyId = context.VendorPartyId ?? shipment.CarrierPartyId;
    }

    private async Task WriteMeasure(
        string type,
        Guid objectId,
        string code,
        decimal? value,
        string uom,
        bool confirm,
        string owner,
        string? overrideReason,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            return;
        }

        var row = await _db.OperationalMeasurements
            .FirstOrDefaultAsync(m => m.ObjectType == type && m.ObjectId == objectId && m.MeasureCode == code, cancellationToken);
        if (row is null)
        {
            _db.OperationalMeasurements.Add(new OperationalMeasurement
            {
                TenantId = _tenant.TenantId!.Value,
                ObjectType = type,
                ObjectId = objectId,
                MeasureCode = code,
                Quantity = value.Value,
                Uom = uom,
                SourceChannel = Channel(owner),
                IsConfirmed = confirm,
                ConfirmedAt = confirm ? DateTimeOffset.UtcNow : null
            });
            return;
        }

        if (code == MeasureCodes.ChargeableWeightKg && row.IsConfirmed && row.Quantity != value.Value)
        {
            if (string.IsNullOrWhiteSpace(overrideReason))
            {
                throw new ConflictAppException("Trọng lượng tính cước đã xác nhận. Nhập lý do để ghi đè.");
            }

            row.Quantity = value.Value;
            row.IsConfirmed = confirm;
            row.ConfirmedAt = confirm ? DateTimeOffset.UtcNow : row.ConfirmedAt;
            _audit.Append(AuditActions.MeasurementOverride, type, objectId, reason: overrideReason.Trim(), afterJson: value.Value.ToString());
            return;
        }

        if (code == MeasureCodes.ChargeableWeightKg && row.IsConfirmed)
        {
            return;
        }

        row.Quantity = value.Value;
        row.Uom = uom;
        row.SourceChannel = Channel(owner);
        if (confirm)
        {
            row.IsConfirmed = true;
            row.ConfirmedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task GuardAsync(
        string type,
        Guid objectId,
        string fieldName,
        string owner,
        string? overrideReason,
        CancellationToken cancellationToken)
    {
        var owned = await _db.FieldOwnerships.FirstOrDefaultAsync(
            f => f.ObjectType == type && f.ObjectId == objectId && f.FieldName == fieldName,
            cancellationToken);
        if (owned is null)
        {
            if (!string.Equals(owner, OperationalSourceSystems.LcmsManual, StringComparison.OrdinalIgnoreCase))
            {
                _db.FieldOwnerships.Add(new FieldOwnership
                {
                    TenantId = _tenant.TenantId!.Value,
                    ObjectType = type,
                    ObjectId = objectId,
                    FieldName = fieldName,
                    OwnerSystem = owner
                });
            }

            return;
        }

        if (string.Equals(owned.OwnerSystem, owner, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(overrideReason))
        {
            throw new ConflictAppException($"Trường {fieldName} thuộc hệ thống {owned.OwnerSystem}. Nhập lý do để ghi đè.");
        }

        _audit.Append(AuditActions.FieldOverride, type, objectId, reason: overrideReason.Trim(), afterJson: fieldName);
    }

    private static string Channel(string owner) =>
        string.Equals(owner, OperationalSourceSystems.LcmsManual, StringComparison.OrdinalIgnoreCase) ? "manual" : "import";

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
