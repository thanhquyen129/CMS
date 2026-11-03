using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Ratings;
using LCMS.Application.Revenues;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Commands;

public sealed record SeedExpectedRevenuesFromRatingCommand(Guid RatingId)
    : IRequest<SeedExpectedRevenuesResult>;

public sealed record SeedExpectedRevenuesResult(
    Guid RatingId,
    int CreatedCount,
    int ExistingCount,
    IReadOnlyList<Guid> RevenueIds);

public sealed class SeedExpectedRevenuesFromRatingCommandValidator
    : AbstractValidator<SeedExpectedRevenuesFromRatingCommand>
{
    public SeedExpectedRevenuesFromRatingCommandValidator()
    {
        RuleFor(x => x.RatingId).NotEmpty().WithMessage("Lần tính giá không hợp lệ.");
    }
}

/// <summary>
/// Apply SELL rating details → Expected Revenue with EconomicIdentity supersede (ADR-0043).
/// </summary>
public sealed class SeedExpectedRevenuesFromRatingCommandHandler
    : IRequestHandler<SeedExpectedRevenuesFromRatingCommand, SeedExpectedRevenuesResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IRevenueFxStub _fx;
    private readonly IRevenueApprovalGate _approvalGate;

    public SeedExpectedRevenuesFromRatingCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IRevenueFxStub fx,
        IRevenueApprovalGate approvalGate)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fx = fx;
        _approvalGate = approvalGate;
    }

    public async Task<SeedExpectedRevenuesResult> Handle(
        SeedExpectedRevenuesFromRatingCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;

        var rating = await _db.Ratings.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RatingId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lần tính giá.");

        if (string.Equals(rating.PartyTypeCode, "vendor", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationAppException(
                "Không thể tạo doanh thu dự kiến từ lần tính giá mua (BUY). Chỉ lần tính giá bán (SELL) mới được tạo doanh thu dự kiến.");
        }

        var allDetails = await _db.RatingDetails.AsNoTracking()
            .Where(d => d.RatingId == rating.Id && d.FinancialNature == "revenue")
            .OrderBy(d => d.ComponentCode)
            .ToListAsync(cancellationToken);

        var details = RatingFinancialApply.FilterSeedableDetails(allDetails, "revenue");
        if (details.Count == 0)
        {
            throw new ConflictAppException(
                "Lần tính giá không có dòng doanh thu đủ điều kiện áp dụng (chỉ dòng đã áp dụng / appliedCharges).");
        }

        var detailIds = details.Select(d => d.Id).ToList();
        var existingBySource = await _db.Revenues
            .Where(r => r.SourceType == RevenueSourceTypes.RatingDetail && detailIds.Contains(r.SourceId!.Value))
            .ToDictionaryAsync(r => r.SourceId!.Value, cancellationToken);

        var created = 0;
        var reused = 0;
        var revenueIds = new List<Guid>();
        var effective = DateOnly.FromDateTime(rating.RatedAt.UtcDateTime);

        foreach (var detail in details)
        {
            if (existingBySource.TryGetValue(detail.Id, out var already))
            {
                if (string.IsNullOrWhiteSpace(already.EconomicIdentityKey))
                {
                    already.EconomicIdentityKey = RatingFinancialApply.RevenueIdentityKey(rating, detail, tenantId);
                    already.ServiceOccurrenceKey = RatingFinancialApply.RevenueOccurrenceKey(rating, detail);
                    already.AppliedFromRatingId = rating.Id;
                }

                revenueIds.Add(already.Id);
                reused++;
                continue;
            }

            var identityKey = RatingFinancialApply.RevenueIdentityKey(rating, detail, tenantId);
            var occurrenceKey = RatingFinancialApply.RevenueOccurrenceKey(rating, detail);
            var amount = decimal.Round(detail.Amount, 4, MidpointRounding.AwayFromZero);

            var sameIdentity = (await _db.Revenues
                .Where(r => r.EconomicIdentityKey == identityKey
                            && r.RecordStatus == EconomicRecordStatuses.Active
                            && r.DeletedAt == null)
                .ToListAsync(cancellationToken))
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            if (sameIdentity.Count == 0)
            {
                sameIdentity = (await _db.Revenues
                    .Where(r => r.RecordStatus == EconomicRecordStatuses.Active
                                && r.DeletedAt == null
                                && r.BillId == rating.BillId
                                && r.RevenueTypeCode == detail.ComponentCode
                                && r.SourceType == RevenueSourceTypes.RatingDetail
                                && (detail.PartnerSuggestedId == null
                                    || r.CustomerPartyId == detail.PartnerSuggestedId
                                    || r.PartnerSuggestedId == detail.PartnerSuggestedId))
                    .ToListAsync(cancellationToken))
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
            }

            var protectedRow = sameIdentity.FirstOrDefault(RatingFinancialApply.HasProtectedMaturity);
            if (protectedRow is not null)
            {
                protectedRow.ExpectedAmount = amount;
                protectedRow.EconomicIdentityKey = identityKey;
                protectedRow.ServiceOccurrenceKey = occurrenceKey;
                protectedRow.AppliedFromRatingId = rating.Id;
                foreach (var dup in sameIdentity.Where(r =>
                             r.Id != protectedRow.Id && !RatingFinancialApply.HasProtectedMaturity(r)))
                {
                    dup.RecordStatus = EconomicRecordStatuses.Superseded;
                }

                revenueIds.Add(protectedRow.Id);
                reused++;
                continue;
            }

            foreach (var old in sameIdentity.Where(r => !RatingFinancialApply.HasProtectedMaturity(r)))
            {
                old.RecordStatus = EconomicRecordStatuses.Superseded;
            }

            var revenue = new Revenue
            {
                TenantId = tenantId,
                BillId = rating.BillId,
                FinancialMaturity = RevenueMaturities.Expected,
                ExpectedAmount = amount,
                Amount = amount,
                CurrencyCode = detail.CurrencyCode,
                RevenueTypeCode = detail.ComponentCode,
                EconomicChargeTypeId = detail.EconomicChargeTypeId,
                PartnerSuggestedId = detail.PartnerSuggestedId,
                CustomerPartyId = detail.PartnerSuggestedId,
                NetAmount = detail.NetAmount,
                VatRate = detail.VatRate,
                VatAmount = detail.VatAmount,
                GrossAmount = detail.GrossAmount,
                SourceType = RevenueSourceTypes.RatingDetail,
                SourceId = detail.Id,
                EconomicIdentityKey = identityKey,
                ServiceOccurrenceKey = occurrenceKey,
                AppliedFromRatingId = rating.Id,
                RecordStatus = EconomicRecordStatuses.Active,
                ApprovalStatus = "not_required",
                EffectiveDate = effective
            };
            await _fx.ApplyToRevenueAsync(revenue, amount, cancellationToken);
            _approvalGate.RefreshPendingFlag(revenue);
            _db.Revenues.Add(revenue);
            revenueIds.Add(revenue.Id);
            created++;
        }

        if (created > 0 || reused > 0)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                var reloaded = await _db.Revenues.AsNoTracking()
                    .Where(r => r.SourceType == RevenueSourceTypes.RatingDetail && detailIds.Contains(r.SourceId!.Value))
                    .Select(r => r.Id)
                    .ToListAsync(cancellationToken);
                return new SeedExpectedRevenuesResult(rating.Id, 0, reloaded.Count, reloaded);
            }
        }

        return new SeedExpectedRevenuesResult(
            rating.Id,
            created,
            details.Count - created,
            revenueIds);
    }
}
