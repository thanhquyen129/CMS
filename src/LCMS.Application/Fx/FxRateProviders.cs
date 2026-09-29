using System.Globalization;
using System.Xml.Linq;
using LCMS.Application.Common.Exceptions;

namespace LCMS.Application.Fx;

public sealed record FxProviderQuote(string FromCurrencyCode, string? CurrencyName, decimal Rate);

public sealed record FxProviderSnapshot(
    string ProviderCode,
    string ProviderName,
    string ToCurrencyCode,
    DateOnly RateDate,
    string RateType,
    IReadOnlyList<FxProviderQuote> Quotes);

/// <summary>External FX source (FX-ARCH-05). Errors surface as validation failures — never a fabricated rate.</summary>
public interface IFxRateProvider
{
    /// <summary>Stored as fx_rates.source.</summary>
    string Code { get; }

    string Name { get; }

    Task<FxProviderSnapshot> FetchAsync(CancellationToken cancellationToken);
}

public sealed class VietcombankFxRateProvider : IFxRateProvider
{
    public const string XmlUrl = "https://portal.vietcombank.com.vn/UserControls/TVPortal.TyGia/pXML.aspx";

    public string Code => "vcb";

    public string Name => "Vietcombank";

    public async Task<FxProviderSnapshot> FetchAsync(CancellationToken cancellationToken)
    {
        string xml;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "LCMS/1.0");
            xml = await client.GetStringAsync(XmlUrl, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw Fail($"Không tải được tỷ giá Vietcombank: {ex.Message}");
        }

        return Parse(xml);
    }

    public static FxProviderSnapshot Parse(string xml)
    {
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
        if (!string.IsNullOrWhiteSpace(dateAttr)
            && (DateTime.TryParse(dateAttr, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out var parsed)
                || DateTime.TryParse(dateAttr, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)))
        {
            rateDate = DateOnly.FromDateTime(parsed);
        }

        var quotes = new List<FxProviderQuote>();
        foreach (var node in doc.Descendants("Exrate"))
        {
            var code = (node.Attribute("CurrencyCode")?.Value ?? "").Trim().ToUpperInvariant();
            if (code.Length != 3 || code is "VND")
            {
                continue;
            }

            var raw = node.Attribute("Transfer")?.Value
                ?? node.Attribute("Sell")?.Value
                ?? node.Attribute("Buy")?.Value;
            if (string.IsNullOrWhiteSpace(raw) || raw is "-")
            {
                continue;
            }

            var normalized = raw.Replace(",", "").Trim();
            if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate)
                || rate <= 0)
            {
                continue;
            }

            quotes.Add(new FxProviderQuote(
                code,
                node.Attribute("CurrencyName")?.Value?.Trim(),
                decimal.Round(rate, 8, MidpointRounding.AwayFromZero)));
        }

        return new FxProviderSnapshot("vcb", "Vietcombank", "VND", rateDate, "transfer", quotes);
    }

    private static ValidationAppException Fail(string message) =>
        new(new Dictionary<string, string[]> { ["vcb"] = [message] });
}
