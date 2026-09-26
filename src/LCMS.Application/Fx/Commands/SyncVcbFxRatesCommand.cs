using System.Globalization;
using System.Xml.Linq;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Currencies;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Fx.Commands;

public sealed record SyncVcbFxRatesResult(int Upserted, DateOnly RateDate, string? Note);

/// <summary>
/// Pulls Vietcombank public FX XML and upserts foreign→VND transfer rates.
/// Ensures popular currency rows exist first.
/// </summary>
public sealed record SyncVcbFxRatesCommand : IRequest<SyncVcbFxRatesResult>;

public sealed class SyncVcbFxRatesCommandHandler
    : IRequestHandler<SyncVcbFxRatesCommand, SyncVcbFxRatesResult>
{
    public const string VcbXmlUrl =
        "https://portal.vietcombank.com.vn/UserControls/TVPortal.TyGia/pXML.aspx";

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public SyncVcbFxRatesCommandHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
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

        await CurrencyCatalogSeeder.EnsureBaselineAsync(_db, cancellationToken);

        string xml;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "LCMS/1.0");
            xml = await client.GetStringAsync(VcbXmlUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            throw Fail($"Không tải được tỷ giá Vietcombank: {ex.Message}");
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (Exception ex)
        {
            throw Fail($"Phản hồi tỷ giá VCB không hợp lệ: {ex.Message}");
        }

        var rateDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var dateAttr = doc.Root?.Element("DateTime")?.Value
            ?? doc.Root?.Attribute("DateTime")?.Value;
        if (!string.IsNullOrWhiteSpace(dateAttr))
        {
            if (DateTime.TryParse(dateAttr, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out var parsed)
                || DateTime.TryParse(dateAttr, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            {
                rateDate = DateOnly.FromDateTime(parsed);
            }
        }

        var upserted = 0;
        var existingCodes = await _db.Currencies.AsNoTracking()
            .Select(c => c.Code)
            .ToListAsync(cancellationToken);
        var codeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var node in doc.Descendants("Exrate"))
        {
            var code = (node.Attribute("CurrencyCode")?.Value ?? "").Trim().ToUpperInvariant();
            if (code.Length != 3 || code is "VND")
            {
                continue;
            }

            var transferRaw = node.Attribute("Transfer")?.Value
                ?? node.Attribute("Sell")?.Value
                ?? node.Attribute("Buy")?.Value;
            if (string.IsNullOrWhiteSpace(transferRaw) || transferRaw is "-")
            {
                continue;
            }

            var normalized = transferRaw.Replace(",", "").Trim();
            if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate)
                || rate <= 0)
            {
                continue;
            }

            if (!codeSet.Contains(code))
            {
                var name = node.Attribute("CurrencyName")?.Value?.Trim();
                _db.Currencies.Add(new Currency
                {
                    Code = code,
                    Name = string.IsNullOrWhiteSpace(name) ? code : name!,
                    DecimalPlaces = 2,
                    IsActive = true
                });
                codeSet.Add(code);
            }

            rate = decimal.Round(rate, 8, MidpointRounding.AwayFromZero);
            var existing = await _db.FxRates.FirstOrDefaultAsync(
                r => r.FromCurrencyCode == code
                     && r.ToCurrencyCode == "VND"
                     && r.RateDate == rateDate
                     && r.Version == 1,
                cancellationToken);

            if (existing is not null)
            {
                existing.Rate = rate;
                existing.Source = FxRateSources.Vcb;
                existing.Note = "Đồng bộ Vietcombank";
            }
            else
            {
                _db.FxRates.Add(new FxRate
                {
                    TenantId = _tenantContext.TenantId!.Value,
                    FromCurrencyCode = code,
                    ToCurrencyCode = "VND",
                    RateDate = rateDate,
                    Rate = rate,
                    Source = FxRateSources.Vcb,
                    Version = 1,
                    Note = "Đồng bộ Vietcombank"
                });
            }

            upserted++;
        }

        if (upserted == 0)
        {
            throw Fail("VCB không trả về tỷ giá nào để ghi.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new SyncVcbFxRatesResult(upserted, rateDate, "Nguồn: Vietcombank (chuyển khoản → VND)");
    }

    private static ValidationAppException Fail(string message) =>
        new(new Dictionary<string, string[]> { ["vcb"] = [message] });
}
