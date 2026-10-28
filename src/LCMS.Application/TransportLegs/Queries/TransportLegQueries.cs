using LCMS.Application.OperationalReferences;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.TransportLegs.Queries;

public sealed record TransportLegDto(
    Guid Id,
    Guid TenantId,
    Guid ShipmentId,
    string LegNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record TransportLegMovementRef(Guid Id, string MovementNo, string OperationalStatus);

public sealed record TransportLegDetailDto(
    Guid Id,
    Guid TenantId,
    Guid ShipmentId,
    string? ShipmentNo,
    string LegNo,
    string SourceSystem,
    string ExternalId,
    string? ExternalVersion,
    string OperationalStatus,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OperationalBillRef> RelatedBills,
    IReadOnlyList<TransportLegMovementRef> Movements);

public sealed record ListTransportLegsQuery(string? Q = null, Guid? ShipmentId = null)
    : IRequest<IReadOnlyList<TransportLegDto>>;

/// <summary>Lists chặng vận chuyển (reference only — not dispatch).</summary>
public sealed class ListTransportLegsQueryHandler
    : IRequestHandler<ListTransportLegsQuery, IReadOnlyList<TransportLegDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListTransportLegsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<TransportLegDto>> Handle(
        ListTransportLegsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.TransportLegs.AsNoTracking().AsQueryable();
        if (request.ShipmentId.HasValue)
        {
            query = query.Where(l => l.ShipmentId == request.ShipmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var pattern = request.Q.Trim().ToLowerInvariant();
            query = query.Where(l =>
                l.LegNo.ToLower().Contains(pattern)
                || l.ExternalId.ToLower().Contains(pattern));
        }

        return await query
            .OrderBy(l => l.LegNo)
            .Select(l => new TransportLegDto(
                l.Id,
                l.TenantId,
                l.ShipmentId,
                l.LegNo,
                l.SourceSystem,
                l.ExternalId,
                l.ExternalVersion,
                l.OperationalStatus,
                l.IsActive,
                l.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetTransportLegByIdQuery(Guid Id) : IRequest<TransportLegDetailDto>;

/// <summary>Chặng DETAIL + shipment, Bills, chuyến.</summary>
public sealed class GetTransportLegByIdQueryHandler
    : IRequestHandler<GetTransportLegByIdQuery, TransportLegDetailDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetTransportLegByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<TransportLegDetailDto> Handle(
        GetTransportLegByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var leg = await _db.TransportLegs
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chặng vận chuyển.");

        var shipmentNo = await _db.Shipments.AsNoTracking()
            .Where(s => s.Id == leg.ShipmentId)
            .Select(s => s.ShipmentNo)
            .FirstOrDefaultAsync(cancellationToken);

        var related = await (
            from l in _db.BillLegLinks.AsNoTracking()
            join b in _db.Bills.AsNoTracking() on l.BillId equals b.Id
            where l.TransportLegId == leg.Id
            orderby b.BillNo
            select new OperationalBillRef(b.Id, b.BillNo, b.OperationalStatus))
            .ToListAsync(cancellationToken);

        var movements = await (
            from lm in _db.LegMovementLinks.AsNoTracking()
            join m in _db.TransportMovements.AsNoTracking() on lm.TransportMovementId equals m.Id
            where lm.TransportLegId == leg.Id
            orderby m.MovementNo
            select new TransportLegMovementRef(m.Id, m.MovementNo, m.OperationalStatus))
            .ToListAsync(cancellationToken);

        return new TransportLegDetailDto(
            leg.Id,
            leg.TenantId,
            leg.ShipmentId,
            shipmentNo,
            leg.LegNo,
            leg.SourceSystem,
            leg.ExternalId,
            leg.ExternalVersion,
            leg.OperationalStatus,
            leg.IsActive,
            leg.CreatedAt,
            related,
            movements);
    }
}

public sealed record TransportLegOption(
    Guid Id,
    string LegNo,
    int SequenceNo,
    string? OriginCode,
    string? DestinationCode,
    string? TransportMode,
    string OperationalStatus,
    string SourceSystem,
    string ExternalId,
    Guid ShipmentId,
    string? ShipmentNo);

public sealed record GetTransportLegOptionsQuery(Guid? ShipmentId = null, string? TransportMode = null)
    : IRequest<IReadOnlyList<TransportLegOption>>;

public sealed class GetTransportLegOptionsQueryHandler
    : IRequestHandler<GetTransportLegOptionsQuery, IReadOnlyList<TransportLegOption>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetTransportLegOptionsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<TransportLegOption>> Handle(
        GetTransportLegOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId;
        var query = from l in _db.TransportLegs.AsNoTracking()
            join s in _db.Shipments.AsNoTracking() on l.ShipmentId equals s.Id
            where l.TenantId == tenantId && l.IsActive
            select new { Leg = l, Shipment = s };

        if (request.ShipmentId is Guid sid && sid != Guid.Empty)
        {
            query = query.Where(x => x.Leg.ShipmentId == sid);
        }

        if (!string.IsNullOrWhiteSpace(request.TransportMode))
        {
            var mode = LCMS.Domain.Pricing.RateFieldPolicy.ToStoredMode(request.TransportMode);
            query = query.Where(x =>
                (x.Leg.TransportMode != null && x.Leg.TransportMode.ToUpper() == mode) ||
                (x.Leg.TransportMode == null && x.Shipment.TransportMode != null && x.Shipment.TransportMode.ToUpper() == mode));
        }

        var rows = await query
            .OrderBy(x => x.Shipment.ShipmentNo)
            .ThenBy(x => x.Leg.SequenceNo)
            .Take(200)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new TransportLegOption(
            x.Leg.Id,
            x.Leg.LegNo,
            x.Leg.SequenceNo,
            x.Leg.OriginCode ?? x.Shipment.OriginCode,
            x.Leg.DestinationCode ?? x.Shipment.DestinationCode,
            x.Leg.TransportMode ?? x.Shipment.TransportMode,
            x.Leg.OperationalStatus,
            x.Leg.SourceSystem,
            x.Leg.ExternalId,
            x.Leg.ShipmentId,
            x.Shipment.ShipmentNo)).ToList();
    }
}

public sealed record GetShipmentLegSummaryQuery(Guid ShipmentId) : IRequest<ShipmentLegSummaryDto>;

public sealed record ShipmentLegSummaryDto(
    Guid ShipmentId,
    string ShipmentNo,
    int TotalLegs,
    IReadOnlyList<TransportLegSummaryItem> Legs);

public sealed record TransportLegSummaryItem(
    Guid LegId,
    string LegNo,
    int SequenceNo,
    string? OriginCode,
    string? DestinationCode,
    string? TransportMode,
    string OperationalStatus,
    int LinkedBillCount);

public sealed class GetShipmentLegSummaryQueryHandler
    : IRequestHandler<GetShipmentLegSummaryQuery, ShipmentLegSummaryDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetShipmentLegSummaryQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<ShipmentLegSummaryDto> Handle(
        GetShipmentLegSummaryQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId;
        var shipment = await _db.Shipments.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == request.ShipmentId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lô hàng.");

        var legs = await _db.TransportLegs.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.ShipmentId == request.ShipmentId)
            .OrderBy(l => l.SequenceNo)
            .ToListAsync(cancellationToken);

        var legIds = legs.Select(l => l.Id).ToList();
        var counts = await _db.BillLegLinks.AsNoTracking()
            .Where(x => x.TenantId == tenantId && legIds.Contains(x.TransportLegId))
            .GroupBy(x => x.TransportLegId)
            .Select(g => new { LegId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.LegId, x => x.Count, cancellationToken);

        var items = legs.Select(l => new TransportLegSummaryItem(
            l.Id,
            l.LegNo,
            l.SequenceNo,
            l.OriginCode ?? shipment.OriginCode,
            l.DestinationCode ?? shipment.DestinationCode,
            l.TransportMode ?? shipment.TransportMode,
            l.OperationalStatus,
            counts.TryGetValue(l.Id, out var c) ? c : 0)).ToList();

        return new ShipmentLegSummaryDto(shipment.Id, shipment.ShipmentNo, items.Count, items);
    }
}

public sealed record OperationalBillRef(Guid Id, string BillNo, string Status);

public sealed record NextLegContextDto(
    Guid ShipmentId,
    string ShipmentNo,
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

public sealed record GetNextLegContextQuery(
    Guid ShipmentId,
    int? ExpectedTotalLegs = null,
    bool? IsFinalLeg = null
) : IRequest<NextLegContextDto>;

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
            // If total legs > 1, first leg does not end at shipment final destination!
            destCode = (request.ExpectedTotalLegs.HasValue && request.ExpectedTotalLegs.Value > 1 && request.IsFinalLeg != true)
                ? null
                : shipment.DestinationCode;
        }
        else
        {
            var lastLeg = existingLegs.Last();
            origCode = lastLeg.DestinationCode;

            // UT-11: Intermediate legs must NOT copy shipment destination endpoints!
            bool isIntermediate = request.IsFinalLeg != true && (
                (request.ExpectedTotalLegs.HasValue && nextSeq < request.ExpectedTotalLegs.Value) ||
                (request.ExpectedTotalLegs == null && !string.Equals(origCode, shipment.DestinationCode, StringComparison.OrdinalIgnoreCase))
            );

            destCode = (request.IsFinalLeg == true || !isIntermediate) ? shipment.DestinationCode : null;
        }

        var relatedBills = await (
            from l in _db.BillShipmentLinks.AsNoTracking()
            join b in _db.Bills.AsNoTracking() on l.BillId equals b.Id
            where l.ShipmentId == shipment.Id && l.TenantId == tenantId
            orderby b.BillNo
            select new OperationalBillRef(b.Id, b.BillNo, b.OperationalStatus))
            .ToListAsync(cancellationToken);

        var notice = existingLegs.Count == 0
            ? $"Tự động điền chặng đầu tiên từ Lô hàng {shipment.ShipmentNo} (Cảng đi: {origCode}, Cảng đến: {destCode ?? "Chờ chọn điểm chuyển tải"})."
            : (destCode == null
                ? $"Tự động nối chặng {nextSeq} (Daisy-chaining): Cảng đi lấy từ điểm đến của chặng trước ({origCode}). Chặng trung gian yêu cầu chọn điểm trung chuyển kế tiếp (không gán ép đích cuối)."
                : $"Tự động nối chặng {nextSeq} (Daisy-chaining): Cảng đi lấy từ điểm đến chặng trước ({origCode}), đích đến hoàn tất hành trình ({destCode}).");

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

        // UT-13: Validate physical transport modes across combined legs!
        var distinctModes = legs.Where(l => !string.IsNullOrWhiteSpace(l.TransportMode))
            .Select(l => ShipmentModePolicy.Normalize(l.TransportMode))
            .Distinct()
            .ToList();

        if (distinctModes.Count > 1)
        {
            throw new ValidationAppException($"Không thể gom các chặng có phương thức vận chuyển vật lý khác nhau ({string.Join(", ", distinctModes)}) vào cùng một chuyến vận tải.");
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
            TransportMode: ShipmentModePolicy.Normalize(firstLeg.TransportMode ?? shipment?.TransportMode),
            CarrierPartyId: carrierId,
            OriginLocationId: firstLeg.OriginLocationId ?? shipment?.OriginLocationId,
            DestinationLocationId: firstLeg.DestinationLocationId ?? shipment?.DestinationLocationId,
            MovementOn: shipment?.EtdAt,
            RelatedBills: relatedBills,
            NoticeMessage: $"Đã kế thừa thành công phương thức và lộ trình đồng nhất từ {legs.Count} chặng vận chuyển."
        );
    }
}
