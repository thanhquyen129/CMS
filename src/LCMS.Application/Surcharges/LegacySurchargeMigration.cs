using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Surcharges;

public sealed record MigrateLegacySurchargesResult(
    int Migrated,
    int SkippedBase,
    int SkippedUnknown,
    int SkippedPublished,
    int AlreadyMapped);

public sealed record MigrateLegacySurchargesCommand : IRequest<MigrateLegacySurchargesResult>;

public sealed class MigrateLegacySurchargesCommandHandler
    : IRequestHandler<MigrateLegacySurchargesCommand, MigrateLegacySurchargesResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public MigrateLegacySurchargesCommandHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<MigrateLegacySurchargesResult> Handle(
        MigrateLegacySurchargesCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenant.TenantId!.Value;
        var components = await _db.PricingRuleComponents.ToListAsync(cancellationToken);
        var logged = await _db.SurchargeMigrationLogs.AsNoTracking()
            .Select(l => l.PricingRuleComponentId)
            .ToListAsync(cancellationToken);
        var seen = logged.ToHashSet();
        var ruleIds = components.Select(c => c.PricingRuleId).Distinct().ToList();
        var rules = await _db.PricingRules.AsNoTracking().Where(r => ruleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken);
        var versionIds = rules.Values.Select(r => r.RateVersionId).Distinct().ToList();
        var versions = await _db.RateVersions.AsNoTracking().Where(v => versionIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, cancellationToken);
        var cardIds = versions.Values.Select(v => v.RateCardId).Distinct().ToList();
        var cards = await _db.RateCards.AsNoTracking().Where(c => cardIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        var breakRuleIds = rules.Values.Select(r => r.Id).ToList();
        var breaks = await _db.RateBreaks.AsNoTracking().Where(b => breakRuleIds.Contains(b.PricingRuleId)).ToListAsync(cancellationToken);
        var existingCodes = await _db.Surcharges.AsNoTracking().Select(s => s.Code).ToListAsync(cancellationToken);
        var codes = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var migrated = 0;
        var skippedBase = 0;
        var skippedUnknown = 0;
        var skippedPublished = 0;
        var already = 0;

        foreach (var component in components)
        {
            if (!seen.Add(component.Id))
            {
                already++;
                continue;
            }

            rules.TryGetValue(component.PricingRuleId, out var rule);
            RateVersion? version = null;
            if (rule is not null)
            {
                versions.TryGetValue(rule.RateVersionId, out version);
            }

            cards.TryGetValue(version?.RateCardId ?? Guid.Empty, out var card);
            var classification = LegacyComponentClassifier.Classify(component.Code, component.Name);
            string outcome;
            Guid? surchargeId = null;
            if (classification == LegacyComponentClasses.Base)
            {
                outcome = SurchargeMigrationOutcomes.SkippedBase;
                skippedBase++;
            }
            else if (classification == LegacyComponentClasses.Unknown)
            {
                outcome = SurchargeMigrationOutcomes.SkippedUnknown;
                skippedUnknown++;
            }
            else if (!codes.Add(component.Code.Trim()))
            {
                outcome = SurchargeMigrationOutcomes.AlreadyMapped;
                already++;
            }
            else
            {
                var created = CopyDraft(tenantId, component, rule, version, card, breaks);
                surchargeId = created.Id;
                outcome = SurchargeMigrationOutcomes.Migrated;
                migrated++;
            }

            _db.SurchargeMigrationLogs.Add(new SurchargeMigrationLog
            {
                TenantId = tenantId,
                PricingRuleComponentId = component.Id,
                Classification = classification,
                Outcome = outcome,
                SurchargeId = surchargeId
            });
        }

        var componentRuleIds = components.Select(c => c.PricingRuleId).ToHashSet();
        var embeddedRules = await _db.PricingRules.ToListAsync(cancellationToken);
        var embeddedVersionIds = embeddedRules.Select(r => r.RateVersionId).Distinct().ToList();
        var embeddedVersions = await _db.RateVersions.AsNoTracking()
            .Where(v => embeddedVersionIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);
        var embeddedCardIds = embeddedVersions.Values.Select(v => v.RateCardId).Distinct().ToList();
        var embeddedCards = await _db.RateCards.AsNoTracking()
            .Where(c => embeddedCardIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);
        var embeddedRuleIds = embeddedRules.Select(r => r.Id).ToList();
        var embeddedBreaks = await _db.RateBreaks.AsNoTracking()
            .Where(b => embeddedRuleIds.Contains(b.PricingRuleId))
            .ToListAsync(cancellationToken);

        foreach (var rule in embeddedRules)
        {
            if (!LegacyComponentClassifier.IsAdditionalCharge(rule.Code, rule.Name, rule.ChargeCode))
            {
                continue;
            }

            if (!seen.Add(rule.Id))
            {
                already++;
                continue;
            }

            if (componentRuleIds.Contains(rule.Id))
            {
                continue;
            }

            embeddedVersions.TryGetValue(rule.RateVersionId, out var ruleVersion);
            embeddedCards.TryGetValue(ruleVersion?.RateCardId ?? Guid.Empty, out var ruleCard);
            string outcome;
            Guid? surchargeId = null;
            if (!codes.Add(rule.Code.Trim()))
            {
                outcome = SurchargeMigrationOutcomes.AlreadyMapped;
                already++;
            }
            else
            {
                var created = CopyRule(tenantId, rule, ruleVersion, ruleCard, embeddedBreaks);
                surchargeId = created.Id;
                outcome = SurchargeMigrationOutcomes.Migrated;
                migrated++;
            }

            _db.SurchargeMigrationLogs.Add(new SurchargeMigrationLog
            {
                TenantId = tenantId,
                PricingRuleComponentId = rule.Id,
                Classification = LegacyComponentClasses.Surcharge,
                Outcome = outcome,
                SurchargeId = surchargeId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new MigrateLegacySurchargesResult(migrated, skippedBase, skippedUnknown, skippedPublished, already);
    }

    private Surcharge CopyDraft(
        Guid tenantId,
        PricingRuleComponent component,
        PricingRule? rule,
        RateVersion? version,
        RateCard? card,
        IReadOnlyList<RateBreak> breaks)
    {
        var direction = string.Equals(card?.PartyType, "customer", StringComparison.OrdinalIgnoreCase)
            ? SurchargeDirections.Sell
            : SurchargeDirections.Buy;
        var mode = MapMode(component.CalcMethod ?? rule?.CalcMethod);
        var surcharge = new Surcharge
        {
            TenantId = tenantId,
            Code = component.Code.Trim(),
            Name = component.Name.Trim(),
            Direction = direction,
            Status = SurchargeStatuses.Active,
            SourceLegacyComponentId = component.Id
        };
        var surchargeVersion = new SurchargeVersion
        {
            TenantId = tenantId,
            SurchargeId = surcharge.Id,
            VersionNo = 1,
            PublishStatus = version?.IsPublished == true
                ? SurchargeVersionStatuses.Published
                : SurchargeVersionStatuses.Draft,
            ValidFrom = version?.EffectiveFrom,
            ValidTo = version?.EffectiveTo,
            PublishedAt = version?.IsPublished == true
                ? version!.PublishedAt ?? version.EffectiveFrom ?? DateTimeOffset.UtcNow
                : null
        };
        var surchargeRule = new SurchargeRule
        {
            TenantId = tenantId,
            SurchargeVersionId = surchargeVersion.Id,
            CalculationMode = mode,
            Basis = mode == SurchargeCalcModes.UnitRate ? "chargeable_weight" : null,
            CurrencyCode = component.CurrencyCode,
            RateAmountPercent = component.Amount,
            Priority = component.SortOrder
        };
        _db.Surcharges.Add(surcharge);
        _db.SurchargeVersions.Add(surchargeVersion);
        _db.SurchargeRules.Add(surchargeRule);
        if (!string.IsNullOrWhiteSpace(card?.TransportMode))
        {
            _db.SurchargeConditions.Add(new SurchargeCondition
            {
                TenantId = tenantId,
                SurchargeRuleId = surchargeRule.Id,
                Dimension = "transport_mode",
                Operator = "eq",
                ValueText = card.TransportMode
            });
        }

        if (version is not null)
        {
            _db.SurchargeScopes.Add(new SurchargeScope
            {
                TenantId = tenantId,
                SurchargeRuleId = surchargeRule.Id,
                RateCardId = version.RateCardId
            });
        }

        if (mode == SurchargeCalcModes.WeightBreak && rule is not null)
        {
            foreach (var band in breaks.Where(b => b.PricingRuleId == rule.Id).OrderBy(b => b.SequenceNo))
            {
                _db.SurchargeBreaks.Add(new SurchargeBreak
                {
                    TenantId = tenantId,
                    SurchargeRuleId = surchargeRule.Id,
                    SequenceNo = band.SequenceNo,
                    MinQuantity = band.MinQuantity,
                    MaxQuantity = band.MaxQuantity,
                    UnitAmount = band.UnitAmount
                });
            }
        }

        return surcharge;
    }

    private Surcharge CopyRule(
        Guid tenantId,
        PricingRule rule,
        RateVersion? version,
        RateCard? card,
        IReadOnlyList<RateBreak> breaks)
    {
        var direction = string.Equals(card?.PartyType, "customer", StringComparison.OrdinalIgnoreCase)
            ? SurchargeDirections.Sell
            : SurchargeDirections.Buy;
        var mode = MapMode(rule.CalcMethod);
        var perGross = string.Equals(rule.Applicability, RatingEngine.PerGrossKg, StringComparison.OrdinalIgnoreCase);
        var rate = rule.UnitAmount;
        decimal? weightTo = null;
        if (string.Equals(rule.CalcMethod, PricingCalcMethods.WeightStep, StringComparison.OrdinalIgnoreCase))
        {
            var band = breaks
                .Where(b => b.PricingRuleId == rule.Id && b.UnitAmount > 0)
                .OrderBy(b => b.SequenceNo)
                .FirstOrDefault();
            if (band is not null)
            {
                mode = SurchargeCalcModes.FixedRate;
                rate = band.UnitAmount;
                weightTo = band.MaxQuantity;
            }
        }
        var surcharge = new Surcharge
        {
            TenantId = tenantId,
            Code = rule.Code.Trim(),
            Name = rule.Name.Trim(),
            Direction = direction,
            Status = SurchargeStatuses.Active,
            SourceLegacyComponentId = rule.Id
        };
        var published = version?.IsPublished == true;
        var surchargeVersion = new SurchargeVersion
        {
            TenantId = tenantId,
            SurchargeId = surcharge.Id,
            VersionNo = 1,
            PublishStatus = published ? SurchargeVersionStatuses.Published : SurchargeVersionStatuses.Draft,
            ValidFrom = version?.EffectiveFrom,
            ValidTo = version?.EffectiveTo,
            PublishedAt = published ? version!.PublishedAt ?? version.EffectiveFrom ?? DateTimeOffset.UtcNow : null
        };
        var surchargeRule = new SurchargeRule
        {
            TenantId = tenantId,
            SurchargeVersionId = surchargeVersion.Id,
            CalculationMode = mode,
            Basis = perGross
                ? "gross_weight"
                : mode == SurchargeCalcModes.UnitRate ? "chargeable_weight" : null,
            CurrencyCode = rule.CurrencyCode,
            RateAmountPercent = rate,
            Priority = rule.SortOrder
        };
        _db.Surcharges.Add(surcharge);
        _db.SurchargeVersions.Add(surchargeVersion);
        _db.SurchargeRules.Add(surchargeRule);
        if (weightTo is decimal cap)
        {
            _db.SurchargeConditions.Add(new SurchargeCondition
            {
                TenantId = tenantId,
                SurchargeRuleId = surchargeRule.Id,
                Dimension = "chargeable_weight",
                Operator = "between",
                ValueFrom = 0m,
                ValueTo = cap
            });
        }

        if (!string.IsNullOrWhiteSpace(rule.DestinationCode))
        {
            _db.SurchargeConditions.Add(new SurchargeCondition
            {
                TenantId = tenantId,
                SurchargeRuleId = surchargeRule.Id,
                Dimension = "destination",
                Operator = "eq",
                ValueText = rule.DestinationCode
            });
        }

        if (version is not null)
        {
            _db.SurchargeScopes.Add(new SurchargeScope
            {
                TenantId = tenantId,
                SurchargeRuleId = surchargeRule.Id,
                RateCardId = version.RateCardId
            });
        }

        if (mode == SurchargeCalcModes.WeightBreak)
        {
            foreach (var band in breaks.Where(b => b.PricingRuleId == rule.Id).OrderBy(b => b.SequenceNo))
            {
                _db.SurchargeBreaks.Add(new SurchargeBreak
                {
                    TenantId = tenantId,
                    SurchargeRuleId = surchargeRule.Id,
                    SequenceNo = band.SequenceNo,
                    MinQuantity = band.MinQuantity,
                    MaxQuantity = band.MaxQuantity,
                    UnitAmount = band.UnitAmount
                });
            }
        }

        return surcharge;
    }

    private static string MapMode(string? calcMethod)
    {
        var mode = CreateSurchargeCommandValidator.NormalizeMode(calcMethod);
        return SurchargeCalcModes.All.Contains(mode) ? mode : SurchargeCalcModes.FixedRate;
    }
}

public static class LegacyComponentClassifier
{
    private static readonly string[] SurchargeTokens =
    [
        "FSC", "SSC", "THC", "BAF", "CAF", "PSS", "AWB", "AMS",
        "FUEL", "SECURITY", "HANDLING", "PEAK", "SURCHARGE", "DG",
        "REMOTE", "DELIVERY"
    ];

    public static bool IsAdditionalCharge(string? code, string? name, string? chargeCode)
    {
        if (!string.IsNullOrWhiteSpace(chargeCode))
        {
            var charge = chargeCode.Trim();
            if (charge.Equals("REMOTE", StringComparison.OrdinalIgnoreCase)
                || charge.Equals("DELIVERY", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return Classify(code ?? "", name ?? "") == LegacyComponentClasses.Surcharge;
    }

    public static string Classify(string code, string name)
    {
        var text = $"{code} {name}".ToUpperInvariant();
        if (IsBase(text))
        {
            return LegacyComponentClasses.Base;
        }

        if (text.Contains("PHỤ PHÍ", StringComparison.Ordinal))
        {
            return LegacyComponentClasses.Surcharge;
        }

        if (SurchargeTokens.Any(token => ContainsToken(text, token)))
        {
            return LegacyComponentClasses.Surcharge;
        }

        return LegacyComponentClasses.Unknown;
    }

    private static bool IsBase(string text) =>
        text.Contains("BASE FREIGHT", StringComparison.Ordinal)
        || text.Contains("BASE HANDLING", StringComparison.Ordinal)
        || text.Contains("BASE_FREIGHT", StringComparison.Ordinal)
        || text.Contains("BASE_HANDLING", StringComparison.Ordinal)
        || text.Contains("OCEAN FREIGHT", StringComparison.Ordinal)
        || text.Contains("AIR FREIGHT", StringComparison.Ordinal)
        || text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(part => part is "FREIGHT" or "BASE");

    private static bool ContainsToken(string text, string token)
    {
        var parts = text.Split([' ', '_', '-', '/'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Contains(token, StringComparer.Ordinal);
    }
}
