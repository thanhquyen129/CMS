using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Commands;

public sealed record AllocationDetailInput(Guid BillId, decimal? BasisValue, decimal? ManualOverrideAmount, string? OverrideReason);

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
            .Must(b => CostAllocationBases.IsSupported(b.Trim().ToLowerInvariant()))
            .WithMessage("Cơ sở phân bổ phải là equal, quantity hoặc manual_ratio.");
        RuleFor(x => x.Details)
            .Must(d => d is { Count: >= 2 })
            .WithMessage("Chi phí chung phải phân bổ cho ít nhất 2 Bill.");
        RuleForEach(x => x.Details).ChildRules(d =>
        {
            d.RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill phân bổ không hợp lệ.");
            d.RuleFor(x => x.OverrideReason)
                .NotEmpty().WithMessage("Phải nêu lý do khi ghi đè số phân bổ.")
                .When(x => x.ManualOverrideAmount.HasValue);
        });
        // C-006: quantity / manual_ratio require basis > 0; equal may omit (normalized to 1).
        RuleForEach(x => x.Details)
            .Must(d => d.BasisValue is null || d.BasisValue > 0)
            .WithMessage("Giá trị cơ sở phân bổ phải lớn hơn 0.")
            .When(x =>
            {
                var b = x.AllocationBasis.Trim().ToLowerInvariant();
                return b is CostAllocationBases.Quantity or CostAllocationBases.ManualRatio;
            });
        RuleForEach(x => x.Details)
            .Must(d => d.BasisValue is > 0 || d.BasisValue is null)
            .WithMessage("Giá trị cơ sở phân bổ phải lớn hơn 0 khi khai báo.")
            .When(x => x.AllocationBasis.Trim().ToLowerInvariant() == CostAllocationBases.Equal);
        RuleFor(x => x)
            .Must(x =>
            {
                var b = x.AllocationBasis.Trim().ToLowerInvariant();
                if (b is not (CostAllocationBases.Quantity or CostAllocationBases.ManualRatio))
                {
                    return true;
                }

                return x.Details.All(d => d.BasisValue is > 0);
            })
            .WithMessage("Cơ sở quantity/manual_ratio bắt buộc giá trị cơ sở > 0 trên mọi dòng.");
    }
}

/// <summary>
/// Draft allocation on a shared Cost. Does not create new Costs (C-003).
/// Bases: equal | quantity | manual_ratio (C-006).
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

        if (cost.BillId.HasValue)
        {
            throw new ConflictAppException("Chi phí chung không được gắn Bill trực tiếp; dùng phân bổ.");
        }

        if (cost.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ phân bổ chi phí đang hiệu lực.");
        }

        var basis = request.AllocationBasis.Trim().ToLowerInvariant();
        if (!CostAllocationBases.IsSupported(basis))
        {
            throw new ConflictAppException("Cơ sở phân bổ phải là equal, quantity hoặc manual_ratio.");
        }

        if (request.Details.Count < 2)
        {
            throw new ConflictAppException("Chi phí chung phải phân bổ cho ít nhất 2 Bill.");
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
            AllocationBasis = basis,
            ApplicabilityMode = "explicit",
            AllocatableAmount = cost.Amount,
            AllocatedAmount = 0m,
            AllocationStatus = CostAllocationStatuses.Draft
        };

        _db.CostAllocations.Add(allocation);
        await _db.SaveChangesAsync(cancellationToken);

        var details = request.Details.Select(d =>
        {
            var basisValue = ResolveBasisValue(basis, d.BasisValue);
            return new CostAllocationDetail
            {
                TenantId = tenantId,
                AllocationId = allocation.Id,
                BillId = d.BillId,
                BasisValue = basisValue,
                BasisRatio = 0m,
                AllocatedAmount = 0m,
                RoundingAdjustment = 0m,
                ManualOverrideAmount = d.ManualOverrideAmount.HasValue
                    ? decimal.Round(d.ManualOverrideAmount.Value, 4, MidpointRounding.AwayFromZero)
                    : null,
                OverrideReason = string.IsNullOrWhiteSpace(d.OverrideReason) ? null : d.OverrideReason.Trim()
            };
        }).ToList();

        // C-006 gate early: total basis must be > 0
        if (details.Sum(d => d.BasisValue) <= 0)
        {
            throw new ConflictAppException("Không thể tạo phân bổ: tổng cơ sở phân bổ bằng 0.");
        }

        _db.CostAllocationDetails.AddRange(details);
        await _db.SaveChangesAsync(cancellationToken);
        return allocation.Id;
    }

    internal static decimal ResolveBasisValue(string basis, decimal? input)
    {
        if (basis == CostAllocationBases.Equal)
        {
            // Equal share: force weight 1 per Bill (ignore client weights).
            return 1m;
        }

        if (input is null or <= 0)
        {
            throw new ConflictAppException("Giá trị cơ sở phân bổ phải lớn hơn 0.");
        }

        return decimal.Round(input.Value, 6, MidpointRounding.AwayFromZero);
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
/// Reallocation supersedes prior finalized (history preserved). Does not create Cost rows (C-003).
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

        if (!CostAllocationBases.IsSupported(allocation.AllocationBasis))
        {
            throw new ConflictAppException("Cơ sở phân bổ phải là equal, quantity hoặc manual_ratio.");
        }

        var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == allocation.CostId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi phí.");

        if (!string.Equals(cost.AttributionType, CostAttributionTypes.Shared, StringComparison.OrdinalIgnoreCase)
            || cost.BillId.HasValue)
        {
            throw new ConflictAppException("Chỉ chốt phân bổ cho chi phí chung (shared) không gắn Bill.");
        }

        // Refresh allocatable from current Cost amount (Single Economic Cost).
        allocation.AllocatableAmount = cost.Amount;

        var details = await _db.CostAllocationDetails
            .Where(d => d.AllocationId == allocation.Id)
            .OrderBy(d => d.BillId)
            .ToListAsync(cancellationToken);

        if (details.Count < 2)
        {
            throw new ConflictAppException("Không thể chốt phân bổ: chi phí chung cần ít nhất 2 Bill.");
        }

        if (string.Equals(allocation.AllocationBasis, CostAllocationBases.Equal, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var d in details)
            {
                d.BasisValue = 1m;
            }
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

        // Reallocation: supersede any prior finalized allocation (history preserved, no silent edit).
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

        await _db.SaveChangesAsync(cancellationToken);
    }
}
