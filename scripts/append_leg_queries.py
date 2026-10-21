# -*- coding: utf-8 -*-
with open('src/LCMS.Application/TransportLegs/Queries/TransportLegQueries.cs', 'r', encoding='utf-8') as f:
    text = f.read()

append_code = '''

public sealed record NextLegContextDto(
    Guid ShipmentId,
    string? ShipmentNo,
    int NextSequenceNo,
    string? SuggestedOriginCode,
    string? SuggestedDestinationCode,
    string? SuggestedRouteCode,
    string? TransportMode,
    string? ServiceTypeCode,
    Guid? CarrierPartyId,
    string? CarrierName,
    Guid? CommodityTypeId,
    DateTimeOffset? EtdAt,
    DateTimeOffset? EtaAt,
    IReadOnlyList<OperationalBillRef> RelatedBills,
    string NoticeMessage
);

public sealed record GetNextLegContextQuery(Guid ShipmentId) : IRequest<NextLegContextDto>;

public sealed class GetNextLegContextQueryHandler : IRequestHandler<GetNextLegContextQuery, NextLegContextDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetNextLegContextQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<NextLegContextDto> Handle(GetNextLegContextQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId;
        var shipment = await _db.Shipments.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == request.ShipmentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lô hàng.");

        var existingLegs = await _db.TransportLegs.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.ShipmentId == request.ShipmentId)
            .OrderBy(l => l.SequenceNo)
            .ToListAsync(cancellationToken);

        int nextSeq = existingLegs.Count + 1;
        string? origCode;
        string? destCode;

        if (existingLegs.Count == 0)
        {
            origCode = shipment.OriginCode;
            destCode = shipment.DestinationCode;
        }
        else
        {
            var lastLeg = existingLegs.Last();
            origCode = lastLeg.DestinationCode;
            destCode = shipment.DestinationCode;
        }

        var relatedBills = await (
            from l in _db.BillShipmentLinks.AsNoTracking()
            join b in _db.Bills.AsNoTracking() on l.BillId equals b.Id
            where l.ShipmentId == shipment.Id && l.TenantId == tenantId
            orderby b.BillNo
            select new OperationalBillRef(b.Id, b.BillNo, b.OperationalStatus))
            .ToListAsync(cancellationToken);

        var notice = existingLegs.Count == 0
            ? $"Tự động điền chặng đầu tiên từ Lô hàng {shipment.ShipmentNo} (Cảng đi: {origCode}, Cảng đến: {destCode})."
            : $"Tự động nối chặng {nextSeq} (Daisy-chaining): Cảng đi lấy từ điểm đến của chặng trước ({origCode}).";

        return new NextLegContextDto(
            ShipmentId: shipment.Id,
            ShipmentNo: shipment.ShipmentNo,
            NextSequenceNo: nextSeq,
            SuggestedOriginCode: origCode,
            SuggestedDestinationCode: destCode,
            SuggestedRouteCode: shipment.RouteCode,
            TransportMode: shipment.TransportMode,
            ServiceTypeCode: shipment.ServiceTypeCode,
            CarrierPartyId: shipment.CarrierPartyId,
            CarrierName: shipment.CarrierName,
            CommodityTypeId: shipment.CommodityTypeId,
            EtdAt: shipment.EtdAt,
            EtaAt: shipment.EtaAt,
            RelatedBills: relatedBills,
            NoticeMessage: notice
        );
    }
}

public sealed record MovementPrefillFromLegsDto(
    string? TransportMode,
    Guid? CarrierPartyId,
    Guid? OriginLocationId,
    Guid? DestinationLocationId,
    DateTimeOffset? MovementOn,
    IReadOnlyList<OperationalBillRef> RelatedBills,
    string NoticeMessage
);

public sealed record PrefillMovementFromLegsQuery(IReadOnlyList<Guid> LegIds) : IRequest<MovementPrefillFromLegsDto>;

public sealed class PrefillMovementFromLegsQueryHandler : IRequestHandler<PrefillMovementFromLegsQuery, MovementPrefillFromLegsDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PrefillMovementFromLegsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<MovementPrefillFromLegsDto> Handle(PrefillMovementFromLegsQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        if (request.LegIds == null || request.LegIds.Count == 0)
        {
            return new MovementPrefillFromLegsDto(null, null, null, null, null, Array.Empty<OperationalBillRef>(), "Không có chặng nào được chọn.");
        }

        var tenantId = _tenantContext.TenantId;
        var legs = await _db.TransportLegs.AsNoTracking()
            .Where(l => l.TenantId == tenantId && request.LegIds.Contains(l.Id))
            .OrderBy(l => l.SequenceNo)
            .ToListAsync(cancellationToken);

        if (legs.Count == 0)
        {
            return new MovementPrefillFromLegsDto(null, null, null, null, null, Array.Empty<OperationalBillRef>(), "Không tìm thấy thông tin chặng.");
        }

        var firstLeg = legs[0];
        var shipment = await _db.Shipments.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == firstLeg.ShipmentId, cancellationToken);

        var carrierId = shipment?.CarrierPartyId;
        var legGuids = legs.Select(l => l.Id).ToList();

        var relatedBills = await (
            from l in _db.BillLegLinks.AsNoTracking()
            join b in _db.Bills.AsNoTracking() on l.BillId equals b.Id
            where legGuids.Contains(l.TransportLegId) && l.TenantId == tenantId
            orderby b.BillNo
            select new OperationalBillRef(b.Id, b.BillNo, b.OperationalStatus))
            .Distinct()
            .ToListAsync(cancellationToken);

        return new MovementPrefillFromLegsDto(
            TransportMode: firstLeg.TransportMode ?? shipment?.TransportMode,
            CarrierPartyId: carrierId,
            OriginLocationId: firstLeg.OriginLocationId ?? shipment?.OriginLocationId,
            DestinationLocationId: firstLeg.DestinationLocationId ?? shipment?.DestinationLocationId,
            MovementOn: shipment?.EtdAt,
            RelatedBills: relatedBills,
            NoticeMessage: $"Đã kế thừa thành công phương thức và lộ trình từ {legs.Count} chặng vận chuyển."
        );
    }
}
'''

if 'GetNextLegContextQuery' not in text:
    with open('src/LCMS.Application/TransportLegs/Queries/TransportLegQueries.cs', 'w', encoding='utf-8', newline='\r\n') as f:
        f.write(text + append_code)
    print('Successfully appended NextLegContext and MovementPrefill handlers')
else:
    print('Already present in TransportLegQueries.cs')

