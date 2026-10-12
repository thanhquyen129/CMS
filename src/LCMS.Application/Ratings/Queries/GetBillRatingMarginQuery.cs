using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Fx;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings.Queries;

public sealed record BillRatingMarginDto(
    string ReportingCurrency,
    decimal? BuyAmount,
    string? BuyCurrency,
    decimal? SellAmount,
    string? SellCurrency,
    decimal? ReportingCost,
    decimal? ReportingRevenue,
    decimal? ReportingProfit,
    bool FxMissing,
    string Notice);

public sealed record GetBillRatingMarginQuery(Guid BillId) : IRequest<BillRatingMarginDto>;

public static class RatingPairMargin
{
    public static BillRatingMarginDto Compute(
        string reportingCurrency,
        (string Currency, decimal Amount)? buy,
        (string Currency, decimal Amount)? sell,
        Func<string, decimal?> rateToReporting)
    {
        var reporting = reportingCurrency.Trim().ToUpperInvariant();
        if (buy is null && sell is null)
        {
            return new BillRatingMarginDto(reporting, null, null, null, null, null, null, null, false, "Chưa có chi phí và doanh thu dự kiến từ lần tính giá.");
        }

        if (buy is null || sell is null)
        {
            return new BillRatingMarginDto(
                reporting,
                buy?.Amount,
                buy?.Currency,
                sell?.Amount,
                sell?.Currency,
                null,
                null,
                null,
                false,
                "Chỉ có một chiều dự kiến từ lần tính giá. Không hiển thị lợi nhuận.");
        }

        var cost = Convert(buy.Value, reporting, rateToReporting);
        var revenue = Convert(sell.Value, reporting, rateToReporting);
        if (cost is null || revenue is null)
        {
            return new BillRatingMarginDto(
                reporting,
                buy.Value.Amount,
                buy.Value.Currency,
                sell.Value.Amount,
                sell.Value.Currency,
                null,
                null,
                null,
                true,
                "Thiếu tỷ giá sang tiền tệ báo cáo. Không hiển thị lợi nhuận quy đổi.");
        }

        return new BillRatingMarginDto(
            reporting,
            buy.Value.Amount,
            buy.Value.Currency,
            sell.Value.Amount,
            sell.Value.Currency,
            cost,
            revenue,
            decimal.Round(revenue.Value - cost.Value, 4, MidpointRounding.AwayFromZero),
            false,
            "Lợi nhuận dự kiến của hai lần tính giá mới nhất.");
    }

    private static decimal? Convert((string Currency, decimal Amount) side, string reporting, Func<string, decimal?> rateToReporting)
    {
        var currency = side.Currency.Trim().ToUpperInvariant();
        if (string.Equals(currency, reporting, StringComparison.OrdinalIgnoreCase))
        {
            return side.Amount;
        }

        var rate = rateToReporting(currency);
        if (rate is null)
        {
            return null;
        }

        return decimal.Round(side.Amount * rate.Value, 4, MidpointRounding.AwayFromZero);
    }

    public static BillRatingMarginDto Redact(BillRatingMarginDto source, bool canCost, bool canRevenue)
    {
        if (canCost && canRevenue)
        {
            return source;
        }

        return source with
        {
            BuyAmount = canCost ? source.BuyAmount : null,
            BuyCurrency = canCost ? source.BuyCurrency : null,
            SellAmount = canRevenue ? source.SellAmount : null,
            SellCurrency = canRevenue ? source.SellCurrency : null,
            ReportingCost = null,
            ReportingRevenue = null,
            ReportingProfit = null,
            FxMissing = false,
            Notice = "Không đủ quyền xem lợi nhuận.",
        };
    }
}

public sealed class GetBillRatingMarginQueryHandler : IRequestHandler<GetBillRatingMarginQuery, BillRatingMarginDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IReportingCurrencyProvider _reporting;
    private readonly IFxRateLookup _fx;
    private readonly IPermissionService _permissions;

    public GetBillRatingMarginQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        IReportingCurrencyProvider reporting,
        IFxRateLookup fx,
        IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _reporting = reporting;
        _fx = fx;
        _permissions = permissions;
    }

    public async Task<BillRatingMarginDto> Handle(GetBillRatingMarginQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var billExists = await _db.Bills.AsNoTracking().AnyAsync(b => b.Id == request.BillId, cancellationToken);
        if (!billExists)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        var ratings = await _db.Ratings.AsNoTracking()
            .Where(r => r.BillId == request.BillId)
            .Select(r => new { r.RatedAt, r.Status, r.PartyTypeCode, r.CurrencyCode, r.TotalAmount })
            .ToListAsync(cancellationToken);
        var active = ratings
            .Where(r => !string.Equals(r.Status, RatingStatuses.Superseded, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.RatedAt)
            .ToList();
        var buy = active.FirstOrDefault(r => string.Equals(r.PartyTypeCode, "vendor", StringComparison.OrdinalIgnoreCase));
        var sell = active.FirstOrDefault(r => string.Equals(r.PartyTypeCode, "customer", StringComparison.OrdinalIgnoreCase));
        var reporting = await _reporting.GetAsync(cancellationToken);
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var currency in new[] { buy?.CurrencyCode, sell?.CurrencyCode })
        {
            if (string.IsNullOrWhiteSpace(currency) || rates.ContainsKey(currency))
            {
                continue;
            }

            if (string.Equals(currency, reporting, StringComparison.OrdinalIgnoreCase))
            {
                rates[currency] = 1m;
                continue;
            }

            var found = await _fx.ResolveAsync(currency, reporting, asOf, cancellationToken);
            if (found is not null)
            {
                rates[currency] = found.Rate;
            }
        }

        var margin = RatingPairMargin.Compute(
            reporting,
            buy is null ? null : (buy.CurrencyCode, buy.TotalAmount),
            sell is null ? null : (sell.CurrencyCode, sell.TotalAmount),
            currency => rates.TryGetValue(currency, out var rate) ? rate : null);
        var canCost = await _permissions.HasPermissionAsync(PermissionCodes.CostRead, cancellationToken);
        var canRevenue = await _permissions.HasPermissionAsync(PermissionCodes.RevenueRead, cancellationToken);
        return RatingPairMargin.Redact(margin, canCost, canRevenue);
    }
}
