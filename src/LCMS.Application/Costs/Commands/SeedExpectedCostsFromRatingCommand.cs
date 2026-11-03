using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Commands;

public sealed record SeedExpectedCostsFromRatingCommand(Guid RatingId) : IRequest<SeedExpectedCostsResult>;

public sealed record SeedExpectedCostsResult(Guid RatingId, int CreatedCount, int ExistingCount, IReadOnlyList<Guid> CostIds);

public sealed class SeedExpectedCostsFromRatingCommandValidator : AbstractValidator<SeedExpectedCostsFromRatingCommand>
{
    public SeedExpectedCostsFromRatingCommandValidator()
    {
        RuleFor(x => x.RatingId).NotEmpty().WithMessage("Lần tính giá không hợp lệ.");
    }
}

/// <summary>
/// Apply BUY rating details → Expected Cost with EconomicIdentity supersede (ADR-0043).
/// Idempotent by source_id; re-apply same identity supersedes prior Expected-only rows.
/// </summary>
public sealed class SeedExpectedCostsFromRatingCommandHandler
    : IRequestHandler<SeedExpectedCostsFromRatingCommand, SeedExpectedCostsResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICostFxStub _fx;
    private readonly ICostApprovalGate _approvalGate;

    public SeedExpectedCostsFromRatingCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICostFxStub fx,
        ICostApprovalGate approvalGate)
    {
        _db = db;
        _tenantContext = tenantContext;
        _fx = fx;
        _approvalGate = approvalGate;
    }

    public async Task<SeedExpectedCostsResult> Handle(
        SeedExpectedCostsFromRatingCommand request,
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

        if (string.Equals(rating.PartyTypeCode, "customer", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationAppException(
                "Không thể tạo chi phí dự kiến từ lần tính giá bán (SELL). Chỉ lần tính giá mua (BUY) mới được tạo chi phí dự kiến.");
        }

        var allDetails = await _db.RatingDetails.AsNoTracking()
            .Where(d => d.RatingId == rating.Id && d.FinancialNature == "cost")
            .OrderBy(d => d.ComponentCode)
            .ToListAsync(cancellationToken);

        var details = RatingFinancialApply.FilterSeedableDetails(allDetails, "cost");
        if (details.Count == 0)
        {
            throw new ConflictAppException(
                "Lần tính giá không có dòng chi phí đủ điều kiện áp dụng (chỉ dòng đã áp dụng / appliedCharges).");
        }

        var detailIds = details.Select(d => d.Id).ToList();
        var existingBySource = await _db.Costs
            .Where(c => c.SourceType == CostSourceTypes.RatingDetail && detailIds.Contains(c.SourceId!.Value))
            .ToDictionaryAsync(c => c.SourceId!.Value, cancellationToken);

        var created = 0;
        var reused = 0;
        var costIds = new List<Guid>();
        var effective = DateOnly.FromDateTime(rating.RatedAt.UtcDateTime);
        var linkedBills = rating.TransportLegId is Guid legId
            ? await _db.BillLegLinks.AsNoTracking().CountAsync(l => l.TransportLegId == legId, cancellationToken)
            : 0;

        string? costScope = null;
        if (!string.IsNullOrWhiteSpace(rating.ContextJson))
        {
            try
            {
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(rating.ContextJson);
                if (jsonDoc.RootElement.TryGetProperty("costScope", out var csProp))
                {
                    costScope = csProp.GetString();
                }
            }
            catch
            {
                // ignore malformed snapshot
            }
        }

        var isLeg = RatingContextTypes.IsLeg(rating.ContextType);
        bool shared;
        if (string.Equals(costScope, "DIRECT_BILL_LEG", StringComparison.OrdinalIgnoreCase))
        {
            shared = false;
        }
        else if (string.Equals(costScope, "SHARED_LEG", StringComparison.OrdinalIgnoreCase))
        {
            shared = true;
        }
        else
        {
            shared = isLeg && linkedBills >= 2;
        }

        var organizationId = await _db.Bills.AsNoTracking()
            .Where(b => b.Id == rating.BillId)
            .Select(b => b.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var detail in details)
        {
            if (existingBySource.TryGetValue(detail.Id, out var already))
            {
                if (string.IsNullOrWhiteSpace(already.EconomicIdentityKey))
                {
                    already.EconomicIdentityKey = RatingFinancialApply.CostIdentityKey(rating, detail, tenantId);
                    already.ServiceOccurrenceKey = RatingFinancialApply.CostOccurrenceKey(rating);
                    already.AppliedFromRatingId = rating.Id;
                }

                costIds.Add(already.Id);
                reused++;
                continue;
            }

            var identityKey = RatingFinancialApply.CostIdentityKey(rating, detail, tenantId);
            var occurrenceKey = RatingFinancialApply.CostOccurrenceKey(rating);
            var amount = decimal.Round(detail.Amount, 4, MidpointRounding.AwayFromZero);

            var sameIdentity = (await _db.Costs
                .Where(c => c.EconomicIdentityKey == identityKey
                            && c.RecordStatus == EconomicRecordStatuses.Active
                            && c.DeletedAt == null)
                .ToListAsync(cancellationToken))
                .OrderByDescending(c => c.CreatedAt)
                .ToList();

            // Legacy rows without key: match bill/leg/vendor/charge among active rating-sourced costs.
            if (sameIdentity.Count == 0)
            {
                sameIdentity = (await _db.Costs
                    .Where(c => c.RecordStatus == EconomicRecordStatuses.Active
                                && c.DeletedAt == null
                                && c.CostTypeCode == detail.ComponentCode
                                && c.BillId == (shared ? null : rating.BillId)
                                && c.TransportLegId == rating.TransportLegId
                                && c.VendorPartyId == (rating.VendorPartyId ?? detail.PartnerSuggestedId)
                                && c.SourceType == CostSourceTypes.RatingDetail)
                    .ToListAsync(cancellationToken))
                    .OrderByDescending(c => c.CreatedAt)
                    .ToList();
            }

            if (sameIdentity.Count > 1
                && rating.TransportLegId is null
                && rating.VendorPartyId is null
                && detail.PartnerSuggestedId is null)
            {
                throw new ConflictAppException(
                    "AMBIGUOUS_ECONOMIC_IDENTITY: Không xác định được nghĩa vụ kinh tế duy nhất cho khoản phí. Cần resolve trước khi áp dụng.");
            }

            var protectedRow = sameIdentity.FirstOrDefault(RatingFinancialApply.HasProtectedMaturity);
            if (protectedRow is not null)
            {
                // BR-DF-013: update Expected only for variance; never overwrite Actual/Confirmed.
                protectedRow.ExpectedAmount = amount;
                protectedRow.EconomicIdentityKey = identityKey;
                protectedRow.ServiceOccurrenceKey = occurrenceKey;
                protectedRow.AppliedFromRatingId = rating.Id;
                foreach (var dup in sameIdentity.Where(c =>
                             c.Id != protectedRow.Id && !RatingFinancialApply.HasProtectedMaturity(c)))
                {
                    dup.RecordStatus = EconomicRecordStatuses.Superseded;
                }

                costIds.Add(protectedRow.Id);
                reused++;
                continue;
            }

            var expectedOnly = sameIdentity
                .Where(c => !RatingFinancialApply.HasProtectedMaturity(c))
                .ToList();
            foreach (var old in expectedOnly)
            {
                old.RecordStatus = EconomicRecordStatuses.Superseded;
            }

            if (shared && rating.TransportLegId is Guid sharedLegId)
            {
                var existingLegCost = await _db.Costs
                    .Where(c => c.TransportLegId == sharedLegId
                                && c.AttributionType == CostAttributionTypes.Shared
                                && c.OriginLevel == CostOriginLevels.Leg
                                && c.PricingRuleId == detail.PricingRuleId
                                && c.DeletedAt == null
                                && c.RecordStatus == EconomicRecordStatuses.Active)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingLegCost is not null)
                {
                    existingLegCost.ExpectedAmount = amount;
                    existingLegCost.Amount = amount;
                    existingLegCost.EconomicIdentityKey = identityKey;
                    existingLegCost.ServiceOccurrenceKey = occurrenceKey;
                    existingLegCost.AppliedFromRatingId = rating.Id;
                    await _fx.ApplyToCostAsync(existingLegCost, amount, cancellationToken);
                    costIds.Add(existingLegCost.Id);
                    reused++;
                    continue;
                }
            }

            if (detail.EconomicChargeTypeId is Guid ectId)
            {
                var active = await _db.EconomicChargeTypes.AsNoTracking()
                    .AnyAsync(e => e.TenantId == tenantId && e.Id == ectId && e.IsActive, cancellationToken);
                if (!active)
                {
                    throw new ValidationAppException(
                        $"Khoản mục kinh tế '{ectId}' cho dòng '{detail.ComponentCode}' không tồn tại hoặc đã bị vô hiệu hóa trên master. Không thể seed chi phí.");
                }
            }

            var cost = new Cost
            {
                TenantId = tenantId,
                BillId = shared ? null : rating.BillId,
                AttributionType = shared ? CostAttributionTypes.Shared : CostAttributionTypes.Direct,
                OriginLevel = rating.TransportLegId is null ? CostOriginLevels.Bill : CostOriginLevels.Leg,
                TransportLegId = rating.TransportLegId,
                ShipmentId = rating.ShipmentId,
                RateCardId = rating.RateCardId,
                RateVersionId = rating.RateVersionId,
                PricingRuleId = detail.PricingRuleId,
                OrganizationId = organizationId,
                FinancialMaturity = CostMaturities.Expected,
                ExpectedAmount = amount,
                Amount = amount,
                CurrencyCode = detail.CurrencyCode,
                CostTypeCode = detail.ComponentCode,
                EconomicChargeTypeId = detail.EconomicChargeTypeId,
                PartnerSuggestedId = detail.PartnerSuggestedId ?? rating.VendorPartyId,
                VendorPartyId = rating.VendorPartyId ?? detail.PartnerSuggestedId,
                NetAmount = detail.NetAmount,
                VatRate = detail.VatRate,
                VatAmount = detail.VatAmount,
                GrossAmount = detail.GrossAmount,
                SourceType = CostSourceTypes.RatingDetail,
                SourceId = detail.Id,
                EconomicIdentityKey = identityKey,
                ServiceOccurrenceKey = occurrenceKey,
                AppliedFromRatingId = rating.Id,
                RecordStatus = EconomicRecordStatuses.Active,
                ApprovalStatus = "not_required",
                EffectiveDate = effective
            };
            await _fx.ApplyToCostAsync(cost, amount, cancellationToken);
            await _approvalGate.RefreshPendingFlagAsync(cost, cancellationToken);
            _db.Costs.Add(cost);
            costIds.Add(cost.Id);
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
                var reloaded = await _db.Costs.AsNoTracking()
                    .Where(c => c.SourceType == CostSourceTypes.RatingDetail && detailIds.Contains(c.SourceId!.Value))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                return new SeedExpectedCostsResult(rating.Id, 0, reloaded.Count, reloaded);
            }
        }

        return new SeedExpectedCostsResult(
            rating.Id,
            created,
            details.Count - created,
            costIds);
    }
}
