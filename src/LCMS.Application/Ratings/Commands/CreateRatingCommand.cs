using FluentValidation;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Ratings.Commands;

public sealed record CreateRatingCommand(
    Guid BillId,
    Guid RateVersionId,
    decimal? Quantity) : IRequest<Guid>;

public sealed class CreateRatingCommandValidator : AbstractValidator<CreateRatingCommand>
{
    public CreateRatingCommandValidator()
    {
        RuleFor(x => x.BillId).NotEmpty().WithMessage("Bill không hợp lệ.");
        RuleFor(x => x.RateVersionId).NotEmpty().WithMessage("Phiên bản bảng giá không hợp lệ.");
        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.")
            .When(x => x.Quantity.HasValue);
    }
}

/// <summary>
/// Thin Expected seed: fixed amount or unit_rate × quantity; snapshot into rating_details (C-011).
/// </summary>
public sealed class CreateRatingCommandHandler : IRequestHandler<CreateRatingCommand, Guid>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public CreateRatingCommandHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Guid> Handle(CreateRatingCommand request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var quantity = request.Quantity ?? 1m;

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

        var rules = await _db.PricingRules
            .AsNoTracking()
            .Where(r => r.RateVersionId == version.Id && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Code)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            throw new ConflictAppException("Phiên bản bảng giá không có quy tắc tính giá hiệu lực.");
        }

        var ruleIds = rules.Select(r => r.Id).ToList();
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
            CurrencyCode = card?.CurrencyCode ?? rules[0].CurrencyCode,
            Quantity = quantity,
            Status = "completed"
        };

        var details = new List<RatingDetail>();
        decimal total = 0m;

        foreach (var rule in rules)
        {
            if (componentsByRule.TryGetValue(rule.Id, out var ruleComponents) && ruleComponents.Count > 0)
            {
                foreach (var component in ruleComponents)
                {
                    var amount = ComputeAmount(rule.CalcMethod, component.Amount, quantity);
                    total += amount;
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
                var amount = ComputeAmount(rule.CalcMethod, rule.UnitAmount, quantity);
                total += amount;
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
        _db.Ratings.Add(rating);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var detail in details)
        {
            detail.RatingId = rating.Id;
        }

        _db.RatingDetails.AddRange(details);
        await _db.SaveChangesAsync(cancellationToken);

        return rating.Id;
    }

    private static decimal ComputeAmount(string calcMethod, decimal unitOrFixed, decimal quantity)
    {
        if (string.Equals(calcMethod, PricingCalcMethods.UnitRate, StringComparison.OrdinalIgnoreCase))
        {
            return decimal.Round(unitOrFixed * quantity, 4, MidpointRounding.AwayFromZero);
        }

        return unitOrFixed;
    }
}
