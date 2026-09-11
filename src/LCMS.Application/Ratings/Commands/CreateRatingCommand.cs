using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Costs.Commands;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings.Commands;

public sealed record CreateRatingCommand(
    Guid BillId,
    Guid RateVersionId,
    decimal? Quantity,
    decimal? Weight,
    string? ServiceTypeCode,
    string? PartyTypeCode,
    string? RouteCode,
    decimal? BaseAmount,
    Guid? SupersedesRatingId,
    bool SeedExpectedCosts = false) : IRequest<Guid>;

public sealed class CreateRatingCommandValidator : AbstractValidator<CreateRatingCommand>
{
    public CreateRatingCommandValidator()
    {
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.RateVersionId).NotEmpty().WithMessage("Phiên bản bảng giá không hợp lệ.");
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.")
            .When(x => x.Quantity.HasValue);
        RuleFor(x => x.Weight)
            .GreaterThan(0).WithMessage("Trọng lượng phải lớn hơn 0.")
            .When(x => x.Weight.HasValue);
        RuleFor(x => x.BaseAmount)
            .GreaterThanOrEqualTo(0).WithMessage("Số tiền cơ sở không được âm.")
            .When(x => x.BaseAmount.HasValue);
        RuleFor(x => x.ServiceTypeCode)
            .MaximumLength(64)
            .When(x => x.ServiceTypeCode is not null);
        RuleFor(x => x.PartyTypeCode)
            .MaximumLength(32)
            .When(x => x.PartyTypeCode is not null);
        RuleFor(x => x.RouteCode)
            .MaximumLength(64)
            .When(x => x.RouteCode is not null);
    }
}

/// <summary>
/// Rate Bill against published version: applicability filter + formula types + optional re-rate / seed.
/// </summary>
public sealed class CreateRatingCommandHandler : IRequestHandler<CreateRatingCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ISender _sender;

    public CreateRatingCommandHandler(ILcmsDbContext db, ITenantContext tenantContext, ISender sender)
    {
        _db = db;
        _tenantContext = tenantContext;
        _sender = sender;
    }

    public async Task<Guid> Handle(CreateRatingCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var quantity = request.Quantity ?? request.Weight ?? 1m;

        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        var version = await _db.RateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.RateVersionId, cancellationToken);
        if (version is null)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        if (!version.IsPublished)
        {
            throw new ConflictAppException("Chỉ được tính giá trên phiên bản bảng giá đã phát hành.");
        }

        var card = await _db.RateCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == version.RateCardId, cancellationToken);

        var serviceType = Normalize(request.ServiceTypeCode) ?? Normalize(bill.BillType);
        var partyType = Normalize(request.PartyTypeCode) ?? Normalize(card?.PartyType);
        var routeCode = Normalize(request.RouteCode);

        Rating? prior = null;
        if (request.SupersedesRatingId.HasValue)
        {
            prior = await _db.Ratings.FirstOrDefaultAsync(
                r => r.Id == request.SupersedesRatingId.Value,
                cancellationToken);
            if (prior is null)
            {
                throw new NotFoundAppException("Không tìm thấy lần tính giá cần thay thế.");
            }

            if (prior.BillId != bill.Id)
            {
                throw new ConflictAppException("Lần tính giá thay thế phải thuộc cùng Bill.");
            }

            if (string.Equals(prior.Status, RatingStatuses.Superseded, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictAppException("Lần tính giá đã bị thay thế; không được ghi đè lịch sử.");
            }
        }

        var rules = await _db.PricingRules
            .AsNoTracking()
            .Where(r => r.RateVersionId == version.Id && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .ToListAsync(cancellationToken);

        var applicable = rules
            .Where(r => MatchesApplicability(r, serviceType, partyType, routeCode))
            .ToList();

        if (applicable.Count == 0)
        {
            throw new ConflictAppException("Không có quy tắc tính giá phù hợp với điều kiện áp dụng.");
        }

        var ruleIds = applicable.Select(r => r.Id).ToList();
        var components = await _db.PricingRuleComponents
            .AsNoTracking()
            .Where(c => ruleIds.Contains(c.PricingRuleId))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Code)
            .ToListAsync(cancellationToken);

        var componentsByRule = components
            .GroupBy(c => c.PricingRuleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rating = new Rating
        {
            TenantId = tenantId,
            BillId = bill.Id,
            RateVersionId = version.Id,
            RatedAt = DateTimeOffset.UtcNow,
            CurrencyCode = card?.CurrencyCode ?? applicable[0].CurrencyCode,
            Quantity = quantity,
            Weight = request.Weight,
            ServiceTypeCode = serviceType,
            PartyTypeCode = partyType,
            RouteCode = routeCode,
            BaseAmount = request.BaseAmount,
            Status = RatingStatuses.Completed,
            SupersedesRatingId = prior?.Id
        };

        var details = new List<RatingDetail>();
        decimal total = 0m;
        decimal runningBase = request.BaseAmount ?? 0m;
        var hasExplicitBase = request.BaseAmount.HasValue;

        foreach (var rule in applicable)
        {
            if (componentsByRule.TryGetValue(rule.Id, out var ruleComponents) && ruleComponents.Count > 0)
            {
                foreach (var component in ruleComponents)
                {
                    var amount = ComputeAmount(
                        rule.CalcMethod,
                        component.Amount,
                        quantity,
                        hasExplicitBase ? request.BaseAmount!.Value : runningBase,
                        rule.MinAmount,
                        rule.MaxAmount);
                    total += amount;
                    if (!hasExplicitBase)
                    {
                        runningBase += amount;
                    }

                    details.Add(new RatingDetail
                    {
                        TenantId = tenantId,
                        PricingRuleId = rule.Id,
                        PricingRuleComponentId = component.Id,
                        RuleCode = rule.Code,
                        ComponentCode = component.Code,
                        ComponentName = component.Name,
                        FinancialNature = component.FinancialNature,
                        FinancialMaturity = "expected",
                        Amount = amount,
                        CurrencyCode = component.CurrencyCode
                    });
                }
            }
            else
            {
                var amount = ComputeAmount(
                    rule.CalcMethod,
                    rule.UnitAmount,
                    quantity,
                    hasExplicitBase ? request.BaseAmount!.Value : runningBase,
                    rule.MinAmount,
                    rule.MaxAmount);
                total += amount;
                if (!hasExplicitBase)
                {
                    runningBase += amount;
                }

                details.Add(new RatingDetail
                {
                    TenantId = tenantId,
                    PricingRuleId = rule.Id,
                    PricingRuleComponentId = null,
                    RuleCode = rule.Code,
                    ComponentCode = rule.Code,
                    ComponentName = rule.Name,
                    FinancialNature = "cost",
                    FinancialMaturity = "expected",
                    Amount = amount,
                    CurrencyCode = rule.CurrencyCode
                });
            }
        }

        rating.TotalAmount = total;

        if (prior is not null)
        {
            // Mark prior superseded; never mutate its details (C-011 history).
            prior.Status = RatingStatuses.Superseded;
        }

        _db.Ratings.Add(rating);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var detail in details)
        {
            detail.RatingId = rating.Id;
        }

        _db.RatingDetails.AddRange(details);
        await _db.SaveChangesAsync(cancellationToken);

        if (request.SeedExpectedCosts)
        {
            await _sender.Send(new SeedExpectedCostsFromRatingCommand(rating.Id), cancellationToken);
        }

        return rating.Id;
    }

    internal static bool MatchesApplicability(
        PricingRule rule,
        string? serviceType,
        string? partyType,
        string? routeCode)
    {
        if (!string.IsNullOrWhiteSpace(rule.ServiceTypeCode)
            && !string.Equals(rule.ServiceTypeCode, serviceType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.PartyTypeCode)
            && !string.Equals(rule.PartyTypeCode, partyType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.RouteCode)
            && !string.Equals(rule.RouteCode, routeCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    internal static decimal ComputeAmount(
        string calcMethod,
        decimal unitOrFixed,
        decimal quantity,
        decimal baseAmount,
        decimal? minAmount,
        decimal? maxAmount)
    {
        decimal raw;
        if (string.Equals(calcMethod, PricingCalcMethods.UnitRate, StringComparison.OrdinalIgnoreCase))
        {
            raw = unitOrFixed * quantity;
        }
        else if (string.Equals(calcMethod, PricingCalcMethods.PercentOfBase, StringComparison.OrdinalIgnoreCase))
        {
            raw = baseAmount * unitOrFixed / 100m;
        }
        else if (string.Equals(calcMethod, PricingCalcMethods.MinMaxClamp, StringComparison.OrdinalIgnoreCase))
        {
            raw = unitOrFixed * quantity;
            raw = Clamp(raw, minAmount, maxAmount);
            return decimal.Round(raw, 4, MidpointRounding.AwayFromZero);
        }
        else
        {
            // fixed (default)
            raw = unitOrFixed;
        }

        return decimal.Round(raw, 4, MidpointRounding.AwayFromZero);
    }

    private static decimal Clamp(decimal value, decimal? min, decimal? max)
    {
        if (min.HasValue && value < min.Value)
        {
            value = min.Value;
        }

        if (max.HasValue && value > max.Value)
        {
            value = max.Value;
        }

        return value;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
