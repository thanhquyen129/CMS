using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Application.Costs.Commands;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Commands;

public sealed record RevenueMappingLineInput(
    Guid BillId,
    decimal? BasisValue,
    decimal? ManualOverrideAmount,
    string? OverrideReason);

public sealed record RevenueMappingCreated(Guid Id, byte[] RowVersion);

public sealed record CreateRevenueMappingCommand(
    Guid RevenueId,
    string AllocationBasis,
    IReadOnlyList<RevenueMappingLineInput> Details,
    string? ApplicabilityMode = null,
    Guid? ScopeId = null,
    string? ConditionCode = null) : IRequest<RevenueMappingCreated>;

public sealed class CreateRevenueMappingCommandValidator : AbstractValidator<CreateRevenueMappingCommand>
{
    public CreateRevenueMappingCommandValidator()
    {
        RuleFor(x => x.RevenueId).NotEmpty();
        RuleFor(x => x.AllocationBasis).NotEmpty();
    }
}

public sealed class CreateRevenueMappingCommandHandler : IRequestHandler<CreateRevenueMappingCommand, RevenueMappingCreated>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public CreateRevenueMappingCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<RevenueMappingCreated> Handle(CreateRevenueMappingCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var basis = request.AllocationBasis.Trim().ToLowerInvariant();
        if (!CostAllocationBases.All.Contains(basis))
        {
            throw new ConflictAppException("Cơ sở chia doanh thu không được hỗ trợ.");
        }

        if (request.Details.Any(d => d.ManualOverrideAmount.HasValue))
        {
            await _permissions.EnsureAsync(
                PermissionCodes.RevenueMappingOverride,
                "Bạn không có quyền sửa kết quả chia doanh thu tự động.",
                cancellationToken);
            if (request.Details.Any(d => d.ManualOverrideAmount.HasValue && string.IsNullOrWhiteSpace(d.OverrideReason)))
            {
                throw new ConflictAppException("Sửa kết quả chia tự động phải có lý do.");
            }
        }

        var revenue = await _db.Revenues.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RevenueId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy doanh thu.");
        if (revenue.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ chia doanh thu đang hiệu lực.");
        }

        var open = await _db.RevenueMappings.AnyAsync(
            m => m.RevenueId == revenue.Id && m.MappingStatus == CostAllocationStatuses.Draft,
            cancellationToken);
        if (open)
        {
            throw new ConflictAppException("Đã có phiên chia doanh thu chưa chốt.");
        }

        var mode = string.IsNullOrWhiteSpace(request.ApplicabilityMode)
            ? "explicit"
            : request.ApplicabilityMode.Trim().ToLowerInvariant();
        var inputs = await ResolveTargetsAsync(request, mode, cancellationToken);
        if (inputs.Count < 2)
        {
            throw new ConflictAppException("Một doanh thu kinh tế phải chia cho ít nhất 2 Bill.");
        }

        var billIds = inputs.Select(d => d.BillId).Distinct().ToList();
        if (billIds.Count != inputs.Count)
        {
            throw new ConflictAppException("Mỗi Bill chỉ được xuất hiện một lần trong phiên chia.");
        }

        var found = await _db.Bills.AsNoTracking()
            .Where(b => billIds.Contains(b.Id))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);
        if (found.Count != billIds.Count)
        {
            throw new NotFoundAppException("Một hoặc nhiều Bill không tồn tại.");
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var maxVersion = await _db.RevenueMappings
            .Where(m => m.RevenueId == revenue.Id)
            .Select(m => (int?)m.VersionNo)
            .MaxAsync(cancellationToken) ?? 0;

        var mapping = new RevenueMapping
        {
            TenantId = tenantId,
            RevenueId = revenue.Id,
            VersionNo = maxVersion + 1,
            AllocationBasis = basis,
            ApplicabilityMode = mode,
            ScopeId = request.ScopeId,
            ConditionCode = string.IsNullOrWhiteSpace(request.ConditionCode) ? null : request.ConditionCode.Trim(),
            AllocatableAmount = revenue.Amount,
            AllocatedAmount = 0m,
            MappedMaturity = revenue.FinancialMaturity,
            MappingStatus = CostAllocationStatuses.Draft
        };

        var measures = CostAllocationBases.FromMeasurement.Contains(basis)
            ? await _db.OperationalMeasurements.AsNoTracking()
                .Where(m => m.ObjectType == OperationalObjectTypes.Bill
                            && billIds.Contains(m.ObjectId)
                            && m.MeasureCode == MeasureCode(basis))
                .ToListAsync(cancellationToken)
            : [];

        var details = inputs.Select(d =>
        {
            var input = CostAllocationBases.FromMeasurement.Contains(basis)
                ? measures.FirstOrDefault(m => m.ObjectId == d.BillId)?.Quantity
                : d.BasisValue;
            return new RevenueMappingDetail
            {
                TenantId = tenantId,
                MappingId = mapping.Id,
                BillId = d.BillId,
                BasisValue = CreateCostAllocationCommandHandler.ResolveBasisValue(basis, input),
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

        _db.RevenueMappings.Add(mapping);
        _db.RevenueMappingDetails.AddRange(details);
        await _db.SaveChangesAsync(cancellationToken);
        return new RevenueMappingCreated(mapping.Id, mapping.RowVersion);
    }

    private async Task<IReadOnlyList<RevenueMappingLineInput>> ResolveTargetsAsync(
        CreateRevenueMappingCommand request,
        string mode,
        CancellationToken cancellationToken)
    {
        var details = request.Details?.ToList() ?? [];
        if (mode is "leg" or "movement")
        {
            if (request.ScopeId is null)
            {
                throw new ConflictAppException(mode == "leg"
                    ? "Chọn chặng trước khi chia theo chặng."
                    : "Chọn chuyến trước khi chia theo chuyến.");
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
                return allowed.Select(id => new RevenueMappingLineInput(id, null, null, null)).ToList();
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
                throw new ConflictAppException("Chọn điều kiện loại dịch vụ trước khi chia.");
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
            throw new ConflictAppException("Phạm vi chia doanh thu không hợp lệ.");
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

public sealed record FinalizeRevenueMappingCommand(Guid MappingId, string? IfMatch = null) : IRequest;

public sealed class FinalizeRevenueMappingCommandHandler : IRequestHandler<FinalizeRevenueMappingCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;
    private readonly IRowVersionGuard _versions;

    public FinalizeRevenueMappingCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IAuditWriter audit,
        IRowVersionGuard versions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _audit = audit;
        _versions = versions;
    }

    public async Task Handle(FinalizeRevenueMappingCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var mapping = await _db.RevenueMappings
            .FirstOrDefaultAsync(m => m.Id == request.MappingId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên chia doanh thu.");
        _versions.EnsureCurrent(mapping, request.IfMatch);
        if (mapping.MappingStatus is CostAllocationStatuses.Finalized
            or CostAllocationStatuses.Cancelled
            or CostAllocationStatuses.Superseded)
        {
            throw new ConflictAppException("Phiên chia doanh thu đã chốt hoặc đã hủy, không sửa.");
        }

        var revenue = await _db.Revenues.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == mapping.RevenueId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy doanh thu.");

        var details = await _db.RevenueMappingDetails
            .Where(d => d.MappingId == mapping.Id)
            .ToListAsync(cancellationToken);
        mapping.AllocatableAmount = revenue.Amount;
        mapping.MappedMaturity = revenue.FinancialMaturity;
        var lines = details.Select(d => new AllocationSplitLine
        {
            BillId = d.BillId,
            BasisValue = d.BasisValue,
            ManualOverrideAmount = d.ManualOverrideAmount
        }).ToList();
        AllocationMath.ApplyLines(mapping.AllocationBasis, mapping.AllocatableAmount, lines);
        for (var i = 0; i < details.Count; i++)
        {
            details[i].BasisValue = lines[i].BasisValue;
            details[i].BasisRatio = lines[i].BasisRatio;
            details[i].AllocatedAmount = lines[i].AllocatedAmount;
            details[i].RoundingAdjustment = lines[i].RoundingAdjustment;
            details[i].OverrideBeforeAmount = lines[i].OverrideBeforeAmount;
        }

        var sum = decimal.Round(details.Sum(d => d.AllocatedAmount), 4, MidpointRounding.AwayFromZero);
        if (sum != decimal.Round(mapping.AllocatableAmount, 4, MidpointRounding.AwayFromZero))
        {
            throw new ConflictAppException("Không thể chốt chia doanh thu: tổng dòng không khớp số gốc.");
        }

        var prior = await _db.RevenueMappings
            .Where(m => m.RevenueId == mapping.RevenueId
                        && m.Id != mapping.Id
                        && m.MappingStatus == CostAllocationStatuses.Finalized)
            .ToListAsync(cancellationToken);
        foreach (var old in prior)
        {
            old.MappingStatus = CostAllocationStatuses.Superseded;
        }

        mapping.AllocatedAmount = sum;
        mapping.MappingStatus = CostAllocationStatuses.Finalized;
        mapping.FinalizedAt = DateTimeOffset.UtcNow;
        mapping.FinalizedBy = _user.UserId;
        mapping.SupersedesMappingId = prior.OrderByDescending(p => p.VersionNo).FirstOrDefault()?.Id;
        _audit.Append(
            AuditActions.RevenueMappingFinalize,
            AuditObjectTypes.Revenue,
            mapping.RevenueId,
            afterJson: mapping.Id.ToString());
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record CancelRevenueMappingCommand(Guid MappingId) : IRequest;

public sealed class CancelRevenueMappingCommandHandler : IRequestHandler<CancelRevenueMappingCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CancelRevenueMappingCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task Handle(CancelRevenueMappingCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var mapping = await _db.RevenueMappings
            .FirstOrDefaultAsync(m => m.Id == request.MappingId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phiên chia doanh thu.");
        if (mapping.MappingStatus != CostAllocationStatuses.Draft)
        {
            throw new ConflictAppException("Chỉ hủy phiên chia đang nháp.");
        }

        mapping.MappingStatus = CostAllocationStatuses.Cancelled;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
