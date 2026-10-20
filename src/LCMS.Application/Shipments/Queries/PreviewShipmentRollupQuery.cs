using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Shipments.Queries;

public sealed record PreviewShipmentRollupQuery(IReadOnlyList<Guid> BillIds) : IRequest<ShipmentRollupPreviewDto>;

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
        if (request.BillIds == null || request.BillIds.Count == 0)
        {
            return new ShipmentRollupPreviewDto(
                null, null, null, null, null, null, null, null, null, null, null,
                0, 0, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), "Không có vận đơn nào được chọn.");
        }

        var bills = await _db.Bills
            .Where(b => b.TenantId == tenantId && request.BillIds.Contains(b.Id))
            .OrderBy(b => b.Id)
            .ToListAsync(cancellationToken);

        if (bills.Count == 0)
        {
            return new ShipmentRollupPreviewDto(
                null, null, null, null, null, null, null, null, null, null, null,
                0, 0, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), "Không tìm thấy thông tin vận đơn.");
        }

        var billGuids = bills.Select(b => b.Id).ToList();
        var measurements = await _db.OperationalMeasurements.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ObjectType == OperationalObjectTypes.Bill && billGuids.Contains(m.ObjectId))
            .ToListAsync(cancellationToken);

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

            totalPackages += p ?? 0;
            totalGross += g ?? 0;
            totalVol += v ?? 0;
            totalCw += c ?? 0;
        }

        var firstBill = bills[0];
        var firstCtx = OperationalContextJson.Deserialize(firstBill.ContextJson);

        var conflicts = new List<string>();

        var distinctModes = bills.Where(b => !string.IsNullOrWhiteSpace(b.TransportMode)).Select(b => b.TransportMode!.Trim().ToLowerInvariant()).Distinct().ToList();
        if (distinctModes.Count > 1)
        {
            conflicts.Add($"Khác biệt Phương thức vận chuyển ({string.Join(", ", distinctModes)}).");
        }

        var distinctOrigins = bills.Where(b => !string.IsNullOrWhiteSpace(b.OriginCode)).Select(b => b.OriginCode!.Trim().ToUpperInvariant()).Distinct().ToList();
        if (distinctOrigins.Count > 1)
        {
            conflicts.Add($"Khác biệt Cảng đi ({string.Join(", ", distinctOrigins)}).");
        }

        var distinctDests = bills.Where(b => !string.IsNullOrWhiteSpace(b.DestinationCode)).Select(b => b.DestinationCode!.Trim().ToUpperInvariant()).Distinct().ToList();
        if (distinctDests.Count > 1)
        {
            conflicts.Add($"Khác biệt Cảng đến ({string.Join(", ", distinctDests)}).");
        }

        var carrierId = bills.FirstOrDefault(b => b.CarrierPartyId.HasValue)?.CarrierPartyId;
        var carrierName = firstCtx?.CarrierName;

        var summary = conflicts.Count > 0
            ? $"Đã cộng dồn {bills.Count} vận đơn: {totalPackages} kiện, {totalGross:N2} kg, {totalVol:N2} CBM (lưu ý: có {conflicts.Count} điểm khác biệt về tuyến/mode)."
            : $"Đã tự động cộng dồn thông số từ {bills.Count} vận đơn (Tổng kiện: {totalPackages}, Trọng lượng: {totalGross:N2} kg, Thể tích: {totalVol:N2} CBM, Tính cước: {totalCw:N2} kg).";

        return new ShipmentRollupPreviewDto(
            TransportMode: bills.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.TransportMode))?.TransportMode,
            OriginCode: bills.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.OriginCode))?.OriginCode,
            DestinationCode: bills.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.DestinationCode))?.DestinationCode,
            RouteCode: bills.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.RouteCode))?.RouteCode,
            RouteId: bills.FirstOrDefault(b => b.RouteId.HasValue)?.RouteId,
            CarrierPartyId: carrierId,
            CarrierName: carrierName,
            ServiceTypeCode: bills.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.ServiceTypeCode))?.ServiceTypeCode ?? firstCtx?.ServiceType,
            EtdAt: bills.Where(b => b.EtdAt.HasValue).Min(b => b.EtdAt),
            EtaAt: bills.Where(b => b.EtaAt.HasValue).Max(b => b.EtaAt),
            CommodityTypeId: bills.FirstOrDefault(b => b.CommodityTypeId.HasValue)?.CommodityTypeId ?? firstCtx?.CommodityTypeId,
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
