using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.FinancialDocuments.Queries;

public sealed record EligibleSourceLineDto(
    Guid SourceId,
    string SourceType, // "cost" or "revenue"
    string? LineCode,
    string? Description,
    string CurrencyCode,
    decimal OriginalAmount,
    decimal AlreadyDocumentedAmount,
    decimal RemainingEligibleAmount,
    Guid? BillId,
    string? BillNo,
    Guid? CounterpartyId,
    string? CounterpartyName,
    string Maturity,
    DateOnly EffectiveDate);

public sealed record GetEligibleDocumentSourceLinesQuery(
    string Direction, // "payable" (costs) or "receivable" (revenues)
    Guid? CounterpartyId = null,
    Guid? BillId = null,
    string? CurrencyCode = null) : IRequest<IReadOnlyList<EligibleSourceLineDto>>;

public sealed class GetEligibleDocumentSourceLinesQueryHandler
    : IRequestHandler<GetEligibleDocumentSourceLinesQuery, IReadOnlyList<EligibleSourceLineDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public GetEligibleDocumentSourceLinesQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<EligibleSourceLineDto>> Handle(
        GetEligibleDocumentSourceLinesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var dir = request.Direction.Trim().ToLowerInvariant();
        if (dir is not (FinancialDocumentDirections.Payable or FinancialDocumentDirections.Receivable or "ap" or "ar"))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["direction"] = ["Chiều phải là payable (AP) hoặc receivable (AR)."]
            });
        }

        var isPayable = dir is FinancialDocumentDirections.Payable or "ap";
        var currency = string.IsNullOrWhiteSpace(request.CurrencyCode) ? null : request.CurrencyCode.Trim().ToUpperInvariant();

        if (isPayable)
        {
            await _permissions.EnsureAsync(
                PermissionCodes.CostRead,
                "Bạn không có quyền xem chi phí để gắn chứng từ.",
                cancellationToken);

            var costQuery = _db.Costs.AsNoTracking()
                .Where(c => c.RecordStatus == "active");

            if (request.BillId.HasValue)
            {
                costQuery = costQuery.Where(c => c.BillId == request.BillId.Value);
            }

            if (request.CounterpartyId.HasValue)
            {
                costQuery = costQuery.Where(c => c.VendorPartyId == request.CounterpartyId.Value);
            }

            if (!string.IsNullOrEmpty(currency))
            {
                costQuery = costQuery.Where(c => c.CurrencyCode == currency);
            }

            var costs = await costQuery
                .OrderByDescending(c => c.EffectiveDate)
                .ThenBy(c => c.Id)
                .Take(200)
                .ToListAsync(cancellationToken);

            if (costs.Count == 0)
            {
                return Array.Empty<EligibleSourceLineDto>();
            }

            var costIds = costs.Select(c => c.Id).ToList();

            // Calculate active documented amounts (in-memory aggregation for SQLite / Postgres parity)
            var activeCostDetails = await _db.DocumentMatchDetails.AsNoTracking()
                .Where(d => d.TargetCostId.HasValue
                            && costIds.Contains(d.TargetCostId.Value)
                            && d.DetailStatus == DocumentMatchDetailStatuses.Active
                            && d.Match!.MatchStatus != DocumentMatchStatuses.Cancelled)
                .Select(d => new { CostId = d.TargetCostId!.Value, d.MatchedAmount })
                .ToListAsync(cancellationToken);

            var documentedSums = activeCostDetails
                .GroupBy(d => d.CostId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.MatchedAmount));

            var billIds = costs.Where(c => c.BillId.HasValue).Select(c => c.BillId!.Value).Distinct().ToList();
            var bills = await _db.Bills.AsNoTracking()
                .Where(b => billIds.Contains(b.Id))
                .Select(b => new { b.Id, b.BillNo })
                .ToDictionaryAsync(b => b.Id, b => b.BillNo, cancellationToken);

            var partyIds = costs.Where(c => c.VendorPartyId.HasValue).Select(c => c.VendorPartyId!.Value).Distinct().ToList();
            var parties = await _db.BusinessParties.AsNoTracking()
                .Where(p => partyIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

            var result = new List<EligibleSourceLineDto>();
            foreach (var c in costs)
            {
                var documented = documentedSums.TryGetValue(c.Id, out var sum) ? sum : 0m;
                var remaining = decimal.Round(c.Amount - documented, 4, MidpointRounding.AwayFromZero);
                if (remaining <= 0m)
                {
                    continue; // fully documented
                }

                bills.TryGetValue(c.BillId ?? Guid.Empty, out var billNo);
                parties.TryGetValue(c.VendorPartyId ?? Guid.Empty, out var vendorName);

                result.Add(new EligibleSourceLineDto(
                    SourceId: c.Id,
                    SourceType: "cost",
                    LineCode: c.CostTypeCode ?? "COST",
                    Description: c.CostTypeCode,
                    CurrencyCode: c.CurrencyCode,
                    OriginalAmount: c.Amount,
                    AlreadyDocumentedAmount: documented,
                    RemainingEligibleAmount: remaining,
                    BillId: c.BillId,
                    BillNo: billNo,
                    CounterpartyId: c.VendorPartyId,
                    CounterpartyName: vendorName,
                    Maturity: c.FinancialMaturity,
                    EffectiveDate: c.EffectiveDate));
            }

            return result;
        }
        else
        {
            await _permissions.EnsureAsync(
                PermissionCodes.RevenueRead,
                "Bạn không có quyền xem doanh thu để gắn chứng từ.",
                cancellationToken);

            var revenueQuery = _db.Revenues.AsNoTracking()
                .Where(r => r.RecordStatus == "active");

            if (request.BillId.HasValue)
            {
                revenueQuery = revenueQuery.Where(r => r.BillId == request.BillId.Value);
            }

            if (!string.IsNullOrEmpty(currency))
            {
                revenueQuery = revenueQuery.Where(r => r.CurrencyCode == currency);
            }

            var revenues = await revenueQuery
                .OrderByDescending(r => r.EffectiveDate)
                .ThenBy(r => r.Id)
                .Take(200)
                .ToListAsync(cancellationToken);

            if (revenues.Count == 0)
            {
                return Array.Empty<EligibleSourceLineDto>();
            }

            var billIds = revenues.Select(r => r.BillId).Distinct().ToList();
            var bills = await _db.Bills.AsNoTracking()
                .Where(b => billIds.Contains(b.Id))
                .Select(b => new { b.Id, b.BillNo, b.CustomerPartyId })
                .ToDictionaryAsync(b => b.Id, cancellationToken);

            // Filter by counterparty if specified
            if (request.CounterpartyId.HasValue)
            {
                var targetParty = request.CounterpartyId.Value;
                revenues = revenues.Where(r =>
                    (r.CustomerPartyId.HasValue && r.CustomerPartyId.Value == targetParty)
                    || (!r.CustomerPartyId.HasValue && bills.TryGetValue(r.BillId, out var b) && b.CustomerPartyId == targetParty))
                    .ToList();
            }

            var revenueIds = revenues.Select(r => r.Id).ToList();

            // Calculate active documented amounts (in-memory aggregation for SQLite / Postgres parity)
            var activeRevDetails = await _db.DocumentMatchDetails.AsNoTracking()
                .Where(d => d.TargetRevenueId.HasValue
                            && revenueIds.Contains(d.TargetRevenueId.Value)
                            && d.DetailStatus == DocumentMatchDetailStatuses.Active
                            && d.Match!.MatchStatus != DocumentMatchStatuses.Cancelled)
                .Select(d => new { RevenueId = d.TargetRevenueId!.Value, d.MatchedAmount })
                .ToListAsync(cancellationToken);

            var documentedSums = activeRevDetails
                .GroupBy(d => d.RevenueId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.MatchedAmount));

            var partyIds = revenues
                .Select(r => r.CustomerPartyId ?? (bills.TryGetValue(r.BillId, out var b) ? b.CustomerPartyId : null))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var parties = await _db.BusinessParties.AsNoTracking()
                .Where(p => partyIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

            var result = new List<EligibleSourceLineDto>();
            foreach (var r in revenues)
            {
                var documented = documentedSums.TryGetValue(r.Id, out var sum) ? sum : 0m;
                var remaining = decimal.Round(r.Amount - documented, 4, MidpointRounding.AwayFromZero);
                if (remaining <= 0m)
                {
                    continue;
                }

                bills.TryGetValue(r.BillId, out var b);
                var partyId = r.CustomerPartyId ?? b?.CustomerPartyId;
                parties.TryGetValue(partyId ?? Guid.Empty, out var customerName);

                result.Add(new EligibleSourceLineDto(
                    SourceId: r.Id,
                    SourceType: "revenue",
                    LineCode: r.RevenueTypeCode ?? "REV",
                    Description: r.RevenueTypeCode,
                    CurrencyCode: r.CurrencyCode,
                    OriginalAmount: r.Amount,
                    AlreadyDocumentedAmount: documented,
                    RemainingEligibleAmount: remaining,
                    BillId: r.BillId,
                    BillNo: b?.BillNo,
                    CounterpartyId: partyId,
                    CounterpartyName: customerName,
                    Maturity: r.FinancialMaturity,
                    EffectiveDate: r.EffectiveDate));
            }

            return result;
        }
    }
}

