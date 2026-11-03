using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Financials;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

public sealed record GetBillEffectiveFinancialWorkspaceQuery(Guid BillId, DateOnly? AsOf = null)
    : IRequest<BillEffectiveFinancialWorkspaceDto>;

public sealed record EffectiveWorkspaceLineDto(
    Guid Id,
    string Side,
    string ChargeCode,
    string EconomicIdentityKey,
    string SourceLabel,
    string MaturityLabel,
    decimal Amount,
    string CurrencyCode,
    bool EffectiveIncluded,
    string ParticipationBadge,
    string? ExclusionReasonCode,
    string? ExclusionReasonVi,
    Guid? AppliedFromRatingId);

public sealed record SegmentedProfitKpiDto(
    string Code,
    string LabelVi,
    decimal? Amount,
    string CurrencyCode,
    bool Incomplete,
    bool MaturityMix,
    string? WarningVi);

public sealed record RatingTimelineEventDto(
    DateTimeOffset At,
    string EventType,
    string LabelVi,
    string? Actor,
    string? Detail,
    Guid? RatingId);

public sealed record BillEffectiveFinancialWorkspaceDto(
    Guid BillId,
    string BillNo,
    DateOnly? AsOf,
    IReadOnlyList<EffectiveWorkspaceLineDto> Lines,
    IReadOnlyList<SegmentedProfitKpiDto> ProfitKpis,
    IReadOnlyList<RatingTimelineEventDto> Timeline,
    decimal EffectiveCost,
    decimal EffectiveRevenue,
    decimal BestAvailableProfit);

public sealed class GetBillEffectiveFinancialWorkspaceQueryHandler
    : IRequestHandler<GetBillEffectiveFinancialWorkspaceQuery, BillEffectiveFinancialWorkspaceDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetBillEffectiveFinancialWorkspaceQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BillEffectiveFinancialWorkspaceDto> Handle(
        GetBillEffectiveFinancialWorkspaceQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var bill = await _db.Bills.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy Bill.");

        var costs = await _db.Costs.AsNoTracking()
            .Where(c => c.BillId == bill.Id && c.DeletedAt == null)
            .ToListAsync(cancellationToken);
        var revenues = await _db.Revenues.AsNoTracking()
            .Where(r => r.BillId == bill.Id && r.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var costCandidates = costs.Select(c => new EconomicFactCandidate(
            c.Id,
            "cost",
            string.IsNullOrWhiteSpace(c.EconomicIdentityKey)
                ? EconomicIdentity.Build(
                    c.TenantId, c.BillId, EconomicIdentity.DirectionBuy,
                    c.CostTypeCode ?? "COST", c.TransportLegId, c.ShipmentId, c.VendorPartyId, c.ServiceOccurrenceKey)
                : c.EconomicIdentityKey!,
            c.FinancialMaturity,
            c.ExpectedAmount,
            c.ConfirmedAmount,
            c.ActualAmount,
            c.RecordStatus,
            c.SourceType,
            c.SourceId,
            c.AppliedFromRatingId,
            c.CreatedAt,
            Ambiguous: false)).ToList();

        var revCandidates = revenues.Select(r => new EconomicFactCandidate(
            r.Id,
            "revenue",
            string.IsNullOrWhiteSpace(r.EconomicIdentityKey)
                ? EconomicIdentity.Build(
                    r.TenantId, r.BillId, EconomicIdentity.DirectionSell,
                    r.RevenueTypeCode ?? "REV", null, null, r.CustomerPartyId, r.ServiceOccurrenceKey)
                : r.EconomicIdentityKey!,
            r.FinancialMaturity,
            r.ExpectedAmount,
            r.ConfirmedAmount,
            r.ActualAmount,
            r.RecordStatus,
            r.SourceType,
            r.SourceId,
            r.AppliedFromRatingId,
            r.CreatedAt)).ToList();

        var costEff = EffectiveFactSelector.Select(costCandidates);
        var revEff = EffectiveFactSelector.Select(revCandidates);
        var costById = costs.ToDictionary(c => c.Id);
        var revById = revenues.ToDictionary(r => r.Id);

        var lines = new List<EffectiveWorkspaceLineDto>();
        foreach (var l in costEff)
        {
            var c = costById[l.Id];
            lines.Add(ToLine(l, c.CostTypeCode ?? "COST", c.CurrencyCode, c.Amount, c.FinancialMaturity, c.SourceType));
        }

        foreach (var l in revEff)
        {
            var r = revById[l.Id];
            lines.Add(ToLine(l, r.RevenueTypeCode ?? "REV", r.CurrencyCode, r.Amount, r.FinancialMaturity, r.SourceType));
        }

        var effCost = EffectiveFactSelector.SumBestAvailable(costEff);
        var effRev = EffectiveFactSelector.SumBestAvailable(revEff);
        var expectedCost = EffectiveFactSelector.SumExpectedLayer(costEff);
        var expectedRev = EffectiveFactSelector.SumExpectedLayer(revEff);
        var actualCost = EffectiveFactSelector.SumActualOnly(costEff);
        var actualRev = EffectiveFactSelector.SumActualOnly(revEff);
        var actualIncomplete = EffectiveFactSelector.ActualIncomplete(costEff)
                               || EffectiveFactSelector.ActualIncomplete(revEff);
        var mix = costEff.Any(l => l.EffectiveIncluded && l.ActualAmount is not null)
                  && costEff.Any(l => l.EffectiveIncluded && l.ActualAmount is null)
                  || revEff.Any(l => l.EffectiveIncluded && l.ActualAmount is not null)
                  && revEff.Any(l => l.EffectiveIncluded && l.ActualAmount is null);

        var currency = lines.Select(l => l.CurrencyCode).FirstOrDefault() ?? "VND";
        var kpis = new List<SegmentedProfitKpiDto>
        {
            new("expected", "Lợi nhuận Dự kiến", expectedRev - expectedCost, currency, false, false, null),
            new(
                "best_available",
                "Lợi nhuận Ước tính tốt nhất",
                effRev - effCost,
                currency,
                false,
                mix,
                mix ? "Pha trộn mức độ trưởng thành (Maturity Mix) — không gọi là lợi nhuận thực tế chốt" : null),
            new(
                "actual_only",
                "Lợi nhuận Thực tế",
                actualIncomplete ? actualRev - actualCost : actualRev - actualCost,
                currency,
                actualIncomplete,
                false,
                actualIncomplete ? "Chưa đủ dữ liệu thực tế (Incomplete)" : null)
        };

        var ratings = (await _db.Ratings.AsNoTracking()
            .Where(r => r.BillId == bill.Id && r.DeletedAt == null)
            .ToListAsync(cancellationToken))
            .OrderByDescending(r => r.RatedAt)
            .Take(50)
            .ToList();

        var timeline = ratings.Select(r => new RatingTimelineEventDto(
            r.RatedAt,
            string.Equals(r.Status, RatingStatuses.Superseded, StringComparison.OrdinalIgnoreCase) ? "superseded" : "rating",
            string.Equals(r.Status, RatingStatuses.Superseded, StringComparison.OrdinalIgnoreCase)
                ? "Đã thay thế (Lịch sử)"
                : "Tính thử / Rating",
            null,
            $"Chiều {(string.Equals(r.PartyTypeCode, "customer", StringComparison.OrdinalIgnoreCase) ? "BÁN" : "MUA")}; version={r.RateVersionId}",
            r.Id)).ToList();

        foreach (var c in costs.Where(c => c.AppliedFromRatingId is not null).OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt).Take(20))
        {
            timeline.Add(new RatingTimelineEventDto(
                c.UpdatedAt ?? c.CreatedAt,
                "apply",
                "Áp dụng vào hồ sơ tài chính",
                null,
                $"Cost {c.CostTypeCode} → {c.ExpectedAmount:0.####} {c.CurrencyCode}",
                c.AppliedFromRatingId));
        }

        return new BillEffectiveFinancialWorkspaceDto(
            bill.Id,
            bill.BillNo,
            request.AsOf,
            lines.OrderBy(l => l.Side).ThenBy(l => l.ChargeCode).ToList(),
            kpis,
            timeline.OrderByDescending(t => t.At).ToList(),
            effCost,
            effRev,
            decimal.Round(effRev - effCost, 4, MidpointRounding.AwayFromZero));
    }

    private static EffectiveWorkspaceLineDto ToLine(
        EffectiveFactLine l,
        string charge,
        string currency,
        decimal displayAmount,
        string maturity,
        string? sourceType)
    {
        var sourceLabel = RatingFinancialApply.IsIndependentManualOrImport(sourceType)
            ? "Manual / Import"
            : string.Equals(sourceType, CostSourceTypes.RatingDetail, StringComparison.OrdinalIgnoreCase)
                ? "Rating Run"
                : sourceType ?? "—";
        var maturityLabel = maturity.ToLowerInvariant() switch
        {
            "actual" => "Thực tế",
            "confirmed" => "Đã xác nhận",
            _ => "Dự kiến"
        };
        return new EffectiveWorkspaceLineDto(
            l.Id,
            l.Side,
            charge,
            l.EconomicIdentityKey,
            sourceLabel,
            maturityLabel,
            l.EffectiveIncluded ? l.BestAvailableAmount : displayAmount,
            currency,
            l.EffectiveIncluded,
            l.EffectiveIncluded ? "Có (Included)" : "Không (Excluded)",
            l.ExclusionReason,
            string.IsNullOrWhiteSpace(l.ExclusionReason)
                ? null
                : RatingFinancialApply.ExclusionReasonVi(l.ExclusionReason),
            l.AppliedFromRatingId);
    }
}
