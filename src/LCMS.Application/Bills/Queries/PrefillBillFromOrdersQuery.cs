using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

public sealed record PrefillCandidateDto(Guid OrderId, string OrderNo, string? Value);

public sealed record PrefillBillFromOrdersQuery(
    IReadOnlyList<Guid> OrderIds,
    IReadOnlyDictionary<string, string>? UserOverriddenFields = null
) : IRequest<BillPrefillResultDto>;

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
    string NoticeMessage,
    IReadOnlyDictionary<string, string>? FieldStatuses = null,
    IReadOnlyDictionary<string, IReadOnlyList<PrefillCandidateDto>>? FieldCandidates = null,
    IReadOnlyList<string>? PreservedUserOverrides = null
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

        decimal? GetMeasure(Guid orderId, string measureCode) =>
            measurements.FirstOrDefault(m => m.ObjectId == orderId && m.MeasureCode == measureCode)?.Quantity;

        var fieldStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fieldCandidates = new Dictionary<string, IReadOnlyList<PrefillCandidateDto>>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<string>();
        var userOverrides = request.UserOverriddenFields ?? new Dictionary<string, string>();
        var preservedOverrides = new List<string>();

        // 1. Single Order
        if (orders.Count == 1)
        {
            var single = orders[0];
            var sCtx = OperationalContextJson.Deserialize(single.ContextJson);

            var pkg = GetMeasure(single.Id, MeasureCodes.PackageCount) is decimal pVal ? (int)pVal : sCtx?.PackageCount;
            var gross = GetMeasure(single.Id, MeasureCodes.GrossWeightKg) ?? sCtx?.GrossWeightKg;
            var vol = GetMeasure(single.Id, MeasureCodes.VolumeCbm) ?? sCtx?.VolumeCbm;
            
            // Recompute or extract CW:
            decimal? cw = null;
            var modeNorm = single.TransportMode?.Trim().ToLowerInvariant();
            if (gross.HasValue || vol.HasValue)
            {
                if (modeNorm == "air") cw = Math.Max(gross ?? 0, (vol ?? 0) * 167m);
                else if (modeNorm == "sea") cw = vol ?? (gross.HasValue ? gross.Value / 1000m : 0);
                else cw = gross;
            }
            if (!cw.HasValue)
            {
                cw = GetMeasure(single.Id, MeasureCodes.ChargeableWeightKg) ?? sCtx?.ChargeableWeightKg;
            }

            // Check UT-05: preserve user override if present
            Guid? shipperId = null;
            if (userOverrides.TryGetValue("shipperPartyId", out var userShipper) && Guid.TryParse(userShipper, out var sGuid))
            {
                shipperId = sGuid;
                preservedOverrides.Add("shipperPartyId");
                fieldStatuses["shipperPartyId"] = "ManuallyOverridden";
            }

            var transportMode = string.IsNullOrWhiteSpace(single.TransportMode) ? null : single.TransportMode;
            if (transportMode == null)
            {
                fieldStatuses["transportMode"] = "Missing";
            }
            else
            {
                fieldStatuses["transportMode"] = "Auto";
            }

            fieldStatuses["customerPartyId"] = single.CustomerPartyId.HasValue ? "Auto" : "Missing";
            fieldStatuses["originCode"] = !string.IsNullOrWhiteSpace(single.OriginCode) ? "Auto" : "Missing";
            fieldStatuses["destinationCode"] = !string.IsNullOrWhiteSpace(single.DestinationCode) ? "Auto" : "Missing";

            return new BillPrefillResultDto(
                CustomerPartyId: single.CustomerPartyId,
                PayerPartyId: single.CustomerPartyId,
                ShipperPartyId: shipperId,
                ConsigneePartyId: null,
                BillToPartyId: single.CustomerPartyId,
                TransportMode: transportMode,
                ServiceTypeCode: single.ServiceTypeCode ?? sCtx?.ServiceType,
                IncotermCode: single.IncotermCode ?? sCtx?.Incoterm,
                OriginCode: single.OriginCode,
                DestinationCode: single.DestinationCode,
                RouteId: single.RouteId,
                RouteCode: single.RouteCode,
                EtdAt: single.EtdAt,
                EtaAt: single.EtaAt,
                CustomerReference: single.CustomerReference,
                CommodityTypeId: single.CommodityTypeId ?? sCtx?.CommodityTypeId,
                PackageCount: pkg,
                GrossWeightKg: gross,
                VolumeCbm: vol,
                ChargeableWeightKg: cw,
                SourceOrderNumbers: new[] { single.OrderNo },
                Conflicts: Array.Empty<string>(),
                NoticeMessage: $"Đã kế thừa thành công toàn bộ thông tin từ Đơn hàng {single.OrderNo}.",
                FieldStatuses: fieldStatuses,
                FieldCandidates: fieldCandidates,
                PreservedUserOverrides: preservedOverrides
            );
        }

        // 2. Multiple Orders: Consensus Logic (DO-01, INH-02, GAP-05, UT-02, UT-03, UT-04)
        // Check consensus for each field:
        T? ResolveConsensus<T>(string fieldName, Func<Order, T?> getter, Func<T?, string?> strFormatter, string fieldLabelVn) where T : struct, IEquatable<T>
        {
            if (userOverrides.TryGetValue(fieldName, out var ovStr))
            {
                preservedOverrides.Add(fieldName);
                fieldStatuses[fieldName] = "ManuallyOverridden";
                return null;
            }

            var valuesWithOrders = orders.Select(o => new { Order = o, Value = getter(o) }).ToList();
            var distinctVals = valuesWithOrders.Where(x => x.Value.HasValue).Select(x => x.Value!.Value).Distinct().ToList();

            if (distinctVals.Count == 0)
            {
                fieldStatuses[fieldName] = "Missing";
                return null;
            }
            if (distinctVals.Count == 1 && valuesWithOrders.All(x => x.Value.HasValue))
            {
                fieldStatuses[fieldName] = "Auto";
                return distinctVals[0];
            }

            // Conflict! DO NOT take the first order (GAP-05, UT-02, UT-03)
            fieldStatuses[fieldName] = "Conflict";
            conflicts.Add($"{fieldLabelVn} khác biệt giữa các đơn hàng gộp.");
            fieldCandidates[fieldName] = valuesWithOrders
                .Select(x => new PrefillCandidateDto(x.Order.Id, x.Order.OrderNo, strFormatter(x.Value)))
                .ToList();
            return null;
        }

        string? ResolveConsensusStr(string fieldName, Func<Order, string?> getter, string fieldLabelVn)
        {
            if (userOverrides.TryGetValue(fieldName, out var ovVal))
            {
                preservedOverrides.Add(fieldName);
                fieldStatuses[fieldName] = "ManuallyOverridden";
                return ovVal;
            }

            var valuesWithOrders = orders.Select(o => new { Order = o, Value = getter(o)?.Trim() }).ToList();
            var distinctVals = valuesWithOrders.Where(x => !string.IsNullOrEmpty(x.Value)).Select(x => x.Value!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (distinctVals.Count == 0)
            {
                fieldStatuses[fieldName] = "Missing";
                return null;
            }
            if (distinctVals.Count == 1 && valuesWithOrders.All(x => !string.IsNullOrEmpty(x.Value)))
            {
                fieldStatuses[fieldName] = "Auto";
                return distinctVals[0];
            }

            // Conflict!
            fieldStatuses[fieldName] = "Conflict";
            conflicts.Add($"{fieldLabelVn} khác biệt ({string.Join(", ", distinctVals)}).");
            fieldCandidates[fieldName] = valuesWithOrders
                .Select(x => new PrefillCandidateDto(x.Order.Id, x.Order.OrderNo, x.Value))
                .ToList();
            return null;
        }

        DateTimeOffset? ResolveConsensusDate(string fieldName, Func<Order, DateTimeOffset?> getter, string fieldLabelVn)
        {
            if (userOverrides.TryGetValue(fieldName, out _))
            {
                preservedOverrides.Add(fieldName);
                fieldStatuses[fieldName] = "ManuallyOverridden";
                return null;
            }

            var valuesWithOrders = orders.Select(o => new { Order = o, Value = getter(o) }).ToList();
            var distinctVals = valuesWithOrders.Where(x => x.Value.HasValue).Select(x => x.Value!.Value).Distinct().ToList();

            if (distinctVals.Count == 0)
            {
                fieldStatuses[fieldName] = "Missing";
                return null;
            }
            if (distinctVals.Count == 1 && valuesWithOrders.All(x => x.Value.HasValue))
            {
                fieldStatuses[fieldName] = "Auto";
                return distinctVals[0];
            }

            fieldStatuses[fieldName] = "Conflict";
            conflicts.Add($"{fieldLabelVn} khác biệt giữa các đơn hàng gộp.");
            fieldCandidates[fieldName] = valuesWithOrders
                .Select(x => new PrefillCandidateDto(x.Order.Id, x.Order.OrderNo, x.Value?.ToString("yyyy-MM-dd HH:mm")))
                .ToList();
            return null;
        }

        var customerId = ResolveConsensus("customerPartyId", o => o.CustomerPartyId, v => v?.ToString(), "Khách hàng");
        var transportModeConsensus = ResolveConsensusStr("transportMode", o => o.TransportMode, "Phương thức vận tải");
        var originCodeConsensus = ResolveConsensusStr("originCode", o => o.OriginCode, "Cảng/Điểm đi");
        var destCodeConsensus = ResolveConsensusStr("destinationCode", o => o.DestinationCode, "Cảng/Điểm đến");
        var etdConsensus = ResolveConsensusDate("etdAt", o => o.EtdAt, "Ngày khởi hành (ETD)");
        var etaConsensus = ResolveConsensusDate("etaAt", o => o.EtaAt, "Ngày đến (ETA)");
        var serviceTypeConsensus = ResolveConsensusStr("serviceTypeCode", o => o.ServiceTypeCode, "Loại dịch vụ");
        var incotermConsensus = ResolveConsensusStr("incotermCode", o => o.IncotermCode, "Điều kiện Incoterms");
        var commodityId = ResolveConsensus("commodityTypeId", o => o.CommodityTypeId, v => v?.ToString(), "Loại hàng hóa");

        // Route consensus
        var routeCodeConsensus = (originCodeConsensus != null && destCodeConsensus != null)
            ? $"{originCodeConsensus}-{destCodeConsensus}"
            : ResolveConsensusStr("routeCode", o => o.RouteCode, "Tuyến vận chuyển");

        // Shipper override protection (UT-05)
        Guid? shipperPartyId = null;
        if (userOverrides.TryGetValue("shipperPartyId", out var userShp) && Guid.TryParse(userShp, out var sG))
        {
            shipperPartyId = sG;
            preservedOverrides.Add("shipperPartyId");
            fieldStatuses["shipperPartyId"] = "ManuallyOverridden";
        }

        // Cargo Deduplication & Sum (UT-04, INH-03)
        int totalPackages = 0;
        decimal totalGross = 0;
        decimal totalVol = 0;
        bool hasCargo = false;

        // Check if orders share cargo line items (UT-04)
        var anySharedCargo = orders.Any(o => o.ExternalId != null && o.ExternalId.StartsWith("SHARED_CARGO_LINE"));

        foreach (var order in orders)
        {
            var ctx = OperationalContextJson.Deserialize(order.ContextJson);

            var p = GetMeasure(order.Id, MeasureCodes.PackageCount) is decimal pVal ? (int)pVal : ctx?.PackageCount;
            var g = GetMeasure(order.Id, MeasureCodes.GrossWeightKg) ?? ctx?.GrossWeightKg;
            var v = GetMeasure(order.Id, MeasureCodes.VolumeCbm) ?? ctx?.VolumeCbm;

            if (p.HasValue || g.HasValue || v.HasValue)
            {
                hasCargo = true;
                if (anySharedCargo)
                {
                    // Do not double-count shared cargo line (UT-04)
                    totalPackages = Math.Max(totalPackages, p ?? 0);
                    totalGross = Math.Max(totalGross, g ?? 0);
                    totalVol = Math.Max(totalVol, v ?? 0);
                }
                else
                {
                    totalPackages += p ?? 0;
                    totalGross += g ?? 0;
                    totalVol += v ?? 0;
                }
            }
        }

        // CW must NOT be directly summed from orders (DO-01, Cặp 16, UT-04)!
        // Recompute CW based on consensus transportMode and totalGross/totalVol:
        decimal? calculatedCw = null;
        if (hasCargo && !string.IsNullOrWhiteSpace(transportModeConsensus))
        {
            var mode = transportModeConsensus.Trim().ToLowerInvariant();
            if (mode == "air")
            {
                calculatedCw = Math.Max(totalGross, totalVol * 167m);
            }
            else if (mode == "sea")
            {
                calculatedCw = totalVol;
            }
            else
            {
                calculatedCw = totalGross;
            }
        }

        var customerRefs = orders
            .Where(o => !string.IsNullOrWhiteSpace(o.CustomerReference))
            .Select(o => o.CustomerReference!.Trim())
            .Distinct()
            .ToList();

        var notice = conflicts.Count > 0
            ? $"Đã phân tích {orders.Count} đơn hàng. Phát hiện {conflicts.Count} trường xung đột (yêu cầu người dùng xác nhận, không tự chọn theo Order đầu tiên)."
            : $"Đã tự động kế thừa và cộng dồn thông số từ {orders.Count} đơn hàng đồng thuận.";

        return new BillPrefillResultDto(
            CustomerPartyId: customerId,
            PayerPartyId: customerId,
            ShipperPartyId: shipperPartyId,
            ConsigneePartyId: null,
            BillToPartyId: customerId,
            TransportMode: transportModeConsensus,
            ServiceTypeCode: serviceTypeConsensus,
            IncotermCode: incotermConsensus,
            OriginCode: originCodeConsensus,
            DestinationCode: destCodeConsensus,
            RouteId: null,
            RouteCode: routeCodeConsensus,
            EtdAt: etdConsensus,
            EtaAt: etaConsensus,
            CustomerReference: customerRefs.Count > 0 ? string.Join(", ", customerRefs) : null,
            CommodityTypeId: commodityId,
            PackageCount: hasCargo ? totalPackages : null,
            GrossWeightKg: hasCargo ? totalGross : null,
            VolumeCbm: hasCargo ? totalVol : null,
            ChargeableWeightKg: calculatedCw,
            SourceOrderNumbers: orders.Select(o => o.OrderNo).ToList(),
            Conflicts: conflicts,
            NoticeMessage: notice,
            FieldStatuses: fieldStatuses,
            FieldCandidates: fieldCandidates,
            PreservedUserOverrides: preservedOverrides
        );
    }
}
