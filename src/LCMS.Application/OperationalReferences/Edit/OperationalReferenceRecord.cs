using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalReferences.Edit;

/// <summary>
/// Uniform view over Bill / Order / Shipment / Chặng / Chuyến for the edit pattern (ADR-0039).
/// Reads and writes typed fields by catalog code; measures live in operational_measurements.
/// </summary>
internal sealed class OperationalReferenceRecord
{
    private OperationalReferenceRecord(string type, Guid id)
    {
        Type = type;
        Id = id;
    }

    public string Type { get; }
    public Guid Id { get; }
    public string Code { get; private set; } = string.Empty;
    public string? SourceSystem { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public Bill? Bill { get; private set; }
    public Order? Order { get; private set; }
    public Shipment? Shipment { get; private set; }
    public TransportLeg? Leg { get; private set; }
    public TransportMovement? Movement { get; private set; }

    public OperationalContextDocument? Context { get; private set; }
    public bool ContextDirty { get; private set; }
    public List<OperationalMeasurement> Measures { get; private set; } = [];
    public Dictionary<string, OperationalFieldOverride> Overrides { get; private set; } = new(StringComparer.Ordinal);
    public Dictionary<string, FieldOwnership> Ownerships { get; private set; } = new(StringComparer.Ordinal);

    public bool IsExternal =>
        !string.IsNullOrWhiteSpace(SourceSystem)
        && !string.Equals(SourceSystem, OperationalSourceSystems.LcmsManual, StringComparison.OrdinalIgnoreCase);

    public string? TransportMode => Type switch
    {
        OperationalObjectTypes.Bill => Bill!.TransportMode,
        OperationalObjectTypes.Order => Order!.TransportMode,
        OperationalObjectTypes.Shipment => Shipment!.TransportMode,
        OperationalObjectTypes.Movement => Movement!.TransportMode,
        _ => null
    };

    public static string NormalizeType(string raw)
    {
        var type = (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "orders" => OperationalObjectTypes.Order,
            "shipments" => OperationalObjectTypes.Shipment,
            "legs" or "transport-legs" or "transport_leg" => OperationalObjectTypes.Leg,
            "movements" or "transport-movements" or "transport_movement" => OperationalObjectTypes.Movement,
            "bills" => OperationalObjectTypes.Bill,
            var t => t
        };
        if (!OperationalObjectTypes.All.Contains(type))
        {
            throw new NotFoundAppException("Loại tham chiếu vận hành không được hỗ trợ.");
        }

        return type;
    }

    public static async Task<OperationalReferenceRecord> LoadAsync(
        ILcmsDbContext db,
        string type,
        Guid id,
        bool track,
        CancellationToken cancellationToken)
    {
        var record = new OperationalReferenceRecord(type, id);
        switch (type)
        {
            case OperationalObjectTypes.Bill:
            {
                var q = track ? db.Bills : db.Bills.AsNoTracking();
                var bill = await q.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
                    ?? throw new NotFoundAppException("Không tìm thấy Bill.");
                record.Bill = bill;
                record.Code = bill.BillNo;
                record.SourceSystem = bill.SourceSystem;
                record.CreatedBy = bill.CreatedBy;
                record.OrganizationId = bill.OrganizationId;
                record.RowVersion = bill.RowVersion;
                record.Context = OperationalContextJson.Deserialize(bill.ContextJson);
                break;
            }
            case OperationalObjectTypes.Order:
            {
                var q = track ? db.Orders : db.Orders.AsNoTracking();
                var order = await q.FirstOrDefaultAsync(o => o.Id == id, cancellationToken)
                    ?? throw new NotFoundAppException("Không tìm thấy đơn hàng.");
                record.Order = order;
                record.Code = order.OrderNo;
                record.SourceSystem = order.SourceSystem;
                record.CreatedBy = order.CreatedBy;
                record.RowVersion = order.RowVersion;
                record.Context = OperationalContextJson.Deserialize(order.ContextJson);
                break;
            }
            case OperationalObjectTypes.Shipment:
            {
                var q = track ? db.Shipments : db.Shipments.AsNoTracking();
                var shipment = await q.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                    ?? throw new NotFoundAppException("Không tìm thấy lô hàng.");
                record.Shipment = shipment;
                record.Code = shipment.ShipmentNo;
                record.SourceSystem = shipment.SourceSystem;
                record.CreatedBy = shipment.CreatedBy;
                record.RowVersion = shipment.RowVersion;
                record.Context = OperationalContextJson.Deserialize(shipment.ContextJson);
                break;
            }
            case OperationalObjectTypes.Leg:
            {
                var q = track ? db.TransportLegs : db.TransportLegs.AsNoTracking();
                var leg = await q.FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
                    ?? throw new NotFoundAppException("Không tìm thấy chặng vận chuyển.");
                record.Leg = leg;
                record.Code = leg.LegNo;
                record.SourceSystem = leg.SourceSystem;
                record.CreatedBy = leg.CreatedBy;
                record.RowVersion = leg.RowVersion;
                break;
            }
            default:
            {
                var q = track ? db.TransportMovements : db.TransportMovements.AsNoTracking();
                var movement = await q.FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
                    ?? throw new NotFoundAppException("Không tìm thấy chuyến vận chuyển.");
                record.Movement = movement;
                record.Code = movement.MovementNo;
                record.SourceSystem = movement.SourceSystem;
                record.CreatedBy = movement.CreatedBy;
                record.RowVersion = movement.RowVersion;
                break;
            }
        }

        var measures = track ? db.OperationalMeasurements : db.OperationalMeasurements.AsNoTracking();
        record.Measures = await measures
            .Where(m => m.ObjectType == type && m.ObjectId == id)
            .ToListAsync(cancellationToken);
        var overrides = track ? db.OperationalFieldOverrides : db.OperationalFieldOverrides.AsNoTracking();
        record.Overrides = (await overrides
                .Where(o => o.ObjectType == type && o.ObjectId == id)
                .ToListAsync(cancellationToken))
            .ToDictionary(o => o.FieldCode, StringComparer.Ordinal);
        record.Ownerships = (await db.FieldOwnerships.AsNoTracking()
                .Where(f => f.ObjectType == type && f.ObjectId == id)
                .ToListAsync(cancellationToken))
            .GroupBy(f => f.FieldName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        return record;
    }

    public OperationalMeasurement? Measure(string measureCode) =>
        Measures.FirstOrDefault(m => m.MeasureCode == measureCode);

    /// <summary>External owner of a field (API-integrated SoT), or null when LCMS may edit it.</summary>
    public string? ExternalOwner(OperationalFieldDef def)
    {
        if (def.OwnershipKey is null || !Ownerships.TryGetValue(def.OwnershipKey, out var owned))
        {
            return null;
        }

        return string.Equals(owned.OwnerSystem, OperationalSourceSystems.LcmsManual, StringComparison.OrdinalIgnoreCase)
            ? null
            : owned.OwnerSystem;
    }

    public string? Get(string code)
    {
        switch (code)
        {
            case OperationalFieldCodes.PackageCount:
                return OperationalFieldValues.FormatDecimal(Measure(MeasureCodes.PackageCount)?.Quantity)
                    ?? OperationalFieldValues.FormatInt(Context?.PackageCount);
            case OperationalFieldCodes.GrossWeightKg:
                return OperationalFieldValues.FormatDecimal(Measure(MeasureCodes.GrossWeightKg)?.Quantity ?? Context?.GrossWeightKg);
            case OperationalFieldCodes.VolumeCbm:
                return OperationalFieldValues.FormatDecimal(Measure(MeasureCodes.VolumeCbm)?.Quantity ?? Context?.VolumeCbm);
            case OperationalFieldCodes.ChargeableWeightKg:
                return OperationalFieldValues.FormatDecimal(Measure(MeasureCodes.ChargeableWeightKg)?.Quantity);
            case OperationalFieldCodes.CommodityTypeId:
                return (Bill?.CommodityTypeId ?? Order?.CommodityTypeId ?? Shipment?.CommodityTypeId)?.ToString();
            case OperationalFieldCodes.SpecialFlags:
                return Context?.SpecialFlags is { Count: > 0 } flags
                    ? string.Join(",", flags.OrderBy(f => f, StringComparer.Ordinal))
                    : null;
            case OperationalFieldCodes.CargoDescription:
                return Context?.CargoDescription;
            case OperationalFieldCodes.TransportMode:
                return TransportMode;
            case OperationalFieldCodes.ServiceTypeCode:
                return Bill?.ServiceTypeCode ?? Order?.ServiceTypeCode ?? Shipment?.ServiceTypeCode;
            case OperationalFieldCodes.OriginCode:
                return Bill?.OriginCode ?? Order?.OriginCode ?? Shipment?.OriginCode ?? Leg?.OriginCode;
            case OperationalFieldCodes.DestinationCode:
                return Bill?.DestinationCode ?? Order?.DestinationCode ?? Shipment?.DestinationCode ?? Leg?.DestinationCode;
            case OperationalFieldCodes.RouteCode:
                return Bill?.RouteCode ?? Order?.RouteCode ?? Shipment?.RouteCode;
            case OperationalFieldCodes.EtdAt:
                return OperationalFieldValues.FormatDate(Bill?.EtdAt ?? Order?.EtdAt ?? Shipment?.EtdAt);
            case OperationalFieldCodes.EtaAt:
                return OperationalFieldValues.FormatDate(Bill?.EtaAt ?? Order?.EtaAt ?? Shipment?.EtaAt);
            case OperationalFieldCodes.CustomerReference:
                return Bill?.CustomerReference ?? Order?.CustomerReference ?? Shipment?.CustomerReference;
            case OperationalFieldCodes.Description:
                return Bill?.Description ?? Order?.Description ?? Shipment?.Description;
            case OperationalFieldCodes.CarrierName:
                return Shipment?.CarrierName;
            case OperationalFieldCodes.SequenceNo:
                return OperationalFieldValues.FormatInt(Leg?.SequenceNo);
            case OperationalFieldCodes.MovementOn:
                return OperationalFieldValues.FormatDate(Movement?.MovementOn);
            default:
                return null;
        }
    }

    /// <summary>Writes a non-measure field. Value is already validated and canonical.</summary>
    public void SetEntityField(string code, string? value)
    {
        switch (code)
        {
            case OperationalFieldCodes.CommodityTypeId:
                var commodity = value is null ? (Guid?)null : Guid.Parse(value);
                if (Bill is not null) Bill.CommodityTypeId = commodity;
                if (Order is not null) Order.CommodityTypeId = commodity;
                if (Shipment is not null) Shipment.CommodityTypeId = commodity;
                break;
            case OperationalFieldCodes.SpecialFlags:
                EnsureContext().SpecialFlags = value is null
                    ? null
                    : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                ContextDirty = true;
                break;
            case OperationalFieldCodes.CargoDescription:
                EnsureContext().CargoDescription = value;
                ContextDirty = true;
                break;
            case OperationalFieldCodes.TransportMode:
                if (Bill is not null) Bill.TransportMode = value;
                if (Order is not null) Order.TransportMode = value;
                if (Shipment is not null) Shipment.TransportMode = value;
                if (Movement is not null) Movement.TransportMode = value;
                break;
            case OperationalFieldCodes.ServiceTypeCode:
                if (Bill is not null) Bill.ServiceTypeCode = value;
                if (Order is not null) Order.ServiceTypeCode = value;
                if (Shipment is not null) Shipment.ServiceTypeCode = value;
                break;
            case OperationalFieldCodes.OriginCode:
                if (Bill is not null) Bill.OriginCode = value;
                if (Order is not null) Order.OriginCode = value;
                if (Shipment is not null) Shipment.OriginCode = value;
                if (Leg is not null) Leg.OriginCode = value;
                break;
            case OperationalFieldCodes.DestinationCode:
                if (Bill is not null) Bill.DestinationCode = value;
                if (Order is not null) Order.DestinationCode = value;
                if (Shipment is not null) Shipment.DestinationCode = value;
                if (Leg is not null) Leg.DestinationCode = value;
                break;
            case OperationalFieldCodes.RouteCode:
                if (Bill is not null) Bill.RouteCode = value;
                if (Order is not null) Order.RouteCode = value;
                if (Shipment is not null) Shipment.RouteCode = value;
                break;
            case OperationalFieldCodes.EtdAt:
                var etd = value is null ? (DateTimeOffset?)null : DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                if (Bill is not null) Bill.EtdAt = etd;
                if (Order is not null) Order.EtdAt = etd;
                if (Shipment is not null) Shipment.EtdAt = etd;
                break;
            case OperationalFieldCodes.EtaAt:
                var eta = value is null ? (DateTimeOffset?)null : DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                if (Bill is not null) Bill.EtaAt = eta;
                if (Order is not null) Order.EtaAt = eta;
                if (Shipment is not null) Shipment.EtaAt = eta;
                break;
            case OperationalFieldCodes.CustomerReference:
                if (Bill is not null) Bill.CustomerReference = value;
                if (Order is not null) Order.CustomerReference = value;
                if (Shipment is not null) Shipment.CustomerReference = value;
                break;
            case OperationalFieldCodes.Description:
                if (Bill is not null) Bill.Description = value;
                if (Order is not null) Order.Description = value;
                if (Shipment is not null) Shipment.Description = value;
                break;
            case OperationalFieldCodes.CarrierName:
                if (Shipment is not null) Shipment.CarrierName = value;
                break;
            case OperationalFieldCodes.SequenceNo:
                if (Leg is not null) Leg.SequenceNo = value is null ? 0 : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                break;
            case OperationalFieldCodes.MovementOn:
                if (Movement is not null)
                {
                    Movement.MovementOn = value is null ? null : DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                }

                break;
        }
    }

    /// <summary>Mirrors a measure into the create-form context so detail grids stay consistent.</summary>
    public void MirrorMeasureToContext(string code, decimal? value)
    {
        if (Type is not (OperationalObjectTypes.Bill or OperationalObjectTypes.Order or OperationalObjectTypes.Shipment))
        {
            return;
        }

        var ctx = EnsureContext();
        switch (code)
        {
            case OperationalFieldCodes.PackageCount:
                ctx.PackageCount = value is null ? null : (int)value.Value;
                break;
            case OperationalFieldCodes.GrossWeightKg:
                ctx.GrossWeightKg = value;
                break;
            case OperationalFieldCodes.VolumeCbm:
                ctx.VolumeCbm = value;
                break;
            case OperationalFieldCodes.ChargeableWeightKg:
                ctx.ChargeableWeightKg = value;
                break;
        }

        ContextDirty = true;
    }

    public void FlushContext()
    {
        if (!ContextDirty)
        {
            return;
        }

        var json = OperationalContextJson.Serialize(Context);
        if (Bill is not null) Bill.ContextJson = json;
        if (Order is not null) Order.ContextJson = json;
        if (Shipment is not null) Shipment.ContextJson = json;
    }

    private OperationalContextDocument EnsureContext() => Context ??= new OperationalContextDocument();
}
