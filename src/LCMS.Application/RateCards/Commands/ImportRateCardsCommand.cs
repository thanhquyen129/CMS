using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Ratings;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateCards.Commands;

public sealed record RateImportBreak(int SequenceNo, decimal MinQuantity, decimal? MaxQuantity, decimal UnitAmount);

public sealed record RateImportComponent(
    string Code,
    string Name,
    string? FinancialNature,
    decimal Amount,
    string? CurrencyCode,
    string? CalcMethod,
    string? DependsOnCode,
    int? SortOrder);

public sealed record RateImportContainer(string ContainerType, decimal UnitAmount);

public sealed record RateImportRule(
    string Code,
    string Name,
    string CalcMethod,
    decimal UnitAmount,
    string? CurrencyCode,
    string? ChargeCode,
    decimal? MinAmount,
    decimal? RoundingStep,
    decimal? VolumetricFactor,
    int? SortOrder,
    IReadOnlyList<RateImportBreak>? Breaks,
    IReadOnlyList<RateImportComponent>? Components,
    IReadOnlyList<RateImportContainer>? Containers);

public sealed record RateImportCard(
    string Code,
    string Name,
    string PartyType,
    string CurrencyCode,
    string? TransportMode,
    string? RouteCode,
    string? CarrierName,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Note,
    IReadOnlyList<RateImportRule>? Rules);

public sealed record RateImportIssue(int Row, string Field, string Message);

public sealed record RateImportPreview(bool CanCommit, IReadOnlyList<RateImportIssue> Issues);

public sealed record ImportRateCardsRequest(IReadOnlyList<RateImportCard> Cards);

public sealed record PreviewRateImportCommand(IReadOnlyList<RateImportCard> Cards) : IRequest<RateImportPreview>;

public sealed record CommitRateImportCommand(IReadOnlyList<RateImportCard> Cards) : IRequest<int>;

public sealed class PreviewRateImportCommandHandler : IRequestHandler<PreviewRateImportCommand, RateImportPreview>
{
    private readonly RateImportBatch _batch;

    public PreviewRateImportCommandHandler(RateImportBatch batch) => _batch = batch;

    public async Task<RateImportPreview> Handle(PreviewRateImportCommand request, CancellationToken cancellationToken)
    {
        var issues = await _batch.ValidateAsync(request.Cards, cancellationToken);
        return new RateImportPreview(issues.Count == 0, issues);
    }
}

public sealed class CommitRateImportCommandHandler : IRequestHandler<CommitRateImportCommand, int>
{
    private readonly RateImportBatch _batch;

    public CommitRateImportCommandHandler(RateImportBatch batch) => _batch = batch;

    public Task<int> Handle(CommitRateImportCommand request, CancellationToken cancellationToken) =>
        _batch.CommitAsync(request.Cards, cancellationToken);
}

/// <summary>Preview then all-or-nothing insert of draft rate cards. Published history is not rewritten.</summary>
public sealed class RateImportBatch
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public RateImportBatch(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<RateImportIssue>> ValidateAsync(
        IReadOnlyList<RateImportCard> cards,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var issues = new List<RateImportIssue>();
        if (cards is null || cards.Count == 0)
        {
            issues.Add(new RateImportIssue(0, "cards", "File không có bảng giá."));
            return issues;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existing = await _db.RateCards.AsNoTracking().Select(c => c.Code).ToListAsync(cancellationToken);
        var owned = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < cards.Count; i++)
        {
            var row = i + 1;
            var card = cards[i];
            if (string.IsNullOrWhiteSpace(card.Code))
            {
                issues.Add(new RateImportIssue(row, "code", "Thiếu mã bảng giá."));
            }
            else if (!seen.Add(card.Code.Trim()) || owned.Contains(card.Code.Trim()))
            {
                issues.Add(new RateImportIssue(row, "code", "Mã bảng giá bị trùng."));
            }

            if (string.IsNullOrWhiteSpace(card.Name))
            {
                issues.Add(new RateImportIssue(row, "name", "Thiếu tên bảng giá."));
            }

            var party = card.PartyType?.Trim().ToLowerInvariant();
            if (party is not ("customer" or "vendor"))
            {
                issues.Add(new RateImportIssue(row, "partyType", "Loại giá phải là giá mua (vendor) hoặc giá bán (customer)."));
            }

            if (string.IsNullOrWhiteSpace(card.CurrencyCode) || card.CurrencyCode.Trim().Length != 3)
            {
                issues.Add(new RateImportIssue(row, "currencyCode", "Mã tiền tệ phải gồm 3 ký tự."));
            }

            var rules = card.Rules ?? [];
            var ruleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.Code) || !ruleCodes.Add(rule.Code.Trim()))
                {
                    issues.Add(new RateImportIssue(row, "rule", "Mã quy tắc trống hoặc trùng trong cùng bảng giá."));
                }

                if (!PricingCalcMethods.All.Contains(rule.CalcMethod ?? ""))
                {
                    issues.Add(new RateImportIssue(row, "calcMethod", "Phương pháp tính giá không hợp lệ."));
                }

                var sequences = new HashSet<int>();
                var spans = new List<(decimal Min, decimal Max)>();
                foreach (var band in rule.Breaks ?? [])
                {
                    if (!sequences.Add(band.SequenceNo))
                    {
                        issues.Add(new RateImportIssue(row, "breaks", "Bậc trọng lượng bị trùng hoặc chồng khoảng."));
                    }

                    var max = band.MaxQuantity ?? decimal.MaxValue;
                    if (band.MinQuantity > max || spans.Any(s => band.MinQuantity <= s.Max && s.Min <= max))
                    {
                        issues.Add(new RateImportIssue(row, "breaks", "Bậc trọng lượng bị trùng hoặc chồng khoảng."));
                    }

                    spans.Add((band.MinQuantity, max));
                }

                if (string.Equals(rule.CalcMethod, PricingCalcMethods.Composite, StringComparison.OrdinalIgnoreCase))
                {
                    var parts = (rule.Components ?? []).Select(c => new PricingRuleComponent
                    {
                        Code = c.Code ?? "",
                        DependsOnCode = c.DependsOnCode
                    }).ToList();
                    try
                    {
                        if (parts.Count == 0)
                        {
                            issues.Add(new RateImportIssue(row, "components", "COMPOSITE: Quy tắc thiếu thành phần giá."));
                        }
                        else
                        {
                            RatingEngine.OrderComponents(parts);
                        }
                    }
                    catch (ConflictAppException ex)
                    {
                        issues.Add(new RateImportIssue(row, "components", ex.Message));
                    }
                }
            }
        }

        return issues;
    }

    public async Task<int> CommitAsync(IReadOnlyList<RateImportCard> cards, CancellationToken cancellationToken)
    {
        var issues = await ValidateAsync(cards, cancellationToken);
        if (issues.Count > 0)
        {
            throw new ConflictAppException(issues[0].Message);
        }

        var tenantId = _tenant.TenantId!.Value;
        foreach (var card in cards)
        {
            var entity = new RateCard
            {
                TenantId = tenantId,
                Code = card.Code.Trim(),
                Name = card.Name.Trim(),
                PartyType = card.PartyType.Trim().ToLowerInvariant(),
                CurrencyCode = card.CurrencyCode.Trim().ToUpperInvariant(),
                TransportMode = Trim(card.TransportMode),
                RouteCode = Trim(card.RouteCode),
                CarrierName = Trim(card.CarrierName),
                IsActive = true
            };
            var version = new RateVersion
            {
                TenantId = tenantId,
                RateCardId = entity.Id,
                VersionNo = 1,
                Status = RateVersionStatuses.Draft,
                EffectiveFrom = card.EffectiveFrom,
                EffectiveTo = card.EffectiveTo,
                Note = Trim(card.Note)
            };
            _db.RateCards.Add(entity);
            _db.RateVersions.Add(version);
            var sort = 1;
            foreach (var rule in card.Rules ?? [])
            {
                var pricing = new PricingRule
                {
                    TenantId = tenantId,
                    RateVersionId = version.Id,
                    Code = rule.Code.Trim(),
                    Name = string.IsNullOrWhiteSpace(rule.Name) ? rule.Code.Trim() : rule.Name.Trim(),
                    CalcMethod = rule.CalcMethod.Trim().ToLowerInvariant(),
                    UnitAmount = rule.UnitAmount,
                    CurrencyCode = string.IsNullOrWhiteSpace(rule.CurrencyCode)
                        ? entity.CurrencyCode
                        : rule.CurrencyCode.Trim().ToUpperInvariant(),
                    ChargeCode = Trim(rule.ChargeCode),
                    MinAmount = rule.MinAmount,
                    RoundingStep = rule.RoundingStep,
                    VolumetricFactor = rule.VolumetricFactor,
                    SortOrder = rule.SortOrder ?? sort
                };
                sort++;
                _db.PricingRules.Add(pricing);
                foreach (var band in rule.Breaks ?? [])
                {
                    _db.RateBreaks.Add(new RateBreak
                    {
                        TenantId = tenantId,
                        PricingRuleId = pricing.Id,
                        SequenceNo = band.SequenceNo,
                        MinQuantity = band.MinQuantity,
                        MaxQuantity = band.MaxQuantity,
                        UnitAmount = band.UnitAmount
                    });
                }

                var componentSort = 1;
                foreach (var component in rule.Components ?? [])
                {
                    _db.PricingRuleComponents.Add(new PricingRuleComponent
                    {
                        TenantId = tenantId,
                        PricingRuleId = pricing.Id,
                        Code = component.Code.Trim(),
                        Name = string.IsNullOrWhiteSpace(component.Name) ? component.Code.Trim() : component.Name.Trim(),
                        FinancialNature = string.IsNullOrWhiteSpace(component.FinancialNature) ? "cost" : component.FinancialNature.Trim().ToLowerInvariant(),
                        Amount = component.Amount,
                        CurrencyCode = string.IsNullOrWhiteSpace(component.CurrencyCode) ? entity.CurrencyCode : component.CurrencyCode.Trim().ToUpperInvariant(),
                        CalcMethod = Trim(component.CalcMethod)?.ToLowerInvariant(),
                        DependsOnCode = Trim(component.DependsOnCode),
                        SortOrder = component.SortOrder ?? componentSort++
                    });
                }

                foreach (var box in rule.Containers ?? [])
                {
                    _db.ContainerRatePrices.Add(new ContainerRatePrice
                    {
                        TenantId = tenantId,
                        PricingRuleId = pricing.Id,
                        ContainerType = box.ContainerType.Trim().ToUpperInvariant(),
                        UnitAmount = box.UnitAmount
                    });
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return cards.Count;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
