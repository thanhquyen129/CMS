using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;

namespace LCMS.Application.Costs;

/// <summary>
/// Splits one shared amount across bills. The last bill id absorbs the rounding remainder so the lines sum to the original.
/// </summary>
public static class AllocationMath
{
    public static void Apply(string basis, decimal allocatable, IList<CostAllocationDetail> details)
    {
        if (string.Equals(basis, CostAllocationBases.Equal, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var detail in details)
            {
                detail.BasisValue = 1m;
            }
        }

        if (string.Equals(basis, CostAllocationBases.ManualPercent, StringComparison.OrdinalIgnoreCase))
        {
            var percent = details.Sum(d => d.BasisValue);
            if (Math.Abs(percent - 100m) > 0.01m)
            {
                throw new ConflictAppException("Phần trăm phân bổ phải cộng đủ 100.");
            }
        }

        if (string.Equals(basis, CostAllocationBases.ManualAmount, StringComparison.OrdinalIgnoreCase))
        {
            var sum = decimal.Round(details.Sum(d => d.BasisValue), 4, MidpointRounding.AwayFromZero);
            var target = decimal.Round(allocatable, 4, MidpointRounding.AwayFromZero);
            if (sum != target)
            {
                throw new ConflictAppException("Tổng số tiền phân bổ tay không khớp số cần phân bổ.");
            }

            foreach (var detail in details.OrderBy(d => d.BillId))
            {
                detail.BasisRatio = allocatable == 0 ? 0 : decimal.Round(detail.BasisValue / allocatable, 8, MidpointRounding.AwayFromZero);
                detail.AllocatedAmount = decimal.Round(detail.BasisValue, 4, MidpointRounding.AwayFromZero);
                detail.RoundingAdjustment = 0m;
            }

            return;
        }

        var totalBasis = details.Sum(d => d.BasisValue);
        if (totalBasis <= 0)
        {
            throw new ConflictAppException("ZERO_ALLOCATION_BASIS: Tổng cơ sở phân bổ bằng 0. Không chia đều.");
        }

        var ordered = details.OrderBy(d => d.BillId).ToList();
        var positive = ordered.Where(d => d.BasisValue > 0).ToList();
        decimal allocated = 0m;
        for (var i = 0; i < ordered.Count; i++)
        {
            var detail = ordered[i];
            if (detail.BasisValue <= 0)
            {
                detail.BasisRatio = 0m;
                detail.AllocatedAmount = 0m;
                detail.RoundingAdjustment = 0m;
                continue;
            }

            detail.BasisRatio = decimal.Round(detail.BasisValue / totalBasis, 8, MidpointRounding.AwayFromZero);
            var proportional = decimal.Round(allocatable * detail.BasisRatio, 4, MidpointRounding.AwayFromZero);
            var isLastPositive = ReferenceEquals(detail, positive[^1]);
            var amount = detail.ManualOverrideAmount.HasValue
                ? decimal.Round(detail.ManualOverrideAmount.Value, 4, MidpointRounding.AwayFromZero)
                : isLastPositive
                    ? decimal.Round(allocatable - allocated, 4, MidpointRounding.AwayFromZero)
                    : proportional;
            if (detail.ManualOverrideAmount.HasValue)
            {
                detail.OverrideBeforeAmount = proportional;
            }

            detail.AllocatedAmount = amount;
            detail.RoundingAdjustment = decimal.Round(amount - proportional, 4, MidpointRounding.AwayFromZero);
            if (!detail.ManualOverrideAmount.HasValue)
            {
                allocated += amount;
            }
        }

        var withOverride = ordered.Where(d => d.ManualOverrideAmount.HasValue).ToList();
        if (withOverride.Count == 0)
        {
            return;
        }

        var overrideSum = withOverride.Sum(d => d.AllocatedAmount);
        var free = ordered.Where(d => !d.ManualOverrideAmount.HasValue && d.BasisValue > 0).ToList();
        if (free.Count == 0)
        {
            if (decimal.Round(overrideSum, 4, MidpointRounding.AwayFromZero) != decimal.Round(allocatable, 4, MidpointRounding.AwayFromZero))
            {
                throw new ConflictAppException("Không thể chốt phân bổ: tổng phân bổ không khớp số tiền cần phân bổ (conservation).");
            }

            return;
        }

        if (overrideSum > allocatable)
        {
            throw new ConflictAppException("Không thể chốt phân bổ: tổng phân bổ không khớp số tiền cần phân bổ (conservation).");
        }

        var remaining = allocatable - overrideSum;
        var freeBasis = free.Sum(d => d.BasisValue);
        decimal freeAllocated = 0m;
        for (var i = 0; i < free.Count; i++)
        {
            var detail = free[i];
            var proportional = decimal.Round(allocatable * detail.BasisRatio, 4, MidpointRounding.AwayFromZero);
            var raw = i == free.Count - 1
                ? remaining - freeAllocated
                : decimal.Round(remaining * (detail.BasisValue / freeBasis), 4, MidpointRounding.AwayFromZero);
            detail.AllocatedAmount = decimal.Round(raw, 4, MidpointRounding.AwayFromZero);
            detail.RoundingAdjustment = decimal.Round(detail.AllocatedAmount - proportional, 4, MidpointRounding.AwayFromZero);
            freeAllocated += detail.AllocatedAmount;
        }
    }
}
