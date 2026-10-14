using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateCards;

public static class RateCardCatalog
{
    public static void RejectDual(bool hasBasicPayload, bool hasMatrixPayload)
    {
        if (RateDeclarationPolicy.IsDual(hasBasicPayload, hasMatrixPayload))
        {
            throw new ConflictAppException(RateDeclarationPolicy.DualPayloadMessage, "invalid_declaration_mode");
        }
    }

    public static async Task EnsureCurrencyAsync(
        ILcmsDbContext db,
        string code,
        string? unchangedCode,
        CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var unchanged = unchangedCode is not null
            && string.Equals(normalized, unchangedCode.Trim(), StringComparison.OrdinalIgnoreCase);
        var active = unchanged
            || await db.Currencies.AnyAsync(c => c.Code == normalized && c.IsActive, cancellationToken);
        var block = RateDeclarationPolicy.CurrencyBlockReason(active, unchanged);
        if (block is not null)
        {
            throw new ConflictAppException(block, "invalid_currency");
        }
    }

    public static async Task ApplyRouteAsync(
        ILcmsDbContext db,
        RateCard card,
        Guid? routeId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (routeId is null || routeId == Guid.Empty)
        {
            return;
        }

        var route = await db.Routes.FirstOrDefaultAsync(
            r => r.Id == routeId && r.TenantId == tenantId,
            cancellationToken);
        var block = RateDeclarationPolicy.RouteBlockReason(
            route is not null,
            route?.IsActive == true,
            card.RouteId == routeId);
        if (block is not null)
        {
            throw new ConflictAppException(block, "invalid_route");
        }

        card.RouteId = route!.Id;
        card.RouteCode = route.Code;
    }

    public static async Task RetireRulesAsync(
        ILcmsDbContext db,
        IReadOnlyList<PricingRule> rules,
        CancellationToken cancellationToken)
    {
        if (rules.Count == 0)
        {
            return;
        }

        var ids = rules.Select(r => r.Id).ToList();
        var breaks = await db.RateBreaks.Where(b => ids.Contains(b.PricingRuleId)).ToListAsync(cancellationToken);
        var components = await db.PricingRuleComponents.Where(c => ids.Contains(c.PricingRuleId)).ToListAsync(cancellationToken);
        var containers = await db.ContainerRatePrices.Where(c => ids.Contains(c.PricingRuleId)).ToListAsync(cancellationToken);
        foreach (var row in breaks)
        {
            row.SoftDelete(null);
        }

        foreach (var row in components)
        {
            row.SoftDelete(null);
        }

        foreach (var row in containers)
        {
            row.SoftDelete(null);
        }

        foreach (var rule in rules)
        {
            rule.Code = RetiredCode(rule.Code, rule.Id);
            rule.IsActive = false;
            rule.SoftDelete(null);
        }
    }

    public static string RetiredCode(string code, Guid id)
    {
        var suffix = "~" + id.ToString("N")[..8];
        var keep = Math.Max(1, 64 - suffix.Length);
        var head = code.Length <= keep ? code : code[..keep];
        return head + suffix;
    }
}
