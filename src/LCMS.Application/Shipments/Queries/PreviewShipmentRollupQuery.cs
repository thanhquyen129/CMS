using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Shipments.Queries;

public sealed record BillRollupAllocationItem(
    Guid BillId,
    decimal? AllocationRatio = null,
    decimal? AllocatedGrossWeightKg = null,
    decimal? AllocatedVolumeCbm = null,
    int? AllocatedPackageCount = null
);

public sealed record PreviewShipmentRollupQuery(
    IReadOnlyList<Guid>? BillIds = null,
    IReadOnlyList<BillRollupAllocationItem>? AllocationItems = null
) : IRequest<ShipmentRollupPreviewDto>;

public sealed record ShipmentRollupPreviewDto(
    string? TransportMode,
    string? OriginCode,
    string? DestinationCode,
    string? RouteCode,
    Guid? RouteId,
    Guid? CarrierPartyId,
    string? CarrierName,
    string? ServiceTypeCode,
    DateTimeOffset? EtdAt,
    DateTimeOffset? EtaAt,
    Guid? CommodityTypeId,
    bool IsMixedCommodity,
    int TotalPackageCount,
    decimal TotalGrossWeightKg,
    decimal TotalVolumeCbm,
    decimal TotalChargeableWeightKg,
    IReadOnlyList<string> SourceBillNumbers,
    IReadOnlyList<string> Conflicts,
    string RollupSummary
);

public sealed class PreviewShipmentRollupQueryHandler : IRequestHandler<PreviewShipmentRollupQuery, ShipmentRollupPreviewDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PreviewShipmentRollupQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ShipmentRollupPreviewDto> Handle(PreviewShipmentRollupQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId;
        var requestedIds = (request.BillIds ?? Array.Empty<Guid>())
            .Concat((request.AllocationItems ?? Array.Empty<BillRollupAllocationItem>()).Select(i => i.BillId))
            .Distinct()
            .ToList();

        if (requestedIds.Count == 0)
        {
            return new ShipmentRollupPreviewDto(
                null, null, null, null, null, null, null, null, null, null, null, false,
                0, 0, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), "Không có vận đơn nào được chọn.");
        }

        var bills = await _db.Bills
            .Where(b => b.TenantId == tenantId && requestedIds.Contains(b.Id))
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);

        if (bills.Count == 0)
        {
            return new ShipmentRollupPreviewDto(
                null, null, null, null, null, null, null, null, null, null, null, false,
                0, 0, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), "Không tìm thấy thông tin vận đơn.");
        }

        var billGuids = bills.Select(b => b.Id).ToList();
        var measurements = await _db.OperationalMeasurements.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ObjectType == OperationalObjectTypes.Bill && billGuids.Contains(m.ObjectId))
            .ToListAsync(cancellationToken);

        var allocationMap = (request.AllocationItems ?? Array.Empty<BillRollupAllocationItem>())
            .GroupBy(i => i.BillId)
            .ToDictionary(g => g.Key, g => g.First());

        int totalPackages = 0;
        decimal totalGross = 0;
        decimal totalVol = 0;
        decimal totalCw = 0;

        foreach (var bill in bills)
        {
            var ctx = OperationalContextJson.Deserialize(bill.ContextJson);
            var p = measurements.FirstOrDefault(m => m.ObjectId == bill.Id && m.MeasureCode == MeasureCodes.PackageCount)?.Quantity is decimal pVal
                ? (int)pVal
                : ctx?.PackageCount;
            var g = measurements.FirstOrDefault(m => m.ObjectId == bill.Id && m.MeasureCode == MeasureCodes.GrossWeightKg)?.Quantity
                ?? ctx?.GrossWeightKg;
            var v = measurements.FirstOrDefault(m => m.ObjectId == bill.Id && m.MeasureCode == MeasureCodes.VolumeCbm)?.Quantity
                ?? ctx?.VolumeCbm;
            var c = measurements.FirstOrDefault(m => m.ObjectId == bill.Id && m.MeasureCode == MeasureCodes.ChargeableWeightKg)?.Quantity
                ?? ctx?.ChargeableWeightKg;

            // UT-07: Handle partial allocation ratio / explicit allocated amounts when bill is split
            decimal ratio = 1.0m;
            int? allocatedPkg = null;
            decimal? allocatedG = null;
            decimal? allocatedV = null;

            if (allocationMap.TryGetValue(bill.Id, out var alloc))
            {
                if (alloc.AllocationRatio.HasValue && alloc.AllocationRatio > 0 && alloc.AllocationRatio <= 1.0m)
                {
                    ratio = alloc.AllocationRatio.Value;
                }
                allocatedPkg = alloc.AllocatedPackageCount;
                allocatedG = alloc.AllocatedGrossWeightKg;
                allocatedV = alloc.AllocatedVolumeCbm;
            }

            var effP = allocatedPkg ?? (p.HasValue ? (int)Math.Round(p.Value * ratio, MidpointRounding.AwayFromZero) : (int?)null);
            var effG = allocatedG ?? (g.HasValue ? g.Value * ratio : (decimal?)null);
            var effV = allocatedV ?? (v.HasValue ? v.Value * ratio : (decimal?)null);

            totalPackages += effP ?? 0;
            totalGross += effG ?? 0;
            totalVol += effV ?? 0;
            totalCw += (c.HasValue ? c.Value * ratio : (effG ?? 0));
        }

        var conflicts = new List<string>();

        // 1. Transport Mode (UT-08): Different modes -> suggest "multimodal"
        string? suggestedMode;
        var distinctModes = bills.Where(b => !string.IsNullOrWhiteSpace(b.TransportMode))
            .Select(b => b.TransportMode!.Trim().ToLowerInvariant())
            .Distinct().ToList();

        if (distinctModes.Count > 1)
        {
            suggestedMode = "multimodal";
            conflicts.Add($"Lô hàng gom các Bill có nhiều phương thức ({string.Join(", ", distinctModes)}). Đã gợi ý chế độ MULTIMODAL.");
        }
        else
        {
            suggestedMode = distinctModes.FirstOrDefault();
        }

        // 2. Origins & Destinations
        var distinctOrigins = bills.Where(b => !string.IsNullOrWhiteSpace(b.OriginCode))
            .Select(b => b.OriginCode!.Trim().ToUpperInvariant())
            .Distinct().ToList();

        var distinctDests = bills.Where(b => !string.IsNullOrWhiteSpace(b.DestinationCode))
            .Select(b => b.DestinationCode!.Trim().ToUpperInvariant())
            .Distinct().ToList();

        // 3. Opposite Routes Check (UT-09)
        // e.g. Bill 1: SGN -> LAX and Bill 2: LAX -> SGN
        bool hasOppositeRoutes = false;
        if (distinctOrigins.Count > 1 && distinctDests.Count > 1)
        {
            // check if origin of one is dest of another
            foreach (var b1 in bills)
            {
                foreach (var b2 in bills)
                {
                    if (!string.IsNullOrWhiteSpace(b1.OriginCode) && !string.IsNullOrWhiteSpace(b1.DestinationCode)
                        && !string.IsNullOrWhiteSpace(b2.OriginCode) && !string.IsNullOrWhiteSpace(b2.DestinationCode))
                    {
                        if (string.Equals(b1.OriginCode, b2.DestinationCode, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(b1.DestinationCode, b2.OriginCode, StringComparison.OrdinalIgnoreCase))
                        {
                            hasOppositeRoutes = true;
                            break;
                        }
                    }
                }
                if (hasOppositeRoutes) break;
            }
        }

        string? suggestedOrigin = null;
        string? suggestedDest = null;
        string? suggestedRoute = null;

        if (hasOppositeRoutes)
        {
            conflicts.Add("Xung đột tuyến ngược hướng giữa các Bill gom (ví dụ SGN-LAX và LAX-SGN). Không tự động gán tuyến cho Shipment.");
        }
        else
        {
            if (distinctOrigins.Count == 1) suggestedOrigin = distinctOrigins[0];
            else if (distinctOrigins.Count > 1) conflicts.Add($"Khác biệt Cảng đi ({string.Join(", ", distinctOrigins)}).");

            if (distinctDests.Count == 1) suggestedDest = distinctDests[0];
            else if (distinctDests.Count > 1) conflicts.Add($"Khác biệt Cảng đến ({string.Join(", ", distinctDests)}).");

            if (suggestedOrigin != null && suggestedDest != null)
            {
                suggestedRoute = $"{suggestedOrigin}-{suggestedDest}";
            }
            else
            {
                var distinctRoutes = bills.Where(b => !string.IsNullOrWhiteSpace(b.RouteCode))
                    .Select(b => b.RouteCode!.Trim().ToUpperInvariant())
                    .Distinct().ToList();
                if (distinctRoutes.Count == 1) suggestedRoute = distinctRoutes[0];
            }
        }

        // 4. Commodities (UT-10, GAP-07)
        var distinctCommodities = bills
            .Select(b => b.CommodityTypeId ?? OperationalContextJson.Deserialize(b.ContextJson)?.CommodityTypeId)
            .Where(c => c.HasValue)
            .Select(c => c!.Value)
            .Distinct().ToList();

        Guid? suggestedCommodity = null;
        bool isMixed = false;
        if (distinctCommodities.Count == 1)
        {
            suggestedCommodity = distinctCommodities[0];
        }
        else if (distinctCommodities.Count > 1)
        {
            isMixed = true;
            suggestedCommodity = null; // Do NOT pick first commodity!
            conflicts.Add($"Lô hàng chứa nhiều loại hàng hóa khác nhau ({distinctCommodities.Count} loại hàng). Đặt cờ hàng hỗn hợp (Mixed Commodity).");
        }

        // 5. Carrier (GAP-09, INH-11): Do NOT take VendorPartyId!
        // Carrier is distinct from Vendor. Only extract if explicit operational carrier exists in ContextJson.
        Guid? carrierId = null;
        string? carrierName = null;
        foreach (var b in bills)
        {
            var ctx = OperationalContextJson.Deserialize(b.ContextJson);
            if (!string.IsNullOrWhiteSpace(ctx?.CarrierName))
            {
                carrierName = ctx.CarrierName;
                break;
            }
        }

        var summary = conflicts.Count > 0
            ? $"Đã cộng dồn {bills.Count} vận đơn: {totalPackages} kiện, {totalGross:N2} kg, {totalVol:N2} CBM (lưu ý: có {conflicts.Count} điểm khác biệt cần kiểm tra)."
            : $"Đã tự động cộng dồn thông số từ {bills.Count} vận đơn (Tổng kiện: {totalPackages}, Trọng lượng: {totalGross:N2} kg, Thể tích: {totalVol:N2} CBM).";

        return new ShipmentRollupPreviewDto(
            TransportMode: suggestedMode,
            OriginCode: suggestedOrigin,
            DestinationCode: suggestedDest,
            RouteCode: suggestedRoute,
            RouteId: null,
            CarrierPartyId: carrierId,
            CarrierName: carrierName,
            ServiceTypeCode: bills.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.ServiceTypeCode))?.ServiceTypeCode,
            EtdAt: bills.Where(b => b.EtdAt.HasValue).Min(b => b.EtdAt),
            EtaAt: bills.Where(b => b.EtaAt.HasValue).Max(b => b.EtaAt),
            CommodityTypeId: suggestedCommodity,
            IsMixedCommodity: isMixed,
            TotalPackageCount: totalPackages,
            TotalGrossWeightKg: totalGross,
            TotalVolumeCbm: totalVol,
            TotalChargeableWeightKg: totalCw,
            SourceBillNumbers: bills.Select(b => b.BillNo).ToList(),
            Conflicts: conflicts,
            RollupSummary: summary
        );
    }
}
