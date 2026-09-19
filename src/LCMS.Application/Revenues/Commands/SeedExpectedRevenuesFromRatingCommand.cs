using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Revenues;
using LCMS.Domain.Entities;
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
/// Idempotent seed: rating_details (financial_nature=revenue) → Revenue Expected.
/// Does not invent a second economic fact (source_type + source_id unique).
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

        var details = await _db.RatingDetails.AsNoTracking()
            .Where(d => d.RatingId == rating.Id && d.FinancialNature == "revenue")
            .OrderBy(d => d.ComponentCode)
            .ToListAsync(cancellationToken);

        if (details.Count == 0)
        {
            throw new ConflictAppException(
                "Lần tính giá không có dòng doanh thu để tạo doanh thu dự kiến. Thêm thành phần tính chất Doanh thu trên quy tắc bảng giá.");
        }

        var detailIds = details.Select(d => d.Id).ToList();
        var existing = await _db.Revenues
            .Where(r => r.SourceType == RevenueSourceTypes.RatingDetail && detailIds.Contains(r.SourceId!.Value))
            .ToListAsync(cancellationToken);
        var existingBySource = existing.ToDictionary(r => r.SourceId!.Value);

        var created = 0;
        var revenueIds = new List<Guid>();
        var effective = DateOnly.FromDateTime(rating.RatedAt.UtcDateTime);

        foreach (var detail in details)
        {
            if (existingBySource.TryGetValue(detail.Id, out var already))
            {
                revenueIds.Add(already.Id);
                continue;
            }

            var amount = decimal.Round(detail.Amount, 4, MidpointRounding.AwayFromZero);
            var revenue = new Revenue
            {
                TenantId = tenantId,
                BillId = rating.BillId,
                FinancialMaturity = RevenueMaturities.Expected,
                ExpectedAmount = amount,
                Amount = amount,
                CurrencyCode = detail.CurrencyCode,
                RevenueTypeCode = detail.ComponentCode,
                SourceType = RevenueSourceTypes.RatingDetail,
                SourceId = detail.Id,
                RecordStatus = "active",
                ApprovalStatus = "not_required",
                EffectiveDate = effective
            };
            await _fx.ApplyToRevenueAsync(revenue, amount, cancellationToken);
            _approvalGate.RefreshPendingFlag(revenue);
            _db.Revenues.Add(revenue);
            revenueIds.Add(revenue.Id);
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
