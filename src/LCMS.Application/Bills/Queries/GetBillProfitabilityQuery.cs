using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Fx;
using LCMS.Application.Revenues;
using LCMS.Domain.Entities;
using LCMS.Domain.Terminology;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

/// <summary>
/// Explicit profitability view for a Bill (Pass 2 Sprint 5 FULL).
/// view=expected|confirmed|actual|best — never sums raw across currencies (C-014).
/// </summary>
public sealed record GetBillProfitabilityQuery(Guid BillId, string View, string? ReportingCurrency = null)
    : IRequest<BillProfitabilityDto>;

public sealed class GetBillProfitabilityQueryValidator : AbstractValidator<GetBillProfitabilityQuery>
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ProfitabilityViews.Expected,
        ProfitabilityViews.Confirmed,
        ProfitabilityViews.Actual,
        ProfitabilityViews.Best
    };

    public GetBillProfitabilityQueryValidator()
    {
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.View)
            .NotEmpty().WithMessage("Góc nhìn lợi nhuận không được để trống.")
            .Must(v => Allowed.Contains(v.Trim()))
            .WithMessage("Góc nhìn lợi nhuận phải là expected, confirmed, actual hoặc best.");
    }
}

public static class ProfitabilityViews
{
    public const string Expected = "expected";
    public const string Confirmed = "confirmed";
    public const string Actual = "actual";
    public const string Best = "best";
}

public sealed record ProfitabilityCurrencyBucketDto(
    string CurrencyCode,
    decimal RevenueAmount,
    decimal DirectCostAmount,
    decimal AllocatedCostAmount,
    decimal CostAmount,
    decimal ProfitAmount,
    decimal RevenueVarianceExpectedVsActual,
    decimal CostVarianceExpectedVsActual,
    decimal ProfitVarianceExpectedVsActual,
    int RevenueLineCount,
    int DirectCostLineCount,
    int AllocatedCostLineCount,
    decimal? MarginRate = null);

public sealed record BillProfitabilityDto(
    Guid BillId,
    string BillNo,
    string View,
    DateTimeOffset AsOfTimestamp,
    IReadOnlyList<ProfitabilityCurrencyBucketDto> ByCurrency,
    bool HasMixedCurrencies,
    string Note,
    string? ReportingCurrency = null,
    decimal? ReportingRevenue = null,
    decimal? ReportingCost = null,
    decimal? ReportingProfit = null,
    IReadOnlyList<ProfitabilityFxTraceDto>? FxTrace = null,
    IReadOnlyList<string>? UnconvertedCurrencies = null);

public sealed record ProfitabilityFxTraceDto(
    string CurrencyCode,
    decimal Rate,
    string Source,
    DateOnly? RateDate,
    Guid? FxRateId,
    decimal RevenueAmount,
    decimal ConvertedRevenue,
    decimal CostAmount,
    decimal ConvertedCost);

public sealed class GetBillProfitabilityQueryHandler
    : IRequestHandler<GetBillProfitabilityQuery, BillProfitabilityDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IFxRateLookup _fx;

    public GetBillProfitabilityQueryHandler(ILcmsDbContext db, ITenantContext tenantContext, IFxRateLookup fx)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fx = fx;
    }

    public async Task<BillProfitabilityDto> Handle(
        GetBillProfitabilityQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var view = request.View.Trim().ToLowerInvariant();
        var asOfTimestamp = DateTimeOffset.UtcNow;

        var bill = await _db.Bills.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy Bill.");

        var revenues = await LoadRevenuesAsync(bill.Id, cancellationToken);
        var shares = await LoadSharesAsync(bill.Id, revenues, cancellationToken);

        var directCosts = await _db.Costs.AsNoTracking()
            .Where(c => c.BillId == bill.Id
                        && c.AttributionType == CostAttributionTypes.Direct
                        && c.RecordStatus == "active")
            .ToListAsync(cancellationToken);

        var allocatedList = await (
            from d in _db.CostAllocationDetails.AsNoTracking()
            join a in _db.CostAllocations.AsNoTracking() on d.AllocationId equals a.Id
            join c in _db.Costs.AsNoTracking() on a.CostId equals c.Id
            where d.BillId == bill.Id
                  && a.AllocationStatus == CostAllocationStatuses.Finalized
                  && c.RecordStatus == "active"
            select new { d.AllocatedAmount, c.CurrencyCode }
        ).ToListAsync(cancellationToken);

        var currencyCodes = revenues.Select(r => r.CurrencyCode)
            .Concat(directCosts.Select(c => c.CurrencyCode))
            .Concat(allocatedList.Select(a => a.CurrencyCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var buckets = new List<ProfitabilityCurrencyBucketDto>();
        foreach (var code in currencyCodes)
        {
            var revLines = revenues
                .Where(r => string.Equals(r.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var costLines = directCosts
                .Where(c => string.Equals(c.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var allocLines = allocatedList
                .Where(a => string.Equals(a.CurrencyCode, code, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var revenueAmount = revLines.Sum(r => ShareOf(view, r, shares));
            var directAmount = costLines.Sum(c => ProfitabilityShare.Layer(view, c.ActualAmount, c.ConfirmedAmount, c.ExpectedAmount));
            var allocatedAmount = allocLines.Sum(a => a.AllocatedAmount);
            var costAmount = decimal.Round(directAmount + allocatedAmount, 4, MidpointRounding.AwayFromZero);
            var profit = decimal.Round(revenueAmount - costAmount, 4, MidpointRounding.AwayFromZero);

            var revExpected = revLines.Sum(r => ShareOf(ProfitabilityViews.Expected, r, shares));
            var revActual = revLines.Sum(r => ShareOf(ProfitabilityViews.Actual, r, shares));
            var costExpected = costLines.Sum(c => c.ExpectedAmount) + allocatedAmount;
            var costActual = costLines.Sum(c => c.ActualAmount ?? 0m) + allocatedAmount;
            var profitExpected = revExpected - costExpected;
            var profitActual = revActual - costActual;

            buckets.Add(new ProfitabilityCurrencyBucketDto(
                code.ToUpperInvariant(),
                decimal.Round(revenueAmount, 4, MidpointRounding.AwayFromZero),
                decimal.Round(directAmount, 4, MidpointRounding.AwayFromZero),
                decimal.Round(allocatedAmount, 4, MidpointRounding.AwayFromZero),
                costAmount,
                profit,
                decimal.Round(revExpected - revActual, 4, MidpointRounding.AwayFromZero),
                decimal.Round(costExpected - costActual, 4, MidpointRounding.AwayFromZero),
                decimal.Round(profitExpected - profitActual, 4, MidpointRounding.AwayFromZero),
                revLines.Count,
                costLines.Count,
                allocLines.Count,
                ProfitabilityShare.MarginPercent(decimal.Round(revenueAmount, 4, MidpointRounding.AwayFromZero), profit)));
        }

        var mixed = buckets.Count > 1;
        var label = VietnameseUiTerms.Get("BILL_PROFITABILITY");
        var viewLabel = VietnameseUiTerms.Get("PROFITABILITY_VIEW");
        var note = mixed
            ? $"{label}: Không cộng gộp khác loại tiền tệ. {viewLabel}={view}. Allocated cost luôn gồm trong Cost."
            : $"{label}: {viewLabel}={view}. Profit = Revenue − (Direct + Allocated). Không lưu SoT trên Bill.";

        var (reportingRevenue, reportingCost, reportingProfit, trace, missing) =
            await ConvertAsync(request.ReportingCurrency, buckets, cancellationToken);

        return new BillProfitabilityDto(
            bill.Id,
            bill.BillNo,
            view == ProfitabilityViews.Best ? "best" : view,
            asOfTimestamp,
            buckets,
            mixed,
            note,
            string.IsNullOrWhiteSpace(request.ReportingCurrency) ? null : request.ReportingCurrency.Trim().ToUpperInvariant(),
            reportingRevenue,
            reportingCost,
            reportingProfit,
            trace,
            missing);
    }

    private async Task<List<Revenue>> LoadRevenuesAsync(Guid billId, CancellationToken cancellationToken)
    {
        var own = await _db.Revenues.AsNoTracking()
            .Where(r => r.BillId == billId && r.RecordStatus == "active")
            .ToListAsync(cancellationToken);
        var mappedIds = await (
            from d in _db.RevenueMappingDetails.AsNoTracking()
            join m in _db.RevenueMappings.AsNoTracking() on d.MappingId equals m.Id
            where d.BillId == billId && m.MappingStatus == CostAllocationStatuses.Finalized
            select m.RevenueId
        ).Distinct().ToListAsync(cancellationToken);
        var extraIds = mappedIds.Except(own.Select(r => r.Id)).ToList();
        if (extraIds.Count == 0)
        {
            return own;
        }

        var extra = await _db.Revenues.AsNoTracking()
            .Where(r => extraIds.Contains(r.Id) && r.RecordStatus == "active")
            .ToListAsync(cancellationToken);
        own.AddRange(extra);
        return own;
    }

    private async Task<Dictionary<Guid, (string Maturity, decimal Amount)>> LoadSharesAsync(
        Guid billId,
        IReadOnlyList<Revenue> revenues,
        CancellationToken cancellationToken)
    {
        var ids = revenues.Select(r => r.Id).ToList();
        var maps = await _db.RevenueMappings.AsNoTracking()
            .Where(m => ids.Contains(m.RevenueId) && m.MappingStatus == CostAllocationStatuses.Finalized)
            .Select(m => new { m.Id, m.RevenueId, m.VersionNo, m.MappedMaturity })
            .ToListAsync(cancellationToken);
        var mapIds = maps.Select(m => m.Id).ToList();
        var lines = await _db.RevenueMappingDetails.AsNoTracking()
            .Where(d => mapIds.Contains(d.MappingId) && d.BillId == billId)
            .Select(d => new { d.MappingId, d.AllocatedAmount })
            .ToListAsync(cancellationToken);
        return maps
            .GroupBy(m => m.RevenueId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var top = g.OrderByDescending(x => x.VersionNo).First();
                    var amount = lines.Where(l => l.MappingId == top.Id).Select(l => l.AllocatedAmount).FirstOrDefault();
                    return (top.MappedMaturity, amount);
                });
    }

    private static decimal ShareOf(string view, Revenue revenue, IReadOnlyDictionary<Guid, (string Maturity, decimal Amount)> shares)
    {
        shares.TryGetValue(revenue.Id, out var share);
        var has = shares.ContainsKey(revenue.Id);
        return ProfitabilityShare.Amount(
            view,
            revenue.ActualAmount,
            revenue.ConfirmedAmount,
            revenue.ExpectedAmount,
            has,
            share.Maturity,
            share.Amount);
    }

    private async Task<(decimal? Revenue, decimal? Cost, decimal? Profit, IReadOnlyList<ProfitabilityFxTraceDto>? Trace, IReadOnlyList<string>? Missing)> ConvertAsync(
        string? reportingCurrency,
        IReadOnlyList<ProfitabilityCurrencyBucketDto> buckets,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportingCurrency) || buckets.Count == 0)
        {
            return (null, null, null, null, null);
        }

        var target = reportingCurrency.Trim().ToUpperInvariant();
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var trace = new List<ProfitabilityFxTraceDto>();
        var missing = new List<string>();
        foreach (var bucket in buckets)
        {
            if (string.Equals(bucket.CurrencyCode, target, StringComparison.OrdinalIgnoreCase))
            {
                trace.Add(new ProfitabilityFxTraceDto(bucket.CurrencyCode, 1m, "identity", asOf, null, bucket.RevenueAmount, bucket.RevenueAmount, bucket.CostAmount, bucket.CostAmount));
                continue;
            }

            var rate = await _fx.ResolveAsync(bucket.CurrencyCode, target, asOf, cancellationToken);
            if (rate is null)
            {
                missing.Add(bucket.CurrencyCode);
                continue;
            }

            var revenue = decimal.Round(bucket.RevenueAmount * rate.Rate, 4, MidpointRounding.AwayFromZero);
            var cost = decimal.Round(bucket.CostAmount * rate.Rate, 4, MidpointRounding.AwayFromZero);
            trace.Add(new ProfitabilityFxTraceDto(bucket.CurrencyCode, rate.Rate, rate.Source, rate.RateDate, rate.FxRateId, bucket.RevenueAmount, revenue, bucket.CostAmount, cost));
        }

        if (missing.Count > 0)
        {
            return (null, null, null, trace, missing);
        }

        var reportingRevenue = trace.Sum(t => t.ConvertedRevenue);
        var reportingCost = trace.Sum(t => t.ConvertedCost);
        return (reportingRevenue, reportingCost, decimal.Round(reportingRevenue - reportingCost, 4, MidpointRounding.AwayFromZero), trace, missing);
    }
}
