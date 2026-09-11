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

        foreach (var detail in details)
        {
            if (existingBySource.TryGetValue(detail.Id, out var already))
            {
                costIds.Add(already.Id);
                continue;
            }

            var amount = decimal.Round(detail.Amount, 4, MidpointRounding.AwayFromZero);
            var cost = new Cost
            {
                TenantId = tenantId,
                BillId = rating.BillId,
                AttributionType = CostAttributionTypes.Direct,
                FinancialMaturity = CostMaturities.Expected,
                ExpectedAmount = amount,
                Amount = amount,
                CurrencyCode = detail.CurrencyCode,
                CostTypeCode = detail.ComponentCode,
                SourceType = CostSourceTypes.RatingDetail,
                SourceId = detail.Id,
                RecordStatus = "active",
                ApprovalStatus = "not_required",
                EffectiveDate = effective
            };
            _fx.ApplyToCost(cost, amount);
            _approvalGate.RefreshPendingFlag(cost);
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
