using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Ratings.Commands;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings.Queries;

public sealed record RateQuoteDto(
    Guid RateCardId,
    Guid RateVersionId,
    string CardCode,
    string CardName,
    string? CarrierName,
    string PartyType,
    int VersionNo,
    string CurrencyCode,
    decimal TotalAmount,
    decimal BaseAmount,
    decimal SurchargeAmount,
    string RuleCodes,
    DateTimeOffset? EffectiveTo,
    string? Error);

/// <summary>Quotes every published version still effective on the rate date. Does not insert a rating.</summary>
public sealed record CompareRatesQuery(
    Guid? BillId,
    string? PartyType,
    string? TransportMode,
    string? OriginCode,
    string? DestinationCode,
    string? RouteCode,
    DateTimeOffset? RateDate,
    decimal? Quantity,
    decimal? GrossWeightKg,
    decimal? VolumeCbm) : IRequest<IReadOnlyList<RateQuoteDto>>;

public sealed class CompareRatesQueryHandler : IRequestHandler<CompareRatesQuery, IReadOnlyList<RateQuoteDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public CompareRatesQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<RateQuoteDto>> Handle(CompareRatesQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var rateDate = request.RateDate ?? DateTimeOffset.UtcNow;
        var published = await _db.RateVersions.AsNoTracking()
            .Where(v => v.Status == RateVersionStatuses.Published)
            .ToListAsync(cancellationToken);
        var versions = published
            .Where(v => v.EffectiveFrom is null || v.EffectiveFrom <= rateDate)
            .Where(v => v.EffectiveTo is null || v.EffectiveTo >= rateDate)
            .ToList();
        if (versions.Count == 0)
        {
            return [];
        }

        var cardIds = versions.Select(v => v.RateCardId).Distinct().ToList();
        var cards = await _db.RateCards.AsNoTracking()
            .Where(c => cardIds.Contains(c.Id) && c.IsActive)
            .ToListAsync(cancellationToken);
        var party = request.PartyType?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(party))
        {
            cards = cards.Where(c => c.PartyType == party).ToList();
        }

        var mode = request.TransportMode?.Trim();
        if (!string.IsNullOrWhiteSpace(mode))
        {
            cards = cards.Where(c =>
                string.IsNullOrWhiteSpace(c.TransportMode)
                || string.Equals(c.TransportMode, mode, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var cardById = cards.ToDictionary(c => c.Id);
        var versionIds = versions.Where(v => cardById.ContainsKey(v.RateCardId)).Select(v => v.Id).ToList();
        var rules = await _db.PricingRules.AsNoTracking()
            .Where(r => versionIds.Contains(r.RateVersionId))
            .ToListAsync(cancellationToken);
        var ruleIds = rules.Select(r => r.Id).ToList();
        var components = await _db.PricingRuleComponents.AsNoTracking()
            .Where(c => ruleIds.Contains(c.PricingRuleId))
            .ToListAsync(cancellationToken);
        var breaks = await _db.RateBreaks.AsNoTracking()
            .Where(b => ruleIds.Contains(b.PricingRuleId))
            .ToListAsync(cancellationToken);
        var containers = await _db.ContainerRatePrices.AsNoTracking()
            .Where(p => ruleIds.Contains(p.PricingRuleId))
            .ToListAsync(cancellationToken);

        var quotes = new List<RateQuoteDto>();
        foreach (var version in versions.Where(v => cardById.ContainsKey(v.RateCardId)))
        {
            var card = cardById[version.RateCardId];
            var applicable = rules
                .Where(r => r.RateVersionId == version.Id)
                .Where(r => RatingEngine.Matches(
                    r, null, null, request.RouteCode ?? card.RouteCode,
                    mode ?? card.TransportMode, request.OriginCode, request.DestinationCode, null))
                .ToList();
            try
            {
                var selected = RatingEngine.Select(applicable);
                var factor = selected.FirstOrDefault(r => r.VolumetricFactor is not null || r.RoundingStep is not null);
                var computed = RatingEngine.Chargeable(
                    mode ?? card.TransportMode,
                    request.GrossWeightKg,
                    request.VolumeCbm,
                    factor?.VolumetricFactor,
                    factor?.RoundingStep);
                var quantity = request.Quantity ?? computed ?? 1m;
                var (total, first, codes) = Price(selected, components, breaks, containers, quantity);
                quotes.Add(new RateQuoteDto(
                    card.Id, version.Id, card.Code, card.Name, card.CarrierName, card.PartyType,
                    version.VersionNo, card.CurrencyCode, total, first, total - first,
                    codes, version.EffectiveTo, null));
            }
            catch (ConflictAppException ex) when (ex.Message.Contains("NO_APPLICABLE_RATE", StringComparison.Ordinal))
            {
                continue;
            }
            catch (ConflictAppException ex)
            {
                quotes.Add(new RateQuoteDto(
                    card.Id, version.Id, card.Code, card.Name, card.CarrierName, card.PartyType,
                    version.VersionNo, card.CurrencyCode, 0, 0, 0, "", version.EffectiveTo, ex.Message));
            }
        }

        return quotes.OrderBy(q => q.Error is null ? 0 : 1).ThenBy(q => q.TotalAmount).ToList();
    }

    private static (decimal Total, decimal First, string Codes) Price(
        IReadOnlyList<PricingRule> selected,
        IReadOnlyList<PricingRuleComponent> components,
        IReadOnlyList<RateBreak> breaks,
        IReadOnlyList<ContainerRatePrice> containers,
        decimal quantity)
    {
        decimal total = 0m;
        decimal running = 0m;
        decimal first = 0m;
        var seen = false;
        foreach (var rule in selected)
        {
            decimal amount;
            if (string.Equals(rule.CalcMethod, PricingCalcMethods.WeightBreakPivot, StringComparison.OrdinalIgnoreCase))
            {
                amount = RatingEngine.WeightBreakPivot(quantity, Of(breaks, rule.Id), rule.MinAmount).Amount;
            }
            else if (string.Equals(rule.CalcMethod, PricingCalcMethods.WeightStep, StringComparison.OrdinalIgnoreCase))
            {
                amount = RatingEngine.WeightStep(quantity, rule.RoundingStep, rule.UnitAmount, Of(breaks, rule.Id), rule.MinAmount).Amount;
            }
            else if (string.Equals(rule.CalcMethod, PricingCalcMethods.ContainerRate, StringComparison.OrdinalIgnoreCase))
            {
                amount = 0m;
            }
            else if (string.Equals(rule.CalcMethod, PricingCalcMethods.Composite, StringComparison.OrdinalIgnoreCase))
            {
                var parts = components.Where(c => c.PricingRuleId == rule.Id).ToList();
                var ordered = RatingEngine.OrderComponents(parts);
                var byCode = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                amount = 0m;
                foreach (var component in ordered)
                {
                    var componentBase = !string.IsNullOrWhiteSpace(component.DependsOnCode) && byCode.TryGetValue(component.DependsOnCode, out var dep)
                        ? dep
                        : running;
                    var line = CreateRatingCommandHandler.ComputeAmount(
                        string.IsNullOrWhiteSpace(component.CalcMethod) ? PricingCalcMethods.Fixed : component.CalcMethod,
                        component.Amount, quantity, componentBase, rule.MinAmount, rule.MaxAmount);
                    byCode[component.Code] = line;
                    amount += line;
                    running += line;
                }
            }
            else
            {
                var parts = components.Where(c => c.PricingRuleId == rule.Id).ToList();
                if (parts.Count > 0)
                {
                    amount = 0m;
                    foreach (var component in parts)
                    {
                        var line = CreateRatingCommandHandler.ComputeAmount(
                            rule.CalcMethod, component.Amount, quantity, running, rule.MinAmount, rule.MaxAmount);
                        amount += line;
                        running += line;
                    }
                }
                else
                {
                    amount = CreateRatingCommandHandler.ComputeAmount(
                        rule.CalcMethod, rule.UnitAmount, quantity, running, rule.MinAmount, rule.MaxAmount);
                    running += amount;
                }
            }

            if (!seen)
            {
                first = amount;
                seen = true;
            }

            total += amount;
        }

        return (total, first, string.Join(", ", selected.Select(r => r.Code)));
    }

    private static IReadOnlyList<RateBreak> Of(IReadOnlyList<RateBreak> breaks, Guid ruleId) =>
        breaks.Where(b => b.PricingRuleId == ruleId).ToList();
}
