using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Domain.Entities;
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
/// Idempotent seed: rating_details (financial_nature=cost) → Cost Expected with source_type/source_id.
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

        var details = await _db.RatingDetails.AsNoTracking()
            .Where(d => d.RatingId == rating.Id
                        && d.FinancialNature == "cost")
            .OrderBy(d => d.ComponentCode)
            .ToListAsync(cancellationToken);

        if (details.Count == 0)
        {
            throw new ConflictAppException("Lần tính giá không có dòng chi phí để tạo chi phí dự kiến.");
        }

        var detailIds = details.Select(d => d.Id).ToList();
        var existing = await _db.Costs
            .Where(c => c.SourceType == CostSourceTypes.RatingDetail && detailIds.Contains(c.SourceId!.Value))
            .ToListAsync(cancellationToken);
        var existingBySource = existing.ToDictionary(c => c.SourceId!.Value);

        var created = 0;
        var costIds = new List<Guid>();
        var effective = DateOnly.FromDateTime(rating.RatedAt.UtcDateTime);
        var linkedBills = rating.TransportLegId is Guid legId
            ? await _db.BillLegLinks.AsNoTracking().CountAsync(l => l.TransportLegId == legId, cancellationToken)
            : 0;
        var shared = RatingContextTypes.IsLeg(rating.ContextType) && linkedBills >= 2;
        var organizationId = await _db.Bills.AsNoTracking()
            .Where(b => b.Id == rating.BillId)
            .Select(b => b.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);

        foreach (var detail in details)
        {
            if (existingBySource.TryGetValue(detail.Id, out var already))
            {
                costIds.Add(already.Id);
                continue;
            }

            // UT-26 / DO-10 / GAP-04: Kiểm tra mapping khoản mục kinh tế trên master trước khi seed
            if (detail.EconomicChargeTypeId is Guid ectId)
            {
                var active = await _db.EconomicChargeTypes.AsNoTracking()
                    .AnyAsync(e => e.TenantId == tenantId && e.Id == ectId && e.IsActive, cancellationToken);
                if (!active)
                {
                    throw new ValidationAppException($"Khoản mục kinh tế '{ectId}' cho dòng '{detail.ComponentCode}' không tồn tại hoặc đã bị vô hiệu hóa trên master. Không thể seed chi phí.");
                }
            }

            var amount = decimal.Round(detail.Amount, 4, MidpointRounding.AwayFromZero);
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
                RecordStatus = "active",
                ApprovalStatus = "not_required",
                EffectiveDate = effective
            };
            await _fx.ApplyToCostAsync(cost, amount, cancellationToken);
            await _approvalGate.RefreshPendingFlagAsync(cost, cancellationToken);
            _db.Costs.Add(cost);
            costIds.Add(cost.Id);
            created++;
        }

        if (created > 0)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Concurrent seed — reload existing for all details
                var reloaded = await _db.Costs.AsNoTracking()
                    .Where(c => c.SourceType == CostSourceTypes.RatingDetail && detailIds.Contains(c.SourceId!.Value))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                return new SeedExpectedCostsResult(
                    rating.Id,
                    0,
                    reloaded.Count,
                    reloaded);
            }
        }

        return new SeedExpectedCostsResult(
            rating.Id,
            created,
            details.Count - created,
            costIds);
    }
}
