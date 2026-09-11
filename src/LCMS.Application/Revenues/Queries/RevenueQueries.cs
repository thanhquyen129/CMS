using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Revenues.Queries;

public sealed record RevenueAdjustmentDto(
    Guid Id,
    string AdjustmentType,
    decimal DeltaAmount,
    string CurrencyCode,
    string Reason,
    DateOnly EffectiveDate,
    string AppliedToMaturity,
    decimal AmountBefore,
    decimal AmountAfter,
    DateTimeOffset CreatedAt);

public sealed record RevenueDto(
    Guid Id,
    Guid BillId,
    string FinancialMaturity,
    decimal ExpectedAmount,
    decimal? ConfirmedAmount,
    decimal? ActualAmount,
    decimal Amount,
    string CurrencyCode,
    decimal? BaseAmount,
    Guid? FxRateId,
    string? RevenueTypeCode,
    Guid? CustomerPartyId,
    string? SourceType,
    Guid? SourceId,
    string? RecognitionPolicyVersion,
    string RecordStatus,
    string ApprovalStatus,
    DateOnly EffectiveDate,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? ActualizedAt,
    IReadOnlyList<RevenueAdjustmentDto> Adjustments);

public sealed record RevenueListItemDto(
    Guid Id,
    Guid BillId,
    string FinancialMaturity,
    decimal Amount,
    string CurrencyCode,
    string? RevenueTypeCode,
    string RecordStatus,
    DateOnly EffectiveDate);

public sealed record GetRevenueByIdQuery(Guid Id) : IRequest<RevenueDto>;

public sealed class GetRevenueByIdQueryHandler : IRequestHandler<GetRevenueByIdQuery, RevenueDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetRevenueByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<RevenueDto> Handle(GetRevenueByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var revenue = await _db.Revenues.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy doanh thu.");

        var adjustments = await _db.RevenueAdjustments.AsNoTracking()
            .Where(a => a.RevenueId == revenue.Id)
            .OrderBy(a => a.Id)
            .Select(a => new RevenueAdjustmentDto(
                a.Id,
                a.AdjustmentType,
                a.DeltaAmount,
                a.CurrencyCode,
                a.Reason,
                a.EffectiveDate,
                a.AppliedToMaturity,
                a.AmountBefore,
                a.AmountAfter,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        return new RevenueDto(
            revenue.Id,
            revenue.BillId,
            revenue.FinancialMaturity,
            revenue.ExpectedAmount,
            revenue.ConfirmedAmount,
            revenue.ActualAmount,
            revenue.Amount,
            revenue.CurrencyCode,
            revenue.BaseAmount,
            revenue.FxRateId,
            revenue.RevenueTypeCode,
            revenue.CustomerPartyId,
            revenue.SourceType,
            revenue.SourceId,
            revenue.RecognitionPolicyVersion,
            revenue.RecordStatus,
            revenue.ApprovalStatus,
            revenue.EffectiveDate,
            revenue.ConfirmedAt,
            revenue.ActualizedAt,
            adjustments);
    }
}

public sealed record ListRevenuesQuery(Guid? BillId, string? FinancialMaturity)
    : IRequest<IReadOnlyList<RevenueListItemDto>>;

public sealed class ListRevenuesQueryHandler : IRequestHandler<ListRevenuesQuery, IReadOnlyList<RevenueListItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListRevenuesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RevenueListItemDto>> Handle(
        ListRevenuesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.Revenues.AsNoTracking().AsQueryable();
        if (request.BillId.HasValue)
        {
            query = query.Where(r => r.BillId == request.BillId);
        }

        if (!string.IsNullOrWhiteSpace(request.FinancialMaturity))
        {
            var maturity = request.FinancialMaturity.Trim().ToLowerInvariant();
            query = query.Where(r => r.FinancialMaturity == maturity);
        }

        return await query
            .OrderByDescending(r => r.EffectiveDate)
            .ThenBy(r => r.Id)
            .Select(r => new RevenueListItemDto(
                r.Id,
                r.BillId,
                r.FinancialMaturity,
                r.Amount,
                r.CurrencyCode,
                r.RevenueTypeCode,
                r.RecordStatus,
                r.EffectiveDate))
            .ToListAsync(cancellationToken);
    }
}
