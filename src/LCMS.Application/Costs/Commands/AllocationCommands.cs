using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Costs.Commands;

public sealed record AllocationDetailInput(Guid BillId, decimal? BasisValue, decimal? ManualOverrideAmount, string? OverrideReason);

public sealed record CreateCostAllocationCommand(
    Guid CostId,
    string AllocationBasis,
    IReadOnlyList<AllocationDetailInput> Details,
    string? ApplicabilityMode = null,
    Guid? ScopeId = null,
    string? ConditionCode = null,
    string? IdempotencyKey = null) : IRequest<Guid>;

public sealed class CreateCostAllocationCommandValidator : AbstractValidator<CreateCostAllocationCommand>
{
    public CreateCostAllocationCommandValidator()
    {
        RuleFor(x => x.CostId).NotEmpty().WithMessage("Chi phí không hợp lệ.");
        RuleFor(x => x.AllocationBasis)
            .NotEmpty().WithMessage("Cơ sở phân bổ không được để trống.")
            .Must(b => CostAllocationBases.IsSupported(b.Trim().ToLowerInvariant()))
            .WithMessage("Cơ sở phân bổ không hợp lệ.");
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
                return b is CostAllocationBases.Quantity or CostAllocationBases.ManualRatio
                    or CostAllocationBases.ManualPercent or CostAllocationBases.ManualAmount;
            });
        RuleForEach(x => x.Details)
            .Must(d => d.BasisValue is > 0 || d.BasisValue is null)
            .WithMessage("Giá trị cơ sở phân bổ phải lớn hơn 0 khi khai báo.")
            .When(x => x.AllocationBasis.Trim().ToLowerInvariant() == CostAllocationBases.Equal);
        RuleFor(x => x)
            .Must(x =>
            {
                var b = x.AllocationBasis.Trim().ToLowerInvariant();
                if (b is not (CostAllocationBases.Quantity or CostAllocationBases.ManualRatio
                    or CostAllocationBases.ManualPercent or CostAllocationBases.ManualAmount))
                {
                    return true;
                }

                return x.Details.All(d => d.BasisValue is > 0);
            })
            .WithMessage("Cơ sở thủ công bắt buộc giá trị cơ sở > 0 trên mọi dòng.");
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
    private readonly IPermissionService _permissions;
    private readonly IIdempotencyGate _idempotency;

    public CreateCostAllocationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IIdempotencyGate idempotency)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _idempotency = idempotency;
    }

    public async Task<Guid> Handle(CreateCostAllocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var priorId = await _idempotency.FindAsync(
            IdempotencyScopes.CostAllocation,
            request.IdempotencyKey,
            cancellationToken);
        if (priorId.HasValue)
        {
            return priorId.Value;
        }

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
            throw new ConflictAppException("Cơ sở phân bổ không hợp lệ.");
        }

        if (request.Details.Any(d => d.ManualOverrideAmount.HasValue))
        {
            await _permissions.EnsureAsync(
                PermissionCodes.CostAllocationOverride,
                "Bạn không có quyền sửa kết quả phân bổ tự động.",
                cancellationToken);
        }

        var open = await _db.CostAllocations.AnyAsync(
            a => a.CostId == cost.Id && (a.AllocationStatus == CostAllocationStatuses.Draft
                || a.AllocationStatus == CostAllocationStatuses.Calculated
                || a.AllocationStatus == CostAllocationStatuses.PendingApproval),
            cancellationToken);
        if (open)
        {
            throw new ConflictAppException("Đã có phiên phân bổ chưa chốt.");
        }

        var mode = string.IsNullOrWhiteSpace(request.ApplicabilityMode)
            ? "explicit"
            : request.ApplicabilityMode.Trim().ToLowerInvariant();
        var detailInputs = await ResolveTargetsAsync(request, mode, cancellationToken);

        if (detailInputs.Count < 2)
        {
            throw new ConflictAppException("Chi phí chung phải phân bổ cho ít nhất 2 Bill.");
        }

        var billIds = detailInputs.Select(d => d.BillId).Distinct().ToList();
        if (billIds.Count != detailInputs.Count)
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
            ApplicabilityMode = mode,
            ScopeId = request.ScopeId,
            ConditionCode = string.IsNullOrWhiteSpace(request.ConditionCode) ? null : request.ConditionCode.Trim(),
            AllocatableAmount = cost.Amount,
            AllocatedAmount = 0m,
            AllocationStatus = CostAllocationStatuses.Draft
        };

        var measures = CostAllocationBases.FromMeasurement.Contains(basis)
            ? await _db.OperationalMeasurements.AsNoTracking()
                .Where(m => m.ObjectType == OperationalObjectTypes.Bill && billIds.Contains(m.ObjectId) && m.MeasureCode == MeasureCode(basis))
                .ToListAsync(cancellationToken)
            : [];

        var details = detailInputs.Select(d =>
        {
            var input = CostAllocationBases.FromMeasurement.Contains(basis)
                ? measures.FirstOrDefault(m => m.ObjectId == d.BillId)?.Quantity
                : d.BasisValue;
            var basisValue = ResolveBasisValue(basis, input);
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

        if (details.Sum(d => d.BasisValue) <= 0)
        {
            throw new ConflictAppException("ZERO_ALLOCATION_BASIS: Tổng cơ sở phân bổ bằng 0. Không chia đều.");
        }

        _db.CostAllocations.Add(allocation);
        _db.CostAllocationDetails.AddRange(details);
        _idempotency.Remember(
            IdempotencyScopes.CostAllocation,
            request.IdempotencyKey ?? string.Empty,
            allocation.Id,
            tenantId);
        await _db.SaveChangesAsync(cancellationToken);
        return allocation.Id;
    }

    internal static decimal ResolveBasisValue(string basis, decimal? input)
    {
        if (basis == CostAllocationBases.Equal)
        {
            return 1m;
        }

        if (CostAllocationBases.FromMeasurement.Contains(basis))
        {
            return input is > 0 ? decimal.Round(input.Value, 6, MidpointRounding.AwayFromZero) : 0m;
        }

        if (input is null or <= 0)
        {
            throw new ConflictAppException("Giá trị cơ sở phân bổ phải lớn hơn 0.");
        }

        return decimal.Round(input.Value, 6, MidpointRounding.AwayFromZero);
    }

    private async Task<IReadOnlyList<AllocationDetailInput>> ResolveTargetsAsync(
        CreateCostAllocationCommand request,
        string mode,
        CancellationToken cancellationToken)
    {
        var details = request.Details?.ToList() ?? [];
        if (mode is "leg" or "movement")
        {
            if (request.ScopeId is null)
            {
                throw new ConflictAppException(mode == "leg"
                    ? "Chọn chặng trước khi phân bổ theo chặng."
                    : "Chọn chuyến trước khi phân bổ theo chuyến.");
            }

            var linked = mode == "leg"
                ? await _db.BillLegLinks.AsNoTracking()
                    .Where(l => l.TransportLegId == request.ScopeId)
                    .Select(l => l.BillId)
                    .ToListAsync(cancellationToken)
                : await _db.BillMovementLinks.AsNoTracking()
                    .Where(l => l.TransportMovementId == request.ScopeId)
                    .Select(l => l.BillId)
                    .ToListAsync(cancellationToken);
            var allowed = linked.ToHashSet();
            if (details.Count == 0)
            {
                return allowed.Select(id => new AllocationDetailInput(id, null, null, null)).ToList();
            }

            if (details.Any(d => !allowed.Contains(d.BillId)))
            {
                throw new ConflictAppException(mode == "leg"
                    ? "Bill không thuộc chặng được chọn. Không gán mọi Bill liên kết khác."
                    : "Bill không thuộc chuyến được chọn. Không gán mọi Bill liên kết khác.");
            }

            return details;
        }

        if (mode == "condition")
        {
            if (string.IsNullOrWhiteSpace(request.ConditionCode))
            {
                throw new ConflictAppException("Chọn điều kiện loại dịch vụ trước khi phân bổ.");
            }

            var code = request.ConditionCode.Trim();
            var ids = details.Select(d => d.BillId).ToList();
            var matched = await _db.Bills.AsNoTracking()
                .Where(b => ids.Contains(b.Id) && b.ServiceTypeCode == code)
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);
            if (matched.Count != details.Count)
            {
                throw new ConflictAppException("Có Bill không thỏa điều kiện áp dụng. Không gán Bill ngoài điều kiện.");
            }
        }
        else if (mode != "explicit")
        {
            throw new ConflictAppException("Phạm vi phân bổ không hợp lệ.");
        }

        return details;
    }

    private static string MeasureCode(string basis) => basis switch
    {
        CostAllocationBases.GrossKg => MeasureCodes.GrossWeightKg,
        CostAllocationBases.Chargeable => MeasureCodes.ChargeableWeightKg,
        CostAllocationBases.Cbm => MeasureCodes.VolumeCbm,
        CostAllocationBases.PackageCount => MeasureCodes.PackageCount,
        CostAllocationBases.Teu => MeasureCodes.Teu,
        _ => basis
    };
}

public sealed record FinalizeCostAllocationCommand(Guid AllocationId, string? IfMatch = null) : IRequest;

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
    private readonly ICostApprovalGate _approvalGate;
    private readonly IAuditWriter _audit;
    private readonly IRowVersionGuard _versions;

    public FinalizeCostAllocationCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        ICostApprovalGate approvalGate,
        IAuditWriter audit,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _approvalGate = approvalGate;
        _audit = audit;
        _versions = versions;
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
        _versions.EnsureCurrent(allocation, request.IfMatch);

        if (string.Equals(allocation.AllocationStatus, CostAllocationStatuses.Finalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(allocation.AllocationStatus, CostAllocationStatuses.Superseded, StringComparison.OrdinalIgnoreCase)
            || string.Equals(allocation.AllocationStatus, CostAllocationStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Phiên phân bổ đã chốt hoặc đã hủy, không sửa.");
        }

        // W-L1 / PC-21: người tạo phiên không tự chốt khi đăng nhập với user (SoD).
        if (_user.HasUser
            && allocation.CreatedBy.HasValue
            && allocation.CreatedBy.Value == _user.UserId)
        {
            throw new ConflictAppException("PC-21: Người tạo không được tự chốt phân bổ.");
        }

        if (!CostAllocationBases.IsSupported(allocation.AllocationBasis))
        {
            throw new ConflictAppException("Cơ sở phân bổ không hợp lệ.");
        }

        var cost = await _db.Costs.FirstOrDefaultAsync(c => c.Id == allocation.CostId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy chi phí.");

        if (!string.Equals(cost.AttributionType, CostAttributionTypes.Shared, StringComparison.OrdinalIgnoreCase)
            || cost.BillId.HasValue)
        {
            throw new ConflictAppException("Chỉ chốt phân bổ cho chi phí chung (shared) không gắn Bill.");
        }

        try
        {
            await _approvalGate.EnsureConfirmAllowedAsync(cost, cancellationToken);
        }
        catch (ConflictAppException)
        {
            _audit.Append(
                AuditActions.CostAllocationFinalizeBlocked,
                AuditObjectTypes.CostAllocation,
                allocation.Id,
                reason: "Chi phí vượt ngưỡng phê duyệt; cần phê duyệt trước khi chốt phân bổ.");
            await _db.SaveChangesAsync(cancellationToken);
            throw new ConflictAppException(
                "Chi phí vượt ngưỡng phê duyệt; cần phê duyệt trước khi chốt phân bổ.");
        }

        if (string.Equals(allocation.AllocationStatus, CostAllocationStatuses.PendingApproval, StringComparison.OrdinalIgnoreCase))
        {
            var approved = await _db.Approvals.AsNoTracking().AnyAsync(
                a => a.ObjectType == ApprovalObjectTypes.CostAllocation
                     && a.ObjectId == allocation.Id
                     && a.Status == ApprovalStatuses.Approved,
                cancellationToken);
            if (!approved)
            {
                throw new ConflictAppException(
                    "Phiên đã gửi duyệt; cần phê duyệt trước khi chốt phân bổ.");
            }
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

        AllocationMath.Apply(allocation.AllocationBasis, allocation.AllocatableAmount, details);
        var allocatedSum = details.Sum(d => d.AllocatedAmount);
        if (decimal.Round(allocatedSum, 4, MidpointRounding.AwayFromZero)
            != decimal.Round(allocation.AllocatableAmount, 4, MidpointRounding.AwayFromZero))
        {
            throw new ConflictAppException(
                "Không thể chốt phân bổ: tổng phân bổ không khớp số tiền cần phân bổ (conservation).");
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

public sealed record CalculateCostAllocationCommand(Guid AllocationId) : IRequest;

public sealed class CalculateCostAllocationCommandHandler : IRequestHandler<CalculateCostAllocationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public CalculateCostAllocationCommandHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task Handle(CalculateCostAllocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var allocation = await _db.CostAllocations.FirstOrDefaultAsync(a => a.Id == request.AllocationId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên phân bổ.");
        if (!string.Equals(allocation.AllocationStatus, CostAllocationStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ tính phiên phân bổ đang nháp.");
        }

        var cost = await _db.Costs.FirstAsync(c => c.Id == allocation.CostId, cancellationToken);
        allocation.AllocatableAmount = cost.Amount;
        var details = await _db.CostAllocationDetails.Where(d => d.AllocationId == allocation.Id).ToListAsync(cancellationToken);
        AllocationMath.Apply(allocation.AllocationBasis, allocation.AllocatableAmount, details);
        allocation.AllocatedAmount = details.Sum(d => d.AllocatedAmount);
        allocation.AllocationStatus = CostAllocationStatuses.Calculated;
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record SubmitCostAllocationCommand(Guid AllocationId) : IRequest;

public sealed class SubmitCostAllocationCommandHandler : IRequestHandler<SubmitCostAllocationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _user;

    public SubmitCostAllocationCommandHandler(ILcmsDbContext db, ITenantContext tenant, ICurrentUserContext user)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
    }

    public async Task Handle(SubmitCostAllocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var allocation = await _db.CostAllocations.FirstOrDefaultAsync(a => a.Id == request.AllocationId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên phân bổ.");
        if (!string.Equals(allocation.AllocationStatus, CostAllocationStatuses.Calculated, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ gửi duyệt phiên đã tính.");
        }

        allocation.AllocationStatus = CostAllocationStatuses.PendingApproval;

        var pending = await _db.Approvals.AnyAsync(
            a => a.ObjectType == ApprovalObjectTypes.CostAllocation
                 && a.ObjectId == allocation.Id
                 && a.Status == ApprovalStatuses.Pending,
            cancellationToken);
        if (!pending)
        {
            _db.Approvals.Add(new Approval
            {
                TenantId = _tenant.TenantId!.Value,
                ObjectType = ApprovalObjectTypes.CostAllocation,
                ObjectId = allocation.Id,
                Status = ApprovalStatuses.Pending,
                RequiredLevel = 1,
                CurrentLevel = 0,
                RequestedBy = _user.UserId,
                RequestedAt = DateTimeOffset.UtcNow,
                RequestReason = "Chốt phân bổ chi phí chung.",
                ObjectFingerprint = allocation.AllocatableAmount.ToString("0.####")
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CancelCostAllocationCommand(Guid AllocationId) : IRequest;

public sealed class CancelCostAllocationCommandHandler : IRequestHandler<CancelCostAllocationCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public CancelCostAllocationCommandHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task Handle(CancelCostAllocationCommand request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var allocation = await _db.CostAllocations.FirstOrDefaultAsync(a => a.Id == request.AllocationId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên phân bổ.");
        if (!CostAllocationStatuses.IsOpen(allocation.AllocationStatus))
        {
            throw new ConflictAppException("Không hủy phiên đã chốt.");
        }

        allocation.AllocationStatus = CostAllocationStatuses.Cancelled;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
