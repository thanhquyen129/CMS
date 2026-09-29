using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Currencies;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Fx.Commands;

public sealed record SyncVcbFxRatesResult(int Upserted, DateOnly RateDate, string? Note);

/// <summary>
/// Pulls the Vietcombank provider (IFxRateProvider "vcb") and stores foreign→VND transfer rates.
/// A changed rate for the same date becomes a new version; existing rows are never overwritten.
/// </summary>
public sealed record SyncVcbFxRatesCommand : IRequest<SyncVcbFxRatesResult>;

public sealed class SyncVcbFxRatesCommandHandler
    : IRequestHandler<SyncVcbFxRatesCommand, SyncVcbFxRatesResult>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;
    private readonly IEnumerable<IFxRateProvider> _providers;

    public SyncVcbFxRatesCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions,
        IEnumerable<IFxRateProvider> providers)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
        _providers = providers;
    }

    public async Task<SyncVcbFxRatesResult> Handle(
        SyncVcbFxRatesCommand request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.MasterCurrencyManage,
            "Bạn không có quyền cập nhật tỷ giá.",
            cancellationToken);

        var provider = _providers.FirstOrDefault(p => p.Code == FxRateSources.Vcb)
            ?? throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["vcb"] = ["Chưa cấu hình nguồn tỷ giá Vietcombank."]
            });

        await CurrencyCatalogSeeder.EnsureBaselineAsync(_db, cancellationToken);
        var snapshot = await provider.FetchAsync(cancellationToken);
        var to = snapshot.ToCurrencyCode;
        var rateDate = snapshot.RateDate;

        var existingCodes = await _db.Currencies.AsNoTracking()
            .Select(c => c.Code)
            .ToListAsync(cancellationToken);
        var codeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sameDay = await _db.FxRates
            .Where(r => r.ToCurrencyCode == to && r.RateDate == rateDate)
            .ToListAsync(cancellationToken);

        var upserted = 0;
        foreach (var quote in snapshot.Quotes)
        {
            var code = quote.FromCurrencyCode;
            if (!codeSet.Contains(code))
            {
                _db.Currencies.Add(new Currency
                {
                    Code = code,
                    Name = string.IsNullOrWhiteSpace(quote.CurrencyName) ? code : quote.CurrencyName!,
                    DecimalPlaces = 2,
                    IsActive = true
                });
                codeSet.Add(code);
            }

            var latest = sameDay
                .Where(r => r.FromCurrencyCode == code)
                .OrderByDescending(r => r.Version)
                .FirstOrDefault();
            if (latest is not null && latest.Rate == quote.Rate && latest.Source == provider.Code)
            {
                upserted++;
                continue;
            }

            _db.FxRates.Add(new FxRate
            {
                TenantId = _tenantContext.TenantId!.Value,
                FromCurrencyCode = code,
                ToCurrencyCode = to,
                RateDate = rateDate,
                Rate = quote.Rate,
                Source = provider.Code,
                Version = (latest?.Version ?? 0) + 1,
                Note = $"Đồng bộ {provider.Name} ({snapshot.RateType})"
            });
            upserted++;
        }

        if (upserted == 0)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["vcb"] = ["VCB không trả về tỷ giá nào để ghi."]
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new SyncVcbFxRatesResult(upserted, rateDate, $"Nguồn: {provider.Name} (chuyển khoản → {to})");
    }
}
