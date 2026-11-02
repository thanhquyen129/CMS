using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Audit;
using LCMS.Application.Common;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.FinancialCloses;
using LCMS.Application.FinancialControl;
using LCMS.Application.Revenues;
using LCMS.Application.Tenancy;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Commands;

public sealed record ConfirmRevenueCommand(
    Guid RevenueId,
    decimal? ConfirmedAmount,
    string? IfMatch = null,
    string? IdempotencyKey = null) : IRequest;

public sealed class ConfirmRevenueCommandValidator : AbstractValidator<ConfirmRevenueCommand>
{
    public ConfirmRevenueCommandValidator()
    {
        RuleFor(x => x.RevenueId).NotEmpty().WithMessage("Doanh thu không hợp lệ.");
        RuleFor(x => x.ConfirmedAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền xác nhận không được âm.")
            .When(x => x.ConfirmedAmount.HasValue);
    }
}

/// <summary>
/// Expected → Confirmed. Preserves ExpectedAmount (C-009); writes ConfirmedAmount + audit.
/// Optional approval threshold gate (Pass 2 Sprint 5 FULL / ADR-0004).
/// Optional critical-exception block (Pass 2 Sprint 9 FULL).
/// Period lock when financial close is Locked (Pass 2 Sprint 10 FULL).
/// </summary>
public sealed class ConfirmRevenueCommandHandler : IRequestHandler<ConfirmRevenueCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IRevenueFxStub _fx;
    private readonly IRevenueApprovalGate _approvalGate;
    private readonly ICriticalExceptionConfirmGate _criticalExceptionGate;
    private readonly IPeriodLockGate _periodLockGate;
    private readonly IPermissionService _permissions;
    private readonly IRowVersionGuard _versions;
    private readonly IIdempotencyGate _idempotency;
    private readonly ITenantSettingsService _settings;
    private readonly IAuditWriter _audit;

    public ConfirmRevenueCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IRevenueFxStub fx,
        IRevenueApprovalGate approvalGate,
        ICriticalExceptionConfirmGate criticalExceptionGate,
        IPeriodLockGate periodLockGate,
        IPermissionService permissions,
        IRowVersionGuard versions,
        IIdempotencyGate idempotency,
        ITenantSettingsService settings,
        IAuditWriter audit)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _fx = fx;
        _approvalGate = approvalGate;
        _criticalExceptionGate = criticalExceptionGate;
        _periodLockGate = periodLockGate;
        _permissions = permissions;
        _versions = versions;
        _idempotency = idempotency;
        _settings = settings;
        _audit = audit;
    }

    public async Task Handle(ConfirmRevenueCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RevenueConfirm,
            "Bạn không có quyền xác nhận doanh thu.",
            cancellationToken);

        if (await IdempotencyReplay.AlreadyAppliedAsync(
                _idempotency,
                IdempotencyScopes.RevenueConfirm,
                request.IdempotencyKey,
                request.RevenueId,
                cancellationToken))
        {
            return;
        }

        var revenue = await _db.Revenues.FirstOrDefaultAsync(r => r.Id == request.RevenueId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy doanh thu.");
        _versions.EnsureCurrent(revenue, request.IfMatch);

        if (revenue.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được xác nhận doanh thu đang hiệu lực.");
        }

        if (RevenueMaturityModels.IsTwoLayer(await _settings.GetFinancialAsync(cancellationToken)))
        {
            throw new ConflictAppException(
                "Thuê bao dùng mô hình 2 lớp Expected → Actual. Không tạo lớp Confirmed.");
        }

        if (!string.Equals(revenue.FinancialMaturity, RevenueMaturities.Expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ chuyển Expected → Confirmed; không ghi đè mức độ trước.");
        }

        await _periodLockGate.EnsureMutationAllowedAsync(
            revenue.BillId,
            revenue.EffectiveDate,
            "xác nhận doanh thu",
            cancellationToken);

        await _criticalExceptionGate.EnsureConfirmAllowedAsync(
            ApprovalObjectTypes.Revenue,
            revenue.Id,
            cancellationToken);

        try
        {
            _approvalGate.EnsureConfirmAllowed(revenue);
        }
        catch (ConflictAppException)
        {
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }

        var confirmed = decimal.Round(
            request.ConfirmedAmount ?? revenue.ExpectedAmount,
            4,
            MidpointRounding.AwayFromZero);
        var beforeJson = AuditJson.Serialize(new
        {
            maturity = revenue.FinancialMaturity,
            expected = revenue.ExpectedAmount,
            confirmed = revenue.ConfirmedAmount,
            actual = revenue.ActualAmount
        });

        revenue.ConfirmedAmount = confirmed;
        revenue.Amount = confirmed;
        revenue.FinancialMaturity = RevenueMaturities.Confirmed;
        revenue.ConfirmedAt = DateTimeOffset.UtcNow;
        revenue.ConfirmedBy = _user.UserId;
        await _fx.ApplyToRevenueAsync(revenue, confirmed, cancellationToken);

        _audit.Append(
            AuditActions.RevenueConfirm,
            AuditObjectTypes.Revenue,
            revenue.Id,
            beforeJson,
            AuditJson.Serialize(new
            {
                maturity = revenue.FinancialMaturity,
                expected = revenue.ExpectedAmount,
                confirmed = revenue.ConfirmedAmount,
                actor = _user.UserId,
                source = "maturity_transition"
            }),
            "Expected → Confirmed");
        _idempotency.Remember(
            IdempotencyScopes.RevenueConfirm,
            request.IdempotencyKey ?? "",
            revenue.Id,
            _tenantContext.TenantId!.Value);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record ActualizeRevenueCommand(
    Guid RevenueId,
    decimal? ActualAmount,
    string? SourceSystem = null,
    string? OverrideReason = null,
    string? IfMatch = null,
    string? IdempotencyKey = null) : IRequest;

public sealed class ActualizeRevenueCommandValidator : AbstractValidator<ActualizeRevenueCommand>
{
    public ActualizeRevenueCommandValidator()
    {
        RuleFor(x => x.RevenueId).NotEmpty().WithMessage("Doanh thu không hợp lệ.");
        RuleFor(x => x.ActualAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền thực tế không được âm.")
            .When(x => x.ActualAmount.HasValue);
    }
}

/// <summary>
/// Confirmed → Actual. Preserves ExpectedAmount and ConfirmedAmount (C-009).
/// </summary>
public sealed class ActualizeRevenueCommandHandler : IRequestHandler<ActualizeRevenueCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IRevenueFxStub _fx;
    private readonly IPermissionService _permissions;
    private readonly IAuditWriter _audit;
    private readonly IRowVersionGuard _versions;
    private readonly IIdempotencyGate _idempotency;
    private readonly ITenantSettingsService _settings;

    public ActualizeRevenueCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IRevenueFxStub fx,
        IPermissionService permissions,
        IAuditWriter audit,
        IRowVersionGuard versions,
        IIdempotencyGate idempotency,
        ITenantSettingsService settings)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _fx = fx;
        _permissions = permissions;
        _audit = audit;
        _versions = versions;
        _idempotency = idempotency;
        _settings = settings;
    }

    public async Task Handle(ActualizeRevenueCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.RevenueActualize,
            "Bạn không có quyền thực tế hóa doanh thu.",
            cancellationToken);

        if (await IdempotencyReplay.AlreadyAppliedAsync(
                _idempotency,
                IdempotencyScopes.RevenueActualize,
                request.IdempotencyKey,
                request.RevenueId,
                cancellationToken))
        {
            return;
        }

        var revenue = await _db.Revenues.FirstOrDefaultAsync(r => r.Id == request.RevenueId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy doanh thu.");
        _versions.EnsureCurrent(revenue, request.IfMatch);

        if (revenue.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được thực tế hóa doanh thu đang hiệu lực.");
        }

        var twoLayer = RevenueMaturityModels.IsTwoLayer(await _settings.GetFinancialAsync(cancellationToken));
        var fromExpected = string.Equals(revenue.FinancialMaturity, RevenueMaturities.Expected, StringComparison.OrdinalIgnoreCase);
        var fromConfirmed = string.Equals(revenue.FinancialMaturity, RevenueMaturities.Confirmed, StringComparison.OrdinalIgnoreCase);
        if (twoLayer)
        {
            if (!fromExpected && !fromConfirmed)
            {
                throw new ConflictAppException("Mô hình 2 lớp chỉ chuyển Expected hoặc Confirmed lịch sử → Actual.");
            }
        }
        else if (!fromConfirmed)
        {
            throw new ConflictAppException("Chỉ chuyển Confirmed → Actual; không ghi đè mức độ trước.");
        }

        var source = string.IsNullOrWhiteSpace(request.SourceSystem)
            ? OperationalSourceSystems.LcmsManual
            : request.SourceSystem.Trim();
        var owned = await _db.FieldOwnerships.FirstOrDefaultAsync(
            f => f.ObjectType == "revenue" && f.ObjectId == revenue.Id && f.FieldName == "actual_revenue",
            cancellationToken);
        if (owned is not null
            && !string.Equals(owned.OwnerSystem, source, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.OverrideReason))
            {
                throw new ConflictAppException("RV-06: Nguồn không sở hữu doanh thu thực tế. Không ghi đè.");
            }

            _audit.Append(
                AuditActions.FieldOverride,
                AuditObjectTypes.Revenue,
                revenue.Id,
                reason: request.OverrideReason.Trim(),
                afterJson: "actual_revenue");
        }

        var actual = decimal.Round(
            request.ActualAmount ?? (twoLayer && fromExpected ? revenue.ExpectedAmount : revenue.ConfirmedAmount ?? revenue.ExpectedAmount),
            4,
            MidpointRounding.AwayFromZero);
        var beforeActual = AuditJson.Serialize(new
        {
            maturity = revenue.FinancialMaturity,
            expected = revenue.ExpectedAmount,
            confirmed = revenue.ConfirmedAmount,
            actual = revenue.ActualAmount
        });

        revenue.ActualAmount = actual;
        revenue.Amount = actual;
        revenue.FinancialMaturity = RevenueMaturities.Actual;
        revenue.ActualizedAt = DateTimeOffset.UtcNow;
        revenue.ActualizedBy = _user.UserId;
        await _fx.ApplyToRevenueAsync(revenue, actual, cancellationToken);

        _audit.Append(
            AuditActions.RevenueActualize,
            AuditObjectTypes.Revenue,
            revenue.Id,
            beforeActual,
            AuditJson.Serialize(new
            {
                maturity = revenue.FinancialMaturity,
                expected = revenue.ExpectedAmount,
                confirmed = revenue.ConfirmedAmount,
                actual = revenue.ActualAmount,
                actor = _user.UserId,
                model = twoLayer ? RevenueMaturityModels.Two : RevenueMaturityModels.Three,
                source = string.IsNullOrWhiteSpace(request.SourceSystem) ? OperationalSourceSystems.LcmsManual : request.SourceSystem.Trim()
            }),
            request.OverrideReason ?? (twoLayer && fromExpected ? "Expected → Actual" : "Confirmed → Actual"));
        _idempotency.Remember(
            IdempotencyScopes.RevenueActualize,
            request.IdempotencyKey ?? "",
            revenue.Id,
            _tenantContext.TenantId!.Value);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
