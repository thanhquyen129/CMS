using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateCards.Queries;

public sealed record SurchargeDto(
    Guid Id,
    string Code,
    string Name,
    string? CalcMethod,
    decimal Amount,
    string CurrencyCode,
    string? TransportMode,
    string CardCode,
    int VersionNo,
    string VersionStatus,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record ListSurchargesQuery : IRequest<IReadOnlyList<SurchargeDto>>;

public sealed class ListSurchargesQueryHandler : IRequestHandler<ListSurchargesQuery, IReadOnlyList<SurchargeDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListSurchargesQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<SurchargeDto>> Handle(ListSurchargesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var components = await _db.PricingRuleComponents.AsNoTracking().OrderBy(c => c.Code).ToListAsync(cancellationToken);
        if (components.Count == 0)
        {
            return [];
        }

        var ruleIds = components.Select(c => c.PricingRuleId).Distinct().ToList();
        var rules = await _db.PricingRules.AsNoTracking().Where(r => ruleIds.Contains(r.Id)).ToListAsync(cancellationToken);
        var versionIds = rules.Select(r => r.RateVersionId).Distinct().ToList();
        var versions = await _db.RateVersions.AsNoTracking().Where(v => versionIds.Contains(v.Id)).ToListAsync(cancellationToken);
        var cardIds = versions.Select(v => v.RateCardId).Distinct().ToList();
        var cards = await _db.RateCards.AsNoTracking().Where(c => cardIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        var ruleById = rules.ToDictionary(r => r.Id);
        var versionById = versions.ToDictionary(v => v.Id);

        return components.Select(c =>
        {
            ruleById.TryGetValue(c.PricingRuleId, out var rule);
            versionById.TryGetValue(rule?.RateVersionId ?? Guid.Empty, out var version);
            cards.TryGetValue(version?.RateCardId ?? Guid.Empty, out var card);
            return new SurchargeDto(
                c.Id,
                c.Code,
                c.Name,
                c.CalcMethod ?? rule?.CalcMethod,
                c.Amount,
                c.CurrencyCode,
                card?.TransportMode,
                card?.Code ?? "",
                version?.VersionNo ?? 0,
                version?.Status ?? "",
                version?.EffectiveFrom,
                version?.EffectiveTo);
        }).ToList();
    }
}

public sealed record RateAppendixDto(
    Guid Id,
    Guid RateCardId,
    string CardCode,
    string CardName,
    string? Note,
    DateTimeOffset? EffectiveFrom,
    int VersionNo,
    string Status);

public sealed record ListRateAppendicesQuery : IRequest<IReadOnlyList<RateAppendixDto>>;

public sealed class ListRateAppendicesQueryHandler : IRequestHandler<ListRateAppendicesQuery, IReadOnlyList<RateAppendixDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListRateAppendicesQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<RateAppendixDto>> Handle(ListRateAppendicesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var versions = await _db.RateVersions.AsNoTracking().OrderByDescending(v => v.CreatedAt).ToListAsync(cancellationToken);
        var cardIds = versions.Select(v => v.RateCardId).Distinct().ToList();
        var cards = await _db.RateCards.AsNoTracking().Where(c => cardIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        return versions.Select(v =>
        {
            cards.TryGetValue(v.RateCardId, out var card);
            return new RateAppendixDto(v.Id, v.RateCardId, card?.Code ?? "", card?.Name ?? "", v.Note, v.EffectiveFrom, v.VersionNo, v.Status);
        }).ToList();
    }
}

public sealed record RatingHistoryDto(
    Guid Id,
    DateTimeOffset RatedAt,
    Guid BillId,
    string? CardCode,
    int? VersionNo,
    string Status,
    decimal TotalAmount,
    string CurrencyCode,
    string? ContextJson);

public sealed record ListRatingHistoryQuery : IRequest<IReadOnlyList<RatingHistoryDto>>;

public sealed class ListRatingHistoryQueryHandler : IRequestHandler<ListRatingHistoryQuery, IReadOnlyList<RatingHistoryDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListRatingHistoryQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<RatingHistoryDto>> Handle(ListRatingHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var ratings = await _db.Ratings.AsNoTracking().OrderByDescending(r => r.RatedAt).Take(200).ToListAsync(cancellationToken);
        var versionIds = ratings.Select(r => r.RateVersionId).Distinct().ToList();
        var versions = await _db.RateVersions.AsNoTracking().Where(v => versionIds.Contains(v.Id)).ToListAsync(cancellationToken);
        var cardIds = versions.Select(v => v.RateCardId).Distinct().ToList();
        var cards = await _db.RateCards.AsNoTracking().Where(c => cardIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        var versionById = versions.ToDictionary(v => v.Id);
        return ratings.Select(r =>
        {
            versionById.TryGetValue(r.RateVersionId, out var version);
            cards.TryGetValue(version?.RateCardId ?? Guid.Empty, out var card);
            return new RatingHistoryDto(
                r.Id, r.RatedAt, r.BillId, card?.Code, version?.VersionNo, r.Status, r.TotalAmount, r.CurrencyCode, r.ContextJson);
        }).ToList();
    }
}
