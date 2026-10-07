using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
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
    DateTimeOffset? EffectiveTo,
    string Direction,
    string SourceKind);

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

        var independent = await ListIndependentAsync(cancellationToken);
        return independent.OrderBy(r => r.Code, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<IReadOnlyList<SurchargeDto>> ListIndependentAsync(CancellationToken cancellationToken)
    {
        var masters = await _db.Surcharges.AsNoTracking().OrderBy(s => s.Code).ToListAsync(cancellationToken);
        if (masters.Count == 0)
        {
            return [];
        }

        var ids = masters.Select(s => s.Id).ToList();
        var versions = await _db.SurchargeVersions.AsNoTracking()
            .Where(v => ids.Contains(v.SurchargeId))
            .ToListAsync(cancellationToken);
        var latest = versions
            .GroupBy(v => v.SurchargeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.VersionNo).First());
        var versionIds = latest.Values.Select(v => v.Id).ToList();
        var rules = await _db.SurchargeRules.AsNoTracking()
            .Where(r => versionIds.Contains(r.SurchargeVersionId))
            .ToListAsync(cancellationToken);
        var ruleIds = rules.Select(r => r.Id).ToList();
        var conditions = await _db.SurchargeConditions.AsNoTracking()
            .Where(c => ruleIds.Contains(c.SurchargeRuleId) && c.Dimension == "transport_mode")
            .ToListAsync(cancellationToken);
        var scopes = await _db.SurchargeScopes.AsNoTracking()
            .Where(s => ruleIds.Contains(s.SurchargeRuleId) && s.RateCardId != null)
            .ToListAsync(cancellationToken);
        var cardIds = scopes.Select(s => s.RateCardId!.Value).Distinct().ToList();
        var cards = await _db.RateCards.AsNoTracking().Where(c => cardIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);

        return masters.Select(master =>
        {
            latest.TryGetValue(master.Id, out var version);
            var rule = rules.Where(r => r.SurchargeVersionId == (version?.Id ?? Guid.Empty)).OrderByDescending(r => r.Priority).FirstOrDefault();
            var mode = conditions.FirstOrDefault(c => c.SurchargeRuleId == (rule?.Id ?? Guid.Empty))?.ValueText;
            var cardId = scopes.FirstOrDefault(s => s.SurchargeRuleId == (rule?.Id ?? Guid.Empty))?.RateCardId;
            cards.TryGetValue(cardId ?? Guid.Empty, out var card);
            return new SurchargeDto(
                master.Id,
                master.Code,
                master.Name,
                rule?.CalculationMode,
                rule?.RateAmountPercent ?? 0m,
                rule?.CurrencyCode ?? "VND",
                mode,
                card?.Code ?? "",
                version?.VersionNo ?? 0,
                version?.PublishStatus ?? "",
                version?.ValidFrom,
                version?.ValidTo,
                master.Direction,
                "independent");
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
    string? ContextJson,
    string? AppliedSurcharges);

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
        var ratingIds = ratings.Select(r => r.Id).ToList();
        var surchargeLines = await _db.RatingDetails.AsNoTracking()
            .Where(d => ratingIds.Contains(d.RatingId) && d.SourceType == RatingSourceTypes.Surcharge)
            .Select(d => new { d.RatingId, d.ComponentCode, d.SourceVersionId })
            .ToListAsync(cancellationToken);
        var surchargeVersionIds = surchargeLines.Where(l => l.SourceVersionId != null).Select(l => l.SourceVersionId!.Value).Distinct().ToList();
        var surchargeVersions = await _db.SurchargeVersions.AsNoTracking()
            .Where(v => surchargeVersionIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.VersionNo, cancellationToken);
        var traceByRating = surchargeLines
            .GroupBy(l => l.RatingId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(", ", g.Select(l =>
                {
                    var no = l.SourceVersionId is Guid id && surchargeVersions.TryGetValue(id, out var versionNo) ? versionNo : 0;
                    return no == 0 ? l.ComponentCode : $"{l.ComponentCode} v{no}";
                })));
        return ratings.Select(r =>
        {
            versionById.TryGetValue(r.RateVersionId, out var version);
            cards.TryGetValue(version?.RateCardId ?? Guid.Empty, out var card);
            traceByRating.TryGetValue(r.Id, out var applied);
            return new RatingHistoryDto(
                r.Id, r.RatedAt, r.BillId, card?.Code, version?.VersionNo, r.Status, r.TotalAmount, r.CurrencyCode, r.ContextJson, applied);
        }).ToList();
    }
}
