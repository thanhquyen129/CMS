using LCMS.Domain.Entities;
using LCMS.Domain.Pricing;

namespace LCMS.Application.Pricing;

public static class ChargeLineSnapshot
{
    public static void ApplyMoney(RatingDetail detail)
    {
        var (net, vat, gross) = DeclaredVat.Split(detail.Amount, detail.VatRate);
        detail.Amount = net;
        detail.NetAmount = net;
        detail.VatAmount = vat;
        detail.GrossAmount = gross;
    }

    public static void ApplyMoney(Cost cost, decimal net, decimal? vatRate)
    {
        var (rounded, vat, gross) = DeclaredVat.Split(net, vatRate);
        cost.NetAmount = rounded;
        cost.VatRate = vatRate;
        cost.VatAmount = vat;
        cost.GrossAmount = gross;
    }

    public static void ApplyMoney(Revenue revenue, decimal net, decimal? vatRate)
    {
        var (rounded, vat, gross) = DeclaredVat.Split(net, vatRate);
        revenue.NetAmount = rounded;
        revenue.VatRate = vatRate;
        revenue.VatAmount = vat;
        revenue.GrossAmount = gross;
    }

    public static Guid? Match(
        string? code,
        IReadOnlyDictionary<string, Guid> catalogByCode,
        IReadOnlyDictionary<string, Guid> mappingByKey)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var key = code.Trim().ToUpperInvariant();
        if (mappingByKey.TryGetValue(key, out var mapped))
        {
            return mapped;
        }

        return catalogByCode.TryGetValue(key, out var catalog) ? catalog : null;
    }

    public static string MapKey(string kind, string code) =>
        $"{kind.Trim().ToLowerInvariant()}:{code.Trim().ToUpperInvariant()}";
}
