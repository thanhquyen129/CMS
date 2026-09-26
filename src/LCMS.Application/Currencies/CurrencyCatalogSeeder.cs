using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Currencies;

/// <summary>Seeds popular ISO currencies used by Vietnam logistics finance.</summary>
public static class CurrencyCatalogSeeder
{
    public static readonly (string Code, string Name, int DecimalPlaces)[] Baseline =
    [
        ("VND", "Đồng Việt Nam", 0),
        ("USD", "Đô la Mỹ", 2),
        ("EUR", "Euro", 2),
        ("CNY", "Nhân dân tệ", 2),
        ("JPY", "Yên Nhật", 0),
        ("KRW", "Won Hàn Quốc", 0),
        ("SGD", "Đô la Singapore", 2),
        ("THB", "Baht Thái", 2),
        ("GBP", "Bảng Anh", 2),
        ("AUD", "Đô la Úc", 2),
        ("HKD", "Đô la Hồng Kông", 2),
        ("TWD", "Đô la Đài Loan", 2),
        ("CHF", "Franc Thụy Sĩ", 2),
        ("CAD", "Đô la Canada", 2),
        ("MYR", "Ringgit Malaysia", 2)
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
