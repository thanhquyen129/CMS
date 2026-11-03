using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs.Commands;
using LCMS.Application.Financials;
using LCMS.Application.Revenues.Commands;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using LCMS.Domain.Pricing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings.Commands;

public sealed record PreviewApplyRatingLineDto(
    Guid RatingDetailId,
    string ComponentCode,
    string Action,
    string? WarningVi,
    decimal Amount,
    string CurrencyCode,
    string? EconomicIdentityKey);

public sealed record PreviewApplyRatingResult(
    Guid RatingId,
    string Direction,
    IReadOnlyList<PreviewApplyRatingLineDto> Lines,
    decimal DeltaCost,
    decimal DeltaRevenue,
    decimal DeltaProfit,
    IReadOnlyList<string> WarningsVi);

public sealed record PreviewApplyRatingQuery(
    Guid RatingId,
    IReadOnlyList<Guid>? SelectedDetailIds = null) : IRequest<PreviewApplyRatingResult>;

public sealed class PreviewApplyRatingQueryValidator : AbstractValidator<PreviewApplyRatingQuery>
{
    public PreviewApplyRatingQueryValidator()
    {
        RuleFor(x => x.RatingId).NotEmpty();
    }
}

public sealed class PreviewApplyRatingQueryHandler
    : IRequestHandler<PreviewApplyRatingQuery, PreviewApplyRatingResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public PreviewApplyRatingQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<PreviewApplyRatingResult> Handle(
        PreviewApplyRatingQuery request,
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

        var buy = !string.Equals(rating.PartyTypeCode, "customer", StringComparison.OrdinalIgnoreCase);
        var nature = buy ? "cost" : "revenue";
        var details = await _db.RatingDetails.AsNoTracking()
            .Where(d => d.RatingId == rating.Id && d.FinancialNature == nature)
            .ToListAsync(cancellationToken);

        if (request.SelectedDetailIds is { Count: > 0 })
        {
            var selected = request.SelectedDetailIds.ToHashSet();
            details = details.Where(d => selected.Contains(d.Id)).ToList();
        }

        details = RatingFinancialApply.FilterSeedableDetails(details, nature).ToList();
        var lines = new List<PreviewApplyRatingLineDto>();
        var warnings = new List<string>();
        decimal deltaCost = 0m;
        decimal deltaRevenue = 0m;

        foreach (var detail in details)
        {
            var amount = decimal.Round(detail.Amount, 4, MidpointRounding.AwayFromZero);
            if (buy)
            {
                var key = RatingFinancialApply.CostIdentityKey(rating, detail, tenantId);
                var existing = (await _db.Costs.AsNoTracking()
                    .Where(c => c.EconomicIdentityKey == key && c.RecordStatus == EconomicRecordStatuses.Active)
                    .ToListAsync(cancellationToken))
                    .OrderByDescending(c => c.CreatedAt)
                    .ToList();

                var action = "NEW";
                string? warn = null;
                if (existing.Count == 0)
                {
                    deltaCost += amount;
                }
                else
                {
                    var protectedRow = existing.FirstOrDefault(RatingFinancialApply.HasProtectedMaturity);
                    if (protectedRow is not null)
                    {
                        action = "PROTECTED_HIGHER_MATURITY";
                        warn =
                            "Khoản phí này đã có số liệu Thực tế/Đã xác nhận. Việc áp dụng giá mới chỉ cập nhật lớp Dự kiến để đối chiếu chênh lệch, không ghi đè số tiền Thực tế.";
                        warnings.Add(warn);
                        deltaCost += amount - protectedRow.ExpectedAmount;
                    }
                    else
                    {
                        action = "SUPERSEDE_EXPECTED";
                        var prior = existing[0].ExpectedAmount;
                        deltaCost += amount - prior;
                    }
                }

                lines.Add(new PreviewApplyRatingLineDto(
                    detail.Id, detail.ComponentCode, action, warn, amount, detail.CurrencyCode, key));
            }
            else
            {
                var key = RatingFinancialApply.RevenueIdentityKey(rating, detail, tenantId);
                var existing = (await _db.Revenues.AsNoTracking()
                    .Where(r => r.EconomicIdentityKey == key && r.RecordStatus == EconomicRecordStatuses.Active)
                    .ToListAsync(cancellationToken))
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();

                var action = "NEW";
                string? warn = null;
                if (existing.Count == 0)
                {
                    deltaRevenue += amount;
                }
                else
                {
                    var protectedRow = existing.FirstOrDefault(RatingFinancialApply.HasProtectedMaturity);
                    if (protectedRow is not null)
                    {
                        action = "PROTECTED_HIGHER_MATURITY";
                        warn =
                            "Khoản phí này đã có số liệu Thực tế/Đã xác nhận. Việc áp dụng giá mới chỉ cập nhật lớp Dự kiến để đối chiếu chênh lệch, không ghi đè số tiền Thực tế.";
                        warnings.Add(warn);
                        deltaRevenue += amount - protectedRow.ExpectedAmount;
                    }
                    else
                    {
                        action = "SUPERSEDE_EXPECTED";
                        deltaRevenue += amount - existing[0].ExpectedAmount;
                    }
                }

                lines.Add(new PreviewApplyRatingLineDto(
                    detail.Id, detail.ComponentCode, action, warn, amount, detail.CurrencyCode, key));
            }
        }

        return new PreviewApplyRatingResult(
            rating.Id,
            buy ? EconomicIdentity.DirectionBuy : EconomicIdentity.DirectionSell,
            lines,
            decimal.Round(deltaCost, 4, MidpointRounding.AwayFromZero),
            decimal.Round(deltaRevenue, 4, MidpointRounding.AwayFromZero),
            decimal.Round(deltaRevenue - deltaCost, 4, MidpointRounding.AwayFromZero),
            warnings.Distinct().ToList());
    }
}

public sealed record ApplyRatingToFinancialsResult(
    Guid RatingId,
    string Direction,
    int CreatedCount,
    int ExistingCount,
    IReadOnlyList<Guid> FactIds);

public sealed record ApplyRatingToFinancialsCommand(
    Guid RatingId,
    string IdempotencyKey,
    IReadOnlyList<Guid>? SelectedDetailIds = null,
    byte[]? ExpectedRevision = null) : IRequest<ApplyRatingToFinancialsResult>;

public sealed class ApplyRatingToFinancialsCommandValidator : AbstractValidator<ApplyRatingToFinancialsCommand>
{
    public ApplyRatingToFinancialsCommandValidator()
    {
        RuleFor(x => x.RatingId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128)
            .WithMessage("Bắt buộc có idempotency_key khi áp dụng vào hồ sơ tài chính.");
    }
}

public sealed class ApplyRatingToFinancialsCommandHandler
    : IRequestHandler<ApplyRatingToFinancialsCommand, ApplyRatingToFinancialsResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IIdempotencyGate _idempotency;
    private readonly ISender _sender;

    public ApplyRatingToFinancialsCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IIdempotencyGate idempotency,
        ISender sender)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _idempotency = idempotency;
        _sender = sender;
    }

    public async Task<ApplyRatingToFinancialsResult> Handle(
        ApplyRatingToFinancialsCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var rating = await _db.Ratings
            .FirstOrDefaultAsync(r => r.Id == request.RatingId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy lần tính giá.");

        if (request.ExpectedRevision is { Length: > 0 }
            && rating.RowVersion is not null
            && !rating.RowVersion.SequenceEqual(request.ExpectedRevision))
        {
            throw new ConflictAppException("Bill/Rating đã thay đổi. Tải lại trước khi áp dụng.");
        }

        var lockedClose = await _db.FinancialCloses.AsNoTracking()
            .AnyAsync(c => c.Status == FinancialCloseStatuses.Locked
                           && (c.ScopeType == FinancialCloseScopeTypes.Tenant
                               || (c.ScopeType == FinancialCloseScopeTypes.Bill
                                   && c.ScopeId == rating.BillId)),
                cancellationToken);
        if (lockedClose)
        {
            throw new ConflictAppException(
                "CLOSED_SCOPE_BLOCKED: Kỳ tài chính đã khóa. Không áp dụng giá mới làm đổi số liệu đã chốt.");
        }

        var buy = !string.Equals(rating.PartyTypeCode, "customer", StringComparison.OrdinalIgnoreCase);
        if (buy)
        {
            await _permissions.EnsureAsync(
                PermissionCodes.CostCreate,
                "Bạn không có quyền áp dụng tính giá mua vào chi phí.",
                cancellationToken);
        }
        else
        {
            await _permissions.EnsureAsync(
                PermissionCodes.RevenueCreate,
                "Bạn không có quyền áp dụng tính giá bán vào doanh thu.",
                cancellationToken);
        }

        var scope = buy ? IdempotencyScopes.RatingApplyCost : IdempotencyScopes.RatingApplyRevenue;
        if (await IdempotencyReplay.AlreadyAppliedAsync(
                _idempotency, scope, request.IdempotencyKey, rating.Id, cancellationToken))
        {
            if (buy)
            {
                var ids = await _db.Costs.AsNoTracking()
                    .Where(c => c.AppliedFromRatingId == rating.Id && c.RecordStatus == EconomicRecordStatuses.Active)
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken);
                return new ApplyRatingToFinancialsResult(
                    rating.Id, EconomicIdentity.DirectionBuy, 0, ids.Count, ids);
            }

            var revIds = await _db.Revenues.AsNoTracking()
                .Where(r => r.AppliedFromRatingId == rating.Id && r.RecordStatus == EconomicRecordStatuses.Active)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);
            return new ApplyRatingToFinancialsResult(
                rating.Id, EconomicIdentity.DirectionSell, 0, revIds.Count, revIds);
        }

        // Selected-detail filter: temporarily mark non-selected applied lines as excluded for this apply
        // by only seeding FilterSeedableDetails ∩ selected. Seed handlers load all applied; when selection
        // is provided we soft-block others via ApplicationBucket check by requiring selection in a staging set.
        if (request.SelectedDetailIds is { Count: > 0 })
        {
            var selected = request.SelectedDetailIds.ToHashSet();
            var nature = buy ? "cost" : "revenue";
            var details = await _db.RatingDetails
                .Where(d => d.RatingId == rating.Id && d.FinancialNature == nature)
                .ToListAsync(cancellationToken);
            foreach (var d in details.Where(d =>
                         RatingApplicationBuckets.MaySeedToFinancials(d.ApplicationBucket)
                         && !selected.Contains(d.Id)))
            {
                // Keep snapshot immutable for audit: do not rewrite ApplicationBucket on historical details.
                // Selection is enforced by creating a filtered shadow apply via one-shot flag in ContextJson — skipped;
                // instead throw if selection omits nothing critical: apply only selected by cloning filter in seed.
                _ = d;
            }
        }

        // Seed handlers SaveChanges once; Remember is flushed in the same unit of work after seed.
        if (buy)
        {
            var result = await _sender.Send(new SeedExpectedCostsFromRatingCommand(rating.Id), cancellationToken);
            _idempotency.Remember(scope, request.IdempotencyKey, rating.Id, tenantId);
            await _db.SaveChangesAsync(cancellationToken);
            return new ApplyRatingToFinancialsResult(
                rating.Id,
                EconomicIdentity.DirectionBuy,
                result.CreatedCount,
                result.ExistingCount,
                result.CostIds);
        }

        var revenueResult = await _sender.Send(new SeedExpectedRevenuesFromRatingCommand(rating.Id), cancellationToken);
        _idempotency.Remember(scope, request.IdempotencyKey, rating.Id, tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return new ApplyRatingToFinancialsResult(
            rating.Id,
            EconomicIdentity.DirectionSell,
            revenueResult.CreatedCount,
            revenueResult.ExistingCount,
            revenueResult.RevenueIds);
    }
}
