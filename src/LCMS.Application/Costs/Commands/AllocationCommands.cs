using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Commands;

public sealed record AllocationDetailInput(Guid BillId, decimal BasisValue, decimal? ManualOverrideAmount, string? OverrideReason);

public sealed record CreateCostAllocationCommand(
    Guid CostId,
    string AllocationBasis,
    IReadOnlyList<AllocationDetailInput> Details) : IRequest<Guid>;

public sealed class CreateCostAllocationCommandValidator : AbstractValidator<CreateCostAllocationCommand>
{
    public CreateCostAllocationCommandValidator()
    {
        RuleFor(x => x.CostId).NotEmpty().WithMessage("Chi phí không hợp lệ.");
        RuleFor(x => x.AllocationBasis)
            .NotEmpty().WithMessage("Cơ sở phân bổ không được để trống.")
            .MaximumLength(64).WithMessage("Cơ sở phân bổ không được vượt quá 64 ký tự.");
        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("Phải có ít nhất một dòng phân bổ theo Bill.");
        RuleForEach(x => x.Details).ChildRules(d =>
        {
            d.RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill phân bổ không hợp lệ.");
            d.RuleFor(x => x.BasisValue)
                .GreaterThan(0).WithMessage("Giá trị cơ sở phân bổ phải lớn hơn 0.");
            d.RuleFor(x => x.OverrideReason)
                .NotEmpty().WithMessage("Phải nêu lý do khi ghi đè số phân bổ.")
                .When(x => x.ManualOverrideAmount.HasValue);
        });
    }
}

/// <summary>
/// Draft allocation on a shared Cost. Does not create new Costs (C-003).
/// </summary>
public sealed class CreateCostAllocationCommandHandler : IRequestHandler<CreateCostAllocationCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateCostAllocationCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateCostAllocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == request.CostId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi phí.");

        if (!string.Equals(cost.AttributionType, CostAttributionTypes.Shared, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ phân bổ chi phí chung (shared); chi phí trực tiếp đã gắn Bill.");
        }

        if (cost.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ phân bổ chi phí đang hiệu lực.");
        }

        var billIds = request.Details.Select(d => d.BillId).Distinct().ToList();
        if (billIds.Count != request.Details.Count)
        {
            throw new ConflictAppException("Mỗi Bill chỉ được xuất hiện một lần trong phiên phân bổ.");
        }

        var bills = await _db.Bills.AsNoTracking()
            .Where(b => billIds.Contains(b.Id))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);
        if (bills.Count != billIds.Count)
        {
            throw new NotFoundAppException("Một hoặc nhiều Bill phân bổ không tồn tại.");
        }

        var maxVersion = await _db.CostAllocations
            .Where(a => a.CostId == cost.Id)
            .Select(a => (int?)a.VersionNo)
            .MaxAsync(cancellationToken) ?? 0;

        var allocation = new CostAllocation
        {
            TenantId = tenantId,
            CostId = cost.Id,
            VersionNo = maxVersion + 1,
            AllocationBasis = request.AllocationBasis.Trim().ToLowerInvariant(),
            ApplicabilityMode = "explicit",
            AllocatableAmount = cost.Amount,
            AllocatedAmount = 0m,
            AllocationStatus = CostAllocationStatuses.Draft
        };

        _db.CostAllocations.Add(allocation);
        await _db.SaveChangesAsync(cancellationToken);

        var details = request.Details.Select(d => new CostAllocationDetail
        {
            TenantId = tenantId,
            AllocationId = allocation.Id,
            BillId = d.BillId,
            BasisValue = decimal.Round(d.BasisValue, 6, MidpointRounding.AwayFromZero),
            BasisRatio = 0m,
            AllocatedAmount = 0m,
            RoundingAdjustment = 0m,
            ManualOverrideAmount = d.ManualOverrideAmount.HasValue
                ? decimal.Round(d.ManualOverrideAmount.Value, 4, MidpointRounding.AwayFromZero)
                : null,
            OverrideReason = string.IsNullOrWhiteSpace(d.OverrideReason) ? null : d.OverrideReason.Trim()
        }).ToList();

        _db.CostAllocationDetails.AddRange(details);
        await _db.SaveChangesAsync(cancellationToken);
        return allocation.Id;
    }
}

public sealed record FinalizeCostAllocationCommand(Guid AllocationId) : IRequest;

public sealed class FinalizeCostAllocationCommandValidator : AbstractValidator<FinalizeCostAllocationCommand>
{
    public FinalizeCostAllocationCommandValidator()
    {
        RuleFor(x => x.AllocationId).NotEmpty().WithMessage("Phiên phân bổ không hợp lệ.");
    }
}

/// <summary>
/// Finalize draft allocation: C-006 basis check + C-005 conservation after rounding.
/// Does not create new Cost rows (C-003 / Single Economic Cost).
/// </summary>
public sealed class FinalizeCostAllocationCommandHandler : IRequestHandler<FinalizeCostAllocationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;

    public FinalizeCostAllocationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
    }

    public async Task Handle(FinalizeCostAllocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var allocation = await _db.CostAllocations
            .FirstOrDefaultAsync(a => a.Id == request.AllocationId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên phân bổ.");

        if (!string.Equals(allocation.AllocationStatus, CostAllocationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ được chốt phiên phân bổ ở trạng thái nháp.");
        }

        var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == allocation.CostId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi phí.");

        // Refresh allocatable from current Cost amount (Single Economic Cost).
        allocation.AllocatableAmount = cost.Amount;

        var details = await _db.CostAllocationDetails
            .Where(d => d.AllocationId == allocation.Id)
            .OrderBy(d => d.BillId)
            .ToListAsync(cancellationToken);

        if (details.Count == 0)
        {
            throw new ConflictAppException("Không thể chốt phân bổ khi chưa có dòng chi tiết.");
        }

        // C-006: every basis must be present and > 0; total basis ≠ 0.
        if (details.Any(d => d.BasisValue <= 0))
        {
            throw new ConflictAppException("Không thể chốt phân bổ: cơ sở phân bổ thiếu hoặc bằng 0.");
        }

        var totalBasis = details.Sum(d => d.BasisValue);
        if (totalBasis <= 0)
        {
            throw new ConflictAppException("Không thể chốt phân bổ: tổng cơ sở phân bổ bằng 0.");
        }

        var allocatable = allocation.AllocatableAmount;
        decimal allocatedSum = 0m;

        for (var i = 0; i < details.Count; i++)
        {
            var detail = details[i];
            detail.BasisRatio = decimal.Round(detail.BasisValue / totalBasis, 8, MidpointRounding.AwayFromZero);

            decimal raw;
            if (detail.ManualOverrideAmount.HasValue)
            {
                raw = detail.ManualOverrideAmount.Value;
            }
            else if (i == details.Count - 1)
            {
                // Last line absorbs residual so SUM = allocatable (C-005).
                raw = allocatable - allocatedSum;
            }
            else
            {
                raw = decimal.Round(allocatable * detail.BasisRatio, 4, MidpointRounding.AwayFromZero);
            }

            var rounded = decimal.Round(raw, 4, MidpointRounding.AwayFromZero);
            var withoutOverride = i == details.Count - 1 && !detail.ManualOverrideAmount.HasValue
                ? rounded
                : decimal.Round(allocatable * detail.BasisRatio, 4, MidpointRounding.AwayFromZero);

            detail.AllocatedAmount = rounded;
            detail.RoundingAdjustment = decimal.Round(rounded - withoutOverride, 4, MidpointRounding.AwayFromZero);
            allocatedSum += rounded;
        }

        // If manual overrides break conservation, reject (C-005).
        if (allocatedSum != allocatable)
        {
            // Recompute last non-override residual when possible
            var overrideSum = details.Where(d => d.ManualOverrideAmount.HasValue).Sum(d => d.AllocatedAmount);
            var free = details.Where(d => !d.ManualOverrideAmount.HasValue).ToList();
            if (free.Count == 0 || overrideSum > allocatable)
            {
                throw new ConflictAppException(
                    "Không thể chốt phân bổ: tổng phân bổ không khớp số tiền cần phân bổ (conservation).");
            }

            var freeBasis = free.Sum(d => d.BasisValue);
            var remaining = allocatable - overrideSum;
            decimal freeAllocated = 0m;
            for (var i = 0; i < free.Count; i++)
            {
                var detail = free[i];
                detail.BasisRatio = decimal.Round(detail.BasisValue / totalBasis, 8, MidpointRounding.AwayFromZero);
                decimal raw;
                if (i == free.Count - 1)
                {
                    raw = remaining - freeAllocated;
                }
                else
                {
                    raw = decimal.Round(remaining * (detail.BasisValue / freeBasis), 4, MidpointRounding.AwayFromZero);
                }

                var proportional = decimal.Round(allocatable * detail.BasisRatio, 4, MidpointRounding.AwayFromZero);
                detail.AllocatedAmount = decimal.Round(raw, 4, MidpointRounding.AwayFromZero);
                detail.RoundingAdjustment = decimal.Round(detail.AllocatedAmount - proportional, 4, MidpointRounding.AwayFromZero);
                freeAllocated += detail.AllocatedAmount;
            }

            allocatedSum = details.Sum(d => d.AllocatedAmount);
            if (allocatedSum != allocatable)
            {
                throw new ConflictAppException(
                    "Không thể chốt phân bổ: tổng phân bổ không khớp số tiền cần phân bổ (conservation).");
            }
        }

        // Supersede any prior finalized allocation for this cost (history preserved).
        var priorFinal = await _db.CostAllocations
            .Where(a => a.CostId == cost.Id
                        && a.Id != allocation.Id
                        && a.AllocationStatus == CostAllocationStatuses.Finalized)
            .ToListAsync(cancellationToken);
        foreach (var prior in priorFinal)
        {
            prior.AllocationStatus = CostAllocationStatuses.Superseded;
        }

        allocation.AllocatedAmount = allocatedSum;
        allocation.AllocationStatus = CostAllocationStatuses.Finalized;
        allocation.FinalizedAt = DateTimeOffset.UtcNow;
        allocation.FinalizedBy = _user.UserId;
        if (priorFinal.Count > 0)
        {
            allocation.SupersedesAllocationId = priorFinal.OrderByDescending(a => a.VersionNo).First().Id;
        }

        // Assert Single Economic Cost: cost row count for this id stays 1 (no new Cost created).
        await _db.SaveChangesAsync(cancellationToken);
    }
}
