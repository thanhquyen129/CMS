using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LCMS.Application.Fx.Queries;

public sealed record PreviewFxQuery(string CurrencyCode, DateOnly? AsOf, decimal? Amount)
    : IRequest<FxPreviewDto>;

public sealed class PreviewFxQueryHandler : IRequestHandler<PreviewFxQuery, FxPreviewDto>
{
    private readonly ITenantContext _tenant;
    private readonly IFxSnapshotService _fx;
    private readonly CostOptions _cost;

    public PreviewFxQueryHandler(ITenantContext tenant, IFxSnapshotService fx, IOptions<CostOptions> cost)
    {
        _tenant = tenant;
        _fx = fx;
        _cost = cost.Value;
    }

    public Task<FxPreviewDto> Handle(PreviewFxQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var stub = new FxStubPolicy(
            string.IsNullOrWhiteSpace(_cost.BaseCurrency) ? "VND" : _cost.BaseCurrency.Trim().ToUpperInvariant(),
            _cost.AllowStubFxFallback,
            _cost.StubFxRatesToBase,
            CostOptions.SectionName);
        return _fx.PreviewAsync(
            request.CurrencyCode,
            request.AsOf ?? DateOnly.FromDateTime(DateTime.UtcNow),
            request.Amount,
            stub,
            cancellationToken);
    }
}

public sealed record FxExceptionItemDto(
    string ObjectType,
    Guid Id,
    Guid? BillId,
    string CurrencyCode,
    decimal Amount,
    string FxStatus,
    DateOnly AsOf);

public sealed record ListFxExceptionsQuery : IRequest<IReadOnlyList<FxExceptionItemDto>>;

/// <summary>Rows that cannot enter a reporting total (missing rate or migration review).</summary>
public sealed class ListFxExceptionsQueryHandler : IRequestHandler<ListFxExceptionsQuery, IReadOnlyList<FxExceptionItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListFxExceptionsQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<FxExceptionItemDto>> Handle(
        ListFxExceptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var items = new List<FxExceptionItemDto>();
        items.AddRange(await _db.Costs.AsNoTracking()
            .Where(c => c.RecordStatus == "active"
                        && (c.FxStatus == FxStatuses.Missing || c.FxStatus == FxStatuses.RequiresReview))
            .Select(c => new FxExceptionItemDto("cost", c.Id, c.BillId, c.CurrencyCode, c.Amount, c.FxStatus, c.EffectiveDate))
            .ToListAsync(cancellationToken));
        items.AddRange(await _db.Revenues.AsNoTracking()
            .Where(r => r.RecordStatus == "active"
                        && (r.FxStatus == FxStatuses.Missing || r.FxStatus == FxStatuses.RequiresReview))
            .Select(r => new FxExceptionItemDto("revenue", r.Id, r.BillId, r.CurrencyCode, r.Amount, r.FxStatus, r.EffectiveDate))
            .ToListAsync(cancellationToken));
        items.AddRange(await _db.Payments.AsNoTracking()
            .Where(p => p.RecordStatus == "active"
                        && (p.FxStatus == FxStatuses.Missing || p.FxStatus == FxStatuses.RequiresReview))
            .Select(p => new FxExceptionItemDto("payment", p.Id, p.BillId, p.CurrencyCode, p.Amount, p.FxStatus, p.ValueDate))
            .ToListAsync(cancellationToken));
        items.AddRange(await _db.Collections.AsNoTracking()
            .Where(c => c.RecordStatus == "active"
                        && (c.FxStatus == FxStatuses.Missing || c.FxStatus == FxStatuses.RequiresReview))
            .Select(c => new FxExceptionItemDto("collection", c.Id, c.BillId, c.CurrencyCode, c.Amount, c.FxStatus, c.ValueDate))
            .ToListAsync(cancellationToken));
        return items
            .OrderBy(i => i.ObjectType, StringComparer.Ordinal)
            .ThenBy(i => i.AsOf)
            .ToList();
    }
}
