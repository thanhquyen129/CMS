using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Common.Paging;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Queries;

public sealed record BillProfitRowDto(
    Guid BillId,
    string BillNo,
    string? CustomerName,
    string? RouteCode,
    string? TransportMode,
    string? ServiceTypeCode,
    DateTimeOffset? BillDate,
    string OperationalStatus,
    string CurrencyCode,
    bool HasMixedCurrencies,
    decimal ExpectedRevenue,
    decimal ConfirmedRevenue,
    decimal ActualRevenue,
    decimal ProfitAmount,
    decimal? MarginRate);

public sealed record ListBillProfitQuery(string View, int? Page, int? PageSize) : IRequest<PagedResult<BillProfitRowDto>>;

public sealed class ListBillProfitQueryValidator : AbstractValidator<ListBillProfitQuery>
{
    public ListBillProfitQueryValidator()
    {
        RuleFor(x => x.View).Must(v => v is "expected" or "confirmed" or "actual" or "best");
    }
}

public sealed record ProfitGroupDto(
    string Key,
    string Label,
    decimal RevenueAmount,
    decimal CostAmount,
    decimal ProfitAmount,
    decimal? MarginRate,
    int BillCount,
    string CurrencyCode,
    string? Note);

public sealed record GetProfitabilityGroupsQuery(string GroupBy, string View) : IRequest<IReadOnlyList<ProfitGroupDto>>;

public sealed class GetProfitabilityGroupsQueryValidator : AbstractValidator<GetProfitabilityGroupsQuery>
{
    public GetProfitabilityGroupsQueryValidator()
    {
        RuleFor(x => x.GroupBy).Must(v => v is "customer" or "service" or "mode" or "route" or "movement");
        RuleFor(x => x.View).Must(v => v is "expected" or "confirmed" or "actual" or "best");
    }
}

public sealed class ListBillProfitQueryHandler : IRequestHandler<ListBillProfitQuery, PagedResult<BillProfitRowDto>>
{
    private readonly ProfitabilityBoard _board;
    private readonly IPermissionService _permissions;

    public ListBillProfitQueryHandler(ProfitabilityBoard board, IPermissionService permissions)
    {
        _board = board;
        _permissions = permissions;
    }

    public async Task<PagedResult<BillProfitRowDto>> Handle(ListBillProfitQuery request, CancellationToken cancellationToken)
    {
        // H-009: profit board needs both cost + revenue visibility.
        await _permissions.EnsureAndResolveDataScopeAsync(
            PermissionCodes.RevenueRead,
            "Bạn không có quyền xem doanh thu.",
            cancellationToken);
        await _permissions.EnsureAndResolveDataScopeAsync(
            PermissionCodes.CostRead,
            "Bạn không có quyền xem chi phí (cần để xem lợi nhuận).",
            cancellationToken);

        var rows = await _board.RowsAsync(request.View, cancellationToken);
        var page = request.Page is null or < 1 ? 1 : request.Page.Value;
        var size = request.PageSize is null or < 1 ? 20 : Math.Min(request.PageSize.Value, 100);
        return new PagedResult<BillProfitRowDto>(
            rows.Skip((page - 1) * size).Take(size).ToList(),
            page,
            size,
            rows.Count);
    }
}

public sealed class GetProfitabilityGroupsQueryHandler : IRequestHandler<GetProfitabilityGroupsQuery, IReadOnlyList<ProfitGroupDto>>
{
    private readonly ProfitabilityBoard _board;
    private readonly IPermissionService _permissions;

    public GetProfitabilityGroupsQueryHandler(ProfitabilityBoard board, IPermissionService permissions)
    {
        _board = board;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<ProfitGroupDto>> Handle(GetProfitabilityGroupsQuery request, CancellationToken cancellationToken)
    {
        await _permissions.EnsureAndResolveDataScopeAsync(
            PermissionCodes.RevenueRead,
            "Bạn không có quyền xem doanh thu.",
            cancellationToken);
        await _permissions.EnsureAndResolveDataScopeAsync(
            PermissionCodes.CostRead,
            "Bạn không có quyền xem chi phí (cần để xem lợi nhuận).",
            cancellationToken);

        var rows = await _board.RowsAsync(request.View, cancellationToken);
        var comparable = rows.Where(r => !r.HasMixedCurrencies).ToList();
        if (request.GroupBy != "movement")
        {
            return comparable
                .GroupBy(r =>
                {
                    var name = request.GroupBy switch
                    {
                        "customer" => r.CustomerName ?? "Không có khách",
                        "service" => r.ServiceTypeCode ?? "Không có dịch vụ",
                        "mode" => r.TransportMode ?? "Không có phương thức",
                        _ => r.RouteCode ?? "Không có tuyến"
                    };
                    return name + " · " + r.CurrencyCode;
                })
                .Select(g => ToGroup(g.Key, g.Key, g, request.View, g.First().CurrencyCode, null))
                .OrderByDescending(g => g.RevenueAmount)
                .ToList();
        }

        var links = await _board.MovementLinksAsync(cancellationToken);
        return links
            .GroupBy(l => l.MovementId)
            .SelectMany(g =>
            {
                var bills = comparable.Where(r => g.Any(l => l.BillId == r.BillId)).ToList();
                return bills
                    .GroupBy(r => r.CurrencyCode)
                    .Select(cg => ToGroup(
                        g.Key + ":" + cg.Key,
                        g.First().MovementNo + " · " + cg.Key,
                        cg,
                        request.View,
                        cg.Key,
                        "Tổng Bill trên chuyến, không phải lãi lỗ riêng của chuyến."));
            })
            .Where(g => g.BillCount > 0)
            .OrderBy(g => g.Label)
            .ToList();
    }

    private static ProfitGroupDto ToGroup(string key, string label, IEnumerable<BillProfitRowDto> rows, string view, string currency, string? note)
    {
        var list = rows.ToList();
        var revenue = list.Sum(r => view switch
        {
            "confirmed" => r.ConfirmedRevenue,
            "actual" => r.ActualRevenue,
            "best" => r.ActualRevenue != 0m ? r.ActualRevenue : r.ConfirmedRevenue != 0m ? r.ConfirmedRevenue : r.ExpectedRevenue,
            _ => r.ExpectedRevenue
        });
        var profit = list.Sum(r => r.ProfitAmount);
        var cost = decimal.Round(revenue - profit, 4, MidpointRounding.AwayFromZero);
        return new ProfitGroupDto(key, label, revenue, cost, profit, ProfitabilityShare.MarginPercent(revenue, profit), list.Count, currency, note);
    }
}

public sealed class ProfitabilityBoard
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ProfitabilityBoard(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<(Guid MovementId, Guid BillId, string MovementNo)>> MovementLinksAsync(CancellationToken cancellationToken)
    {
        return await (
            from l in _db.BillMovementLinks.AsNoTracking()
            join m in _db.TransportMovements.AsNoTracking() on l.TransportMovementId equals m.Id
            select new ValueTuple<Guid, Guid, string>(m.Id, l.BillId, m.MovementNo)
        ).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BillProfitRowDto>> RowsAsync(string view, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        view = view.Trim().ToLowerInvariant();
        var bills = await _db.Bills.AsNoTracking()
            .Select(b => new
            {
                b.Id,
                b.BillNo,
                b.CustomerPartyId,
                b.RouteId,
                b.TransportMode,
                b.ServiceTypeCode,
                b.BillDate,
                b.OperationalStatus
            })
            .ToListAsync(cancellationToken);
        var revenues = await _db.Revenues.AsNoTracking()
            .Where(r => r.RecordStatus == "active")
            .ToListAsync(cancellationToken);
        var maps = await _db.RevenueMappings.AsNoTracking()
            .Where(m => m.MappingStatus == CostAllocationStatuses.Finalized)
            .Select(m => new { m.Id, m.RevenueId, m.VersionNo, m.MappedMaturity })
            .ToListAsync(cancellationToken);
        var mapIds = maps.Select(m => m.Id).ToList();
        var mapLines = await _db.RevenueMappingDetails.AsNoTracking()
            .Where(d => mapIds.Contains(d.MappingId))
            .Select(d => new { d.MappingId, d.BillId, d.AllocatedAmount })
            .ToListAsync(cancellationToken);
        var latest = maps
            .GroupBy(m => m.RevenueId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.VersionNo).First());
        var costs = await _db.Costs.AsNoTracking()
            .Where(c => c.BillId != null && c.AttributionType == CostAttributionTypes.Direct && c.RecordStatus == "active")
            .Select(c => new { c.BillId, c.CurrencyCode, c.ExpectedAmount, c.ConfirmedAmount, c.ActualAmount })
            .ToListAsync(cancellationToken);
        var allocated = await (
            from d in _db.CostAllocationDetails.AsNoTracking()
            join a in _db.CostAllocations.AsNoTracking() on d.AllocationId equals a.Id
            join c in _db.Costs.AsNoTracking() on a.CostId equals c.Id
            where a.AllocationStatus == CostAllocationStatuses.Finalized && c.RecordStatus == "active"
            select new { d.BillId, c.CurrencyCode, d.AllocatedAmount }
        ).ToListAsync(cancellationToken);

        var partyIds = bills.Where(b => b.CustomerPartyId != null).Select(b => b.CustomerPartyId!.Value).Distinct().ToList();
        var parties = await _db.BusinessParties.AsNoTracking()
            .Where(p => partyIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToListAsync(cancellationToken);
        var routeIds = bills.Where(b => b.RouteId != null).Select(b => b.RouteId!.Value).Distinct().ToList();
        var routes = await _db.Routes.AsNoTracking()
            .Where(r => routeIds.Contains(r.Id))
            .Select(r => new { r.Id, r.Code })
            .ToListAsync(cancellationToken);

        var rows = new List<BillProfitRowDto>();
        foreach (var bill in bills.OrderBy(b => b.BillNo))
        {
            var related = revenues.Where(r => r.BillId == bill.Id || mapLines.Any(l => l.BillId == bill.Id && latest.TryGetValue(r.Id, out var map) && map.Id == l.MappingId)).ToList();
            var currencies = related.Select(r => r.CurrencyCode)
                .Concat(costs.Where(c => c.BillId == bill.Id).Select(c => c.CurrencyCode))
                .Concat(allocated.Where(a => a.BillId == bill.Id).Select(a => a.CurrencyCode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (currencies.Count == 0)
            {
                continue;
            }

            decimal Revenue(string code, string layer)
            {
                return related
                    .Where(r => string.Equals(r.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                    .Sum(r =>
                    {
                        latest.TryGetValue(r.Id, out var map);
                        var amount = 0m;
                        if (map is not null)
                        {
                            amount = mapLines.Where(l => l.MappingId == map.Id && l.BillId == bill.Id).Select(l => l.AllocatedAmount).FirstOrDefault();
                        }

                        return ProfitabilityShare.Amount(layer, r.ActualAmount, r.ConfirmedAmount, r.ExpectedAmount, map is not null, map?.MappedMaturity, amount);
                    });
            }

            decimal Cost(string code)
            {
                var direct = costs.Where(c => c.BillId == bill.Id && string.Equals(c.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                    .Sum(c => ProfitabilityShare.Layer(view, c.ActualAmount, c.ConfirmedAmount, c.ExpectedAmount));
                var alloc = allocated.Where(a => a.BillId == bill.Id && string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                    .Sum(a => a.AllocatedAmount);
                return direct + alloc;
            }

            var primary = currencies.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).First();
            var revenueView = Revenue(primary, view);
            var costView = Cost(primary);
            var profit = decimal.Round(revenueView - costView, 4, MidpointRounding.AwayFromZero);
            rows.Add(new BillProfitRowDto(
                bill.Id,
                bill.BillNo,
                parties.FirstOrDefault(p => p.Id == bill.CustomerPartyId)?.Name,
                routes.FirstOrDefault(r => r.Id == bill.RouteId)?.Code,
                bill.TransportMode,
                bill.ServiceTypeCode,
                bill.BillDate,
                bill.OperationalStatus,
                primary.ToUpperInvariant(),
                currencies.Count > 1,
                decimal.Round(Revenue(primary, "expected"), 4, MidpointRounding.AwayFromZero),
                decimal.Round(Revenue(primary, "confirmed"), 4, MidpointRounding.AwayFromZero),
                decimal.Round(Revenue(primary, "actual"), 4, MidpointRounding.AwayFromZero),
                profit,
                currencies.Count > 1 ? null : ProfitabilityShare.MarginPercent(revenueView, profit)));
        }

        return rows;
    }
}
