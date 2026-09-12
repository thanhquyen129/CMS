using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.FinancialControl;
using LCMS.Application.Revenues;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Commands;

public sealed record ConfirmRevenueCommand(Guid RevenueId, decimal? ConfirmedAmount) : IRequest;

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
/// </summary>
public sealed class ConfirmRevenueCommandHandler : IRequestHandler<ConfirmRevenueCommand>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _user;
    private readonly IRevenueFxStub _fx;
    private readonly IRevenueApprovalGate _approvalGate;
    private readonly ICriticalExceptionConfirmGate _criticalExceptionGate;

    public ConfirmRevenueCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IRevenueFxStub fx,
        IRevenueApprovalGate approvalGate,
        ICriticalExceptionConfirmGate criticalExceptionGate)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _fx = fx;
        _approvalGate = approvalGate;
        _criticalExceptionGate = criticalExceptionGate;
    }

    public async Task Handle(ConfirmRevenueCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var revenue = await _db.Revenues.FirstOrDefaultAsync(r => r.Id == request.RevenueId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy doanh thu.");

        if (revenue.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được xác nhận doanh thu đang hiệu lực.");
        }

        if (!string.Equals(revenue.FinancialMaturity, RevenueMaturities.Expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ chuyển Expected → Confirmed; không ghi đè mức độ trước.");
        }

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

        revenue.ConfirmedAmount = confirmed;
        revenue.Amount = confirmed;
        revenue.FinancialMaturity = RevenueMaturities.Confirmed;
        revenue.ConfirmedAt = DateTimeOffset.UtcNow;
        revenue.ConfirmedBy = _user.UserId;
        _fx.ApplyToRevenue(revenue, confirmed);

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record ActualizeRevenueCommand(Guid RevenueId, decimal? ActualAmount) : IRequest;

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

    public ActualizeRevenueCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext user,
        IRevenueFxStub fx)
    {
        _db = db;
        _tenantContext = tenantContext;
        _user = user;
        _fx = fx;
    }

    public async Task Handle(ActualizeRevenueCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var revenue = await _db.Revenues.FirstOrDefaultAsync(r => r.Id == request.RevenueId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy doanh thu.");

        if (revenue.RecordStatus != "active")
        {
            throw new ConflictAppException("Chỉ được thực tế hóa doanh thu đang hiệu lực.");
        }

        if (!string.Equals(revenue.FinancialMaturity, RevenueMaturities.Confirmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictAppException("Chỉ chuyển Confirmed → Actual; không ghi đè mức độ trước.");
        }

        var actual = decimal.Round(
            request.ActualAmount ?? revenue.ConfirmedAmount ?? revenue.ExpectedAmount,
            4,
            MidpointRounding.AwayFromZero);

        revenue.ActualAmount = actual;
        revenue.Amount = actual;
        revenue.FinancialMaturity = RevenueMaturities.Actual;
        revenue.ActualizedAt = DateTimeOffset.UtcNow;
        revenue.ActualizedBy = _user.UserId;
        _fx.ApplyToRevenue(revenue, actual);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
