using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Queries;

public sealed record EligibleBillAllocationDto(
    Guid BillId,
    string BillNo,
    Guid? CustomerPartyId,
    string? CustomerName,
    decimal? GrossWeightKg,
    decimal? VolumeCbm,
    decimal? ChargeableWeightKg,
    decimal BasisValue
);

public sealed record SharedCostAllocationContextDto(
    IReadOnlyList<EligibleBillAllocationDto> EligibleBills,
    string SuggestedBasis,
    string BasisReason,
    string? TransportMode,
    string NoticeMessage
);

public sealed record GetSharedCostAllocationContextQuery(
    Guid? ShipmentId,
    Guid? TransportLegId,
    Guid? TransportMovementId) : IRequest<SharedCostAllocationContextDto>;

public sealed class GetSharedCostAllocationContextQueryHandler
    : IRequestHandler<GetSharedCostAllocationContextQuery, SharedCostAllocationContextDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public GetSharedCostAllocationContextQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SharedCostAllocationContextDto> Handle(
        GetSharedCostAllocationContextQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenant.TenantId!.Value;
        var billIds = new HashSet<Guid>();
        string? mode = null;

        if (request.ShipmentId.HasValue)
        {
            var shipment = await _db.Shipments.AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == request.ShipmentId.Value, cancellationToken);
            mode = shipment?.TransportMode;

            var ids = await _db.BillShipmentLinks.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.ShipmentId == request.ShipmentId.Value)
                .Select(l => l.BillId)
                .ToListAsync(cancellationToken);
            foreach (var id in ids) billIds.Add(id);
        }
        else if (request.TransportLegId.HasValue)
        {
            var leg = await _db.TransportLegs.AsNoTracking()
                .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == request.TransportLegId.Value, cancellationToken);
            mode = leg?.TransportMode;

            var ids = await _db.BillLegLinks.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.TransportLegId == request.TransportLegId.Value)
                .Select(l => l.BillId)
                .ToListAsync(cancellationToken);
            foreach (var id in ids) billIds.Add(id);
        }
        else if (request.TransportMovementId.HasValue)
        {
            var mov = await _db.TransportMovements.AsNoTracking()
                .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == request.TransportMovementId.Value, cancellationToken);
            mode = mov?.TransportMode;

            var ids = await _db.BillMovementLinks.AsNoTracking()
                .Where(l => l.TenantId == tenantId && l.TransportMovementId == request.TransportMovementId.Value)
                .Select(l => l.BillId)
                .ToListAsync(cancellationToken);
            foreach (var id in ids) billIds.Add(id);
        }

        if (billIds.Count == 0)
        {
            return new SharedCostAllocationContextDto(
                Array.Empty<EligibleBillAllocationDto>(),
                "bill_count",
                "Chưa liên kết vận đơn nào với thực thể vận hành này.",
                mode,
                "Không tìm thấy vận đơn đủ điều kiện phân bổ.");
        }

        var bills = await _db.Bills.AsNoTracking()
            .Where(b => b.TenantId == tenantId && billIds.Contains(b.Id))
            .OrderBy(b => b.BillNo)
            .ToListAsync(cancellationToken);

        var billGuids = bills.Select(b => b.Id).ToList();
        var measurements = await _db.OperationalMeasurements.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ObjectType == OperationalObjectTypes.Bill && billGuids.Contains(m.ObjectId))
            .ToListAsync(cancellationToken);

        var customers = await _db.BusinessParties.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .ToDictionaryAsync(p => p.Id, p => p.LegalName, cancellationToken);

        string suggestedBasis;
        string basisReason;

        var normalizedMode = ShipmentModePolicy.Normalize(mode) ?? "";
        if (normalizedMode == "AIR")
        {
            suggestedBasis = "chargeable_weight";
            basisReason = "Hàng không: Gợi ý phân bổ theo Trọng lượng tính cước (CW).";
        }
        else if (ShipmentModePolicy.IsSeaFamily(normalizedMode))
        {
            suggestedBasis = "cbm";
            basisReason = "Đường biển: Gợi ý phân bổ theo Thể tích hàng hóa (CBM).";
        }
        else if (normalizedMode is "ROAD" or "RAIL")
        {
            suggestedBasis = "gross_weight";
            basisReason = "Đường bộ / đường sắt: Gợi ý phân bổ theo Trọng lượng thực tế (Gross Weight).";
        }
        else
        {
            suggestedBasis = "chargeable_weight";
            basisReason = "Mặc định: Phân bổ theo Trọng lượng tính cước (CW).";
        }

        var eligibleList = new List<EligibleBillAllocationDto>();
        foreach (var b in bills)
        {
            var ctx = OperationalContextJson.Deserialize(b.ContextJson);
            var g = measurements.FirstOrDefault(m => m.ObjectId == b.Id && m.MeasureCode == MeasureCodes.GrossWeightKg)?.Quantity ?? ctx?.GrossWeightKg;
            var v = measurements.FirstOrDefault(m => m.ObjectId == b.Id && m.MeasureCode == MeasureCodes.VolumeCbm)?.Quantity ?? ctx?.VolumeCbm;
            var c = measurements.FirstOrDefault(m => m.ObjectId == b.Id && m.MeasureCode == MeasureCodes.ChargeableWeightKg)?.Quantity ?? ctx?.ChargeableWeightKg;

            decimal basisVal = suggestedBasis switch
            {
                "cbm" => v ?? 1m,
                "gross_weight" => g ?? 1m,
                _ => c ?? g ?? 1m
            };

            var customerName = b.CustomerPartyId.HasValue && customers.TryGetValue(b.CustomerPartyId.Value, out var cName)
                ? cName
                : null;

            eligibleList.Add(new EligibleBillAllocationDto(
                BillId: b.Id,
                BillNo: b.BillNo,
                CustomerPartyId: b.CustomerPartyId,
                CustomerName: customerName,
                GrossWeightKg: g,
                VolumeCbm: v,
                ChargeableWeightKg: c,
                BasisValue: basisVal > 0 ? basisVal : 1m
            ));
        }

        return new SharedCostAllocationContextDto(
            EligibleBills: eligibleList,
            SuggestedBasis: suggestedBasis,
            BasisReason: basisReason,
            TransportMode: mode,
            NoticeMessage: $"Đã tự động tải {eligibleList.Count} vận đơn đủ điều kiện phân bổ."
        );
    }
}

