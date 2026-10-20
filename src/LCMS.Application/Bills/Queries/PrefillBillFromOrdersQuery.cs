using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

public sealed record PrefillBillFromOrdersQuery(IReadOnlyList<Guid> OrderIds) : IRequest<BillPrefillResultDto>;

public sealed record BillPrefillResultDto(
    Guid? CustomerPartyId,
    Guid? PayerPartyId,
    Guid? ShipperPartyId,
    Guid? ConsigneePartyId,
    Guid? BillToPartyId,
    string? TransportMode,
    string? ServiceTypeCode,
    string? IncotermCode,
    string? OriginCode,
    string? DestinationCode,
    Guid? RouteId,
    string? RouteCode,
    DateTimeOffset? EtdAt,
    DateTimeOffset? EtaAt,
    string? CustomerReference,
    Guid? CommodityTypeId,
    int? PackageCount,
    decimal? GrossWeightKg,
    decimal? VolumeCbm,
    decimal? ChargeableWeightKg,
    IReadOnlyList<string> SourceOrderNumbers,
    IReadOnlyList<string> Conflicts,
    string NoticeMessage
);

public sealed class PrefillBillFromOrdersQueryHandler : IRequestHandler<PrefillBillFromOrdersQuery, BillPrefillResultDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PrefillBillFromOrdersQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BillPrefillResultDto> Handle(PrefillBillFromOrdersQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId;
        if (request.OrderIds == null || request.OrderIds.Count == 0)
        {
            return new BillPrefillResultDto(
                null, null, null, null, null,
                null, null, null, null, null,
                null, null, null, null, null, null,
                null, null, null, null,
                Array.Empty<string>(), Array.Empty<string>(), "Không có đơn hàng nào được chọn.");
        }

        var orders = await _db.Orders
            .Where(o => o.TenantId == tenantId && request.OrderIds.Contains(o.Id))
            .OrderBy(o => o.Id)
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return new BillPrefillResultDto(
                null, null, null, null, null,
                null, null, null, null, null,
                null, null, null, null, null, null,
                null, null, null, null,
                Array.Empty<string>(), Array.Empty<string>(), "Không tìm thấy thông tin đơn hàng tương ứng.");
        }

        var orderGuids = orders.Select(o => o.Id).ToList();
        var measurements = await _db.OperationalMeasurements.AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.ObjectType == OperationalObjectTypes.Order && orderGuids.Contains(m.ObjectId))
            .ToListAsync(cancellationToken);

        var firstOrder = orders[0];
        var firstCtx = OperationalContextJson.Deserialize(firstOrder.ContextJson);

        decimal? GetMeasure(Guid orderId, string measureCode) =>
            measurements.FirstOrDefault(m => m.ObjectId == orderId && m.MeasureCode == measureCode)?.Quantity;

        if (orders.Count == 1)
        {
            var pkg = GetMeasure(firstOrder.Id, MeasureCodes.PackageCount) is decimal pVal ? (int)pVal : firstCtx?.PackageCount;
            var gross = GetMeasure(firstOrder.Id, MeasureCodes.GrossWeightKg) ?? firstCtx?.GrossWeightKg;
            var vol = GetMeasure(firstOrder.Id, MeasureCodes.VolumeCbm) ?? firstCtx?.VolumeCbm;
            var cw = GetMeasure(firstOrder.Id, MeasureCodes.ChargeableWeightKg) ?? firstCtx?.ChargeableWeightKg;

            return new BillPrefillResultDto(
                CustomerPartyId: firstOrder.CustomerPartyId,
                PayerPartyId: firstOrder.CustomerPartyId,
                ShipperPartyId: null,
                ConsigneePartyId: null,
                BillToPartyId: firstOrder.CustomerPartyId,
                TransportMode: firstOrder.TransportMode,
                ServiceTypeCode: firstOrder.ServiceTypeCode ?? firstCtx?.ServiceType,
                IncotermCode: firstOrder.IncotermCode ?? firstCtx?.Incoterm,
                OriginCode: firstOrder.OriginCode,
                DestinationCode: firstOrder.DestinationCode,
                RouteId: firstOrder.RouteId,
                RouteCode: firstOrder.RouteCode,
                EtdAt: firstOrder.EtdAt,
                EtaAt: firstOrder.EtaAt,
                CustomerReference: firstOrder.CustomerReference,
                CommodityTypeId: firstOrder.CommodityTypeId ?? firstCtx?.CommodityTypeId,
                PackageCount: pkg,
                GrossWeightKg: gross,
                VolumeCbm: vol,
                ChargeableWeightKg: cw,
                SourceOrderNumbers: new[] { firstOrder.OrderNo },
                Conflicts: Array.Empty<string>(),
                NoticeMessage: $"Đã kế thừa thành công toàn bộ thông tin từ Đơn hàng {firstOrder.OrderNo}."
            );
        }

        // Multiple orders (Gộp N đơn hàng vào 1 Bill)
        int totalPackages = 0;
        decimal totalGross = 0;
        decimal totalVol = 0;
        decimal totalCw = 0;
        bool hasCargo = false;

        foreach (var order in orders)
        {
            var ctx = OperationalContextJson.Deserialize(order.ContextJson);
            var p = GetMeasure(order.Id, MeasureCodes.PackageCount) is decimal pVal ? (int)pVal : ctx?.PackageCount;
            var g = GetMeasure(order.Id, MeasureCodes.GrossWeightKg) ?? ctx?.GrossWeightKg;
            var v = GetMeasure(order.Id, MeasureCodes.VolumeCbm) ?? ctx?.VolumeCbm;
            var c = GetMeasure(order.Id, MeasureCodes.ChargeableWeightKg) ?? ctx?.ChargeableWeightKg;

            if (p.HasValue || g.HasValue || v.HasValue || c.HasValue)
            {
                hasCargo = true;
                totalPackages += p ?? 0;
                totalGross += g ?? 0;
                totalVol += v ?? 0;
                totalCw += c ?? 0;
            }
        }

        var conflicts = new List<string>();

        var distinctCustomers = orders.Where(o => o.CustomerPartyId.HasValue).Select(o => o.CustomerPartyId!.Value).Distinct().ToList();
        if (distinctCustomers.Count > 1)
        {
            conflicts.Add("Khác biệt Khách hàng giữa các đơn hàng gộp. Đang lấy mặc định theo đơn hàng đầu tiên.");
        }

        var distinctModes = orders.Where(o => !string.IsNullOrWhiteSpace(o.TransportMode)).Select(o => o.TransportMode!.Trim().ToLowerInvariant()).Distinct().ToList();
        if (distinctModes.Count > 1)
        {
            conflicts.Add($"Khác biệt Phương thức vận tải ({string.Join(", ", distinctModes)}). Đang lấy mặc định {firstOrder.TransportMode}.");
        }

        var distinctOrigins = orders.Where(o => !string.IsNullOrWhiteSpace(o.OriginCode)).Select(o => o.OriginCode!.Trim().ToUpperInvariant()).Distinct().ToList();
        if (distinctOrigins.Count > 1)
        {
            conflicts.Add($"Khác biệt Cảng/Điểm đi ({string.Join(", ", distinctOrigins)}). Đang lấy mặc định {firstOrder.OriginCode}.");
        }

        var distinctDests = orders.Where(o => !string.IsNullOrWhiteSpace(o.DestinationCode)).Select(o => o.DestinationCode!.Trim().ToUpperInvariant()).Distinct().ToList();
        if (distinctDests.Count > 1)
        {
            conflicts.Add($"Khác biệt Cảng/Điểm đến ({string.Join(", ", distinctDests)}). Đang lấy mặc định {firstOrder.DestinationCode}.");
        }

        var notice = conflicts.Count > 0
            ? $"Đã gộp thông số từ {orders.Count} đơn hàng (phát hiện {conflicts.Count} điểm khác biệt cần kiểm tra)."
            : $"Đã tự động cộng dồn thông số hàng hóa từ {orders.Count} đơn hàng.";

        return new BillPrefillResultDto(
            CustomerPartyId: firstOrder.CustomerPartyId,
            PayerPartyId: firstOrder.CustomerPartyId,
            ShipperPartyId: null,
            ConsigneePartyId: null,
            BillToPartyId: firstOrder.CustomerPartyId,
            TransportMode: firstOrder.TransportMode,
            ServiceTypeCode: firstOrder.ServiceTypeCode ?? firstCtx?.ServiceType,
            IncotermCode: firstOrder.IncotermCode ?? firstCtx?.Incoterm,
            OriginCode: firstOrder.OriginCode,
            DestinationCode: firstOrder.DestinationCode,
            RouteId: firstOrder.RouteId,
            RouteCode: firstOrder.RouteCode,
            EtdAt: orders.Where(o => o.EtdAt.HasValue).Min(o => o.EtdAt),
            EtaAt: orders.Where(o => o.EtaAt.HasValue).Max(o => o.EtaAt),
            CustomerReference: string.Join(", ", orders.Where(o => !string.IsNullOrWhiteSpace(o.CustomerReference)).Select(o => o.CustomerReference)),
            CommodityTypeId: firstOrder.CommodityTypeId ?? firstCtx?.CommodityTypeId,
            PackageCount: hasCargo ? totalPackages : null,
            GrossWeightKg: hasCargo ? totalGross : null,
            VolumeCbm: hasCargo ? totalVol : null,
            ChargeableWeightKg: hasCargo ? totalCw : null,
            SourceOrderNumbers: orders.Select(o => o.OrderNo).ToList(),
            Conflicts: conflicts,
            NoticeMessage: notice
        );
    }
}
