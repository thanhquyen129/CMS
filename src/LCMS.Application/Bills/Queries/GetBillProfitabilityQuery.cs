using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Terminology;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

/// <summary>
/// Explicit profitability view for a Bill (Pass 2 Sprint 5 FULL).
/// view=expected|confirmed|actual|best — never sums raw across currencies (C-014).
/// </summary>
public sealed record GetBillProfitabilityQuery(Guid BillId, string View)
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
    int AllocatedCostLineCount);

public sealed record BillProfitabilityDto(
    Guid BillId,
    string BillNo,
    string View,
    DateTimeOffset AsOfTimestamp,
    IReadOnlyList<ProfitabilityCurrencyBucketDto> ByCurrency,
    bool HasMixedCurrencies,
    string Note);

public sealed class GetBillProfitabilityQueryHandler
    : IRequestHandler<GetBillProfitabilityQuery, BillProfitabilityDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetBillProfitabilityQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
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

        var revenues = await _db.Revenues.AsNoTracking()
            .Where(r => r.BillId == bill.Id && r.RecordStatus == "active")
            .ToListAsync(cancellationToken);

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

            var revenueAmount = revLines.Sum(r => AmountForView(view, r.ActualAmount, r.ConfirmedAmount, r.ExpectedAmount));
            var directAmount = costLines.Sum(c => AmountForView(view, c.ActualAmount, c.ConfirmedAmount, c.ExpectedAmount));
            var allocatedAmount = allocLines.Sum(a => a.AllocatedAmount);
            var costAmount = decimal.Round(directAmount + allocatedAmount, 4, MidpointRounding.AwayFromZero);
            var profit = decimal.Round(revenueAmount - costAmount, 4, MidpointRounding.AwayFromZero);

            var revExpected = revLines.Sum(r => r.ExpectedAmount);
            var revActual = revLines.Sum(r => r.ActualAmount ?? 0m);
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
                allocLines.Count));
        }

        var mixed = buckets.Count > 1;
        var label = VietnameseUiTerms.Get("BILL_PROFITABILITY");
        var viewLabel = VietnameseUiTerms.Get("PROFITABILITY_VIEW");
        var note = mixed
            ? $"{label}: Không cộng gộp khác loại tiền tệ. {viewLabel}={view}. Allocated cost luôn gồm trong Cost."
            : $"{label}: {viewLabel}={view}. Profit = Revenue − (Direct + Allocated). Không lưu SoT trên Bill.";

        return new BillProfitabilityDto(
            bill.Id,
            bill.BillNo,
            view == ProfitabilityViews.Best ? "best" : view,
            asOfTimestamp,
            buckets,
            mixed,
            note);
    }

    /// <summary>
    /// Layer picker: confirmed/actual use 0 when that layer is not set (explicit view honesty).
    /// best = Actual → Confirmed → Expected.
    /// </summary>
    private static decimal AmountForView(string view, decimal? actual, decimal? confirmed, decimal expected)
    {
        return view switch
        {
            ProfitabilityViews.Expected => expected,
            ProfitabilityViews.Confirmed => confirmed ?? 0m,
            ProfitabilityViews.Actual => actual ?? 0m,
            _ => actual ?? confirmed ?? expected
        };
    }
}
