using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Currencies;

/// <summary>Seeds baseline ISO currencies used by logistics finance (VND/USD/EUR).</summary>
public static class CurrencyCatalogSeeder
{
    public static readonly (string Code, string Name, int DecimalPlaces)[] Baseline =
    [
        ("VND", "Đồng Việt Nam", 0),
        ("USD", "Đô la Mỹ", 2),
        ("EUR", "Euro", 2)
    ];

    public static async Task EnsureBaselineAsync(ILcmsDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.Currencies.Select(c => c.Code).ToListAsync(cancellationToken);
        var set = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, name, decimals) in Baseline)
        {
            if (set.Contains(code))
            {
                continue;
            }

            db.Currencies.Add(new Currency
            {
                Code = code,
                Name = name,
                DecimalPlaces = decimals,
                IsActive = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
