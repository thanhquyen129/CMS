using System.Text.Json;
using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Tenancy;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Fx;

/// <summary>Config fallback rates (dev/test only; production disables it). Traced as a POLICY source.</summary>
public sealed record FxStubPolicy(
    string BaseCurrency,
    bool Allowed,
    IReadOnlyDictionary<string, decimal>? Rates,
    string ConfigKey);

/// <summary>User-entered rate on a financial record; null Rate means "use provider/book".</summary>
public sealed record FxManualInput(decimal? Rate, string? Reason)
{
    public bool HasRate => Rate.HasValue;
}

public sealed record FxResolveRequest(
    string CurrencyCode,
    DateOnly AsOf,
    decimal? ManualRate = null,
    string? OverrideReason = null,
    FxStubPolicy? Stub = null);

public sealed record FxSnapshotResult(
    string CurrencyCode,
    string ReportingCurrencyCode,
    decimal Rate,
    string SourceType,
    string? SourceName,
    DateOnly? RateDate,
    Guid? FxRateId,
    string? OverrideReason,
    decimal? ProviderRate);

/// <summary>Preview for forms (FX-UI-01/02/03): never fabricates a rate when none exists.</summary>
public sealed record FxPreviewDto(
    string CurrencyCode,
    string ReportingCurrencyCode,
    bool SameCurrency,
    decimal? Rate,
    string? SourceType,
    string? SourceName,
    DateOnly? RateDate,
    decimal? Amount,
    decimal? ReportingAmount,
    bool RateAvailable,
    bool CanOverride,
    bool ManualAllowed,
    bool OverrideReasonRequired,
    string? Message);

public interface IReportingCurrencyProvider
{
    Task<string> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Sync accessor for legacy sync call-sites; cached per scope.</summary>
    string Get();
}

public sealed class ReportingCurrencyProvider : IReportingCurrencyProvider
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private string? _cached;

    public ReportingCurrencyProvider(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<string> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        if (!_tenant.HasTenant)
        {
            return TenantDefaults.CurrencyCode;
        }

        var tenantId = _tenant.TenantId!.Value;
        var code = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.DefaultCurrencyCode)
            .FirstOrDefaultAsync(cancellationToken);
        _cached = Normalize(code);
        return _cached;
    }

    public string Get()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        if (!_tenant.HasTenant)
        {
            return TenantDefaults.CurrencyCode;
        }

        var tenantId = _tenant.TenantId!.Value;
        var code = _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.DefaultCurrencyCode)
            .FirstOrDefault();
        _cached = Normalize(code);
        return _cached;
    }

    private static string Normalize(string? code) =>
        string.IsNullOrWhiteSpace(code) ? TenantDefaults.CurrencyCode : code.Trim().ToUpperInvariant();
}

public interface IFxSnapshotService
{
    Task<string> GetReportingCurrencyAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves the FX snapshot or throws (FX-MISS-01: never 0, never a silent default).</summary>
    Task<FxSnapshotResult> ResolveAsync(FxResolveRequest request, CancellationToken cancellationToken = default);

    Task<FxPreviewDto> PreviewAsync(
        string currencyCode,
        DateOnly asOf,
        decimal? amount,
        FxStubPolicy? stub,
        CancellationToken cancellationToken = default);

    /// <summary>Writes the snapshot onto the record and audits manual/override (FX-ARCH-06).</summary>
    void Apply(IReportingFx target, FxSnapshotResult snapshot, string objectType, Guid objectId);

    /// <summary>Snapshot currently stored on the record when still valid for its currency.</summary>
    FxSnapshotResult? FromRecord(IReportingFx record);
}

public sealed class FxSnapshotService : IFxSnapshotService
{
    private readonly IFxRateLookup _lookup;
    private readonly IReportingCurrencyProvider _reporting;
    private readonly IPermissionService _permissions;
    private readonly ITenantSettingsService _settings;
    private readonly ICurrentUserContext _user;
    private readonly IAuditWriter _audit;

    public FxSnapshotService(
        IFxRateLookup lookup,
        IReportingCurrencyProvider reporting,
        IPermissionService permissions,
        ITenantSettingsService settings,
        ICurrentUserContext user,
        IAuditWriter audit)
    {
        _lookup = lookup;
        _reporting = reporting;
        _permissions = permissions;
        _settings = settings;
        _user = user;
        _audit = audit;
    }

    public Task<string> GetReportingCurrencyAsync(CancellationToken cancellationToken = default) =>
        _reporting.GetAsync(cancellationToken);

    public async Task<FxSnapshotResult> ResolveAsync(
        FxResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        var currency = request.CurrencyCode.Trim().ToUpperInvariant();
        var reporting = await _reporting.GetAsync(cancellationToken);
        if (string.Equals(currency, reporting, StringComparison.OrdinalIgnoreCase))
        {
            return new FxSnapshotResult(currency, reporting, 1m, FxSourceTypes.Identity, null, null, null, null, null);
        }

        var book = await ResolveBookAsync(currency, reporting, request.AsOf, request.Stub, cancellationToken);
        var manual = request.ManualRate;
        if (manual is null || (book is not null && book.Rate == decimal.Round(manual.Value, 8)))
        {
            if (book is not null)
            {
                return book;
            }

            throw MissingRate(currency, reporting, request.AsOf, request.Stub);
        }

        if (manual.Value <= 0)
        {
            throw Invalid("FxRate", "Tỷ giá phải lớn hơn 0.");
        }

        await _permissions.EnsureAsync(
            PermissionCodes.FxOverride,
            "Bạn không có quyền nhập hoặc ghi đè tỷ giá.",
            cancellationToken);
        var policy = await _settings.GetFinancialAsync(cancellationToken);
        var reason = string.IsNullOrWhiteSpace(request.OverrideReason) ? null : request.OverrideReason.Trim();
        var rate = decimal.Round(manual.Value, 8, MidpointRounding.AwayFromZero);

        if (book is null)
        {
            if (policy.FxManualAllowed == false)
            {
                throw Invalid("FxRate", "Chính sách thuê bao không cho nhập tỷ giá tay khi thiếu tỷ giá nguồn.");
            }

            return new FxSnapshotResult(
                currency, reporting, rate, FxSourceTypes.Manual, "Nhập tay", request.AsOf, null, reason, null);
        }

        if (policy.FxOverrideReasonRequired != false && reason is null)
        {
            throw Invalid("FxOverrideReason", "Nhập lý do ghi đè tỷ giá.");
        }

        return new FxSnapshotResult(
            currency,
            reporting,
            rate,
            FxSourceTypes.Override,
            $"Ghi đè · {book.SourceName}",
            book.RateDate ?? request.AsOf,
            book.FxRateId,
            reason,
            book.Rate);
    }

    public async Task<FxPreviewDto> PreviewAsync(
        string currencyCode,
        DateOnly asOf,
        decimal? amount,
        FxStubPolicy? stub,
        CancellationToken cancellationToken = default)
    {
        var currency = currencyCode.Trim().ToUpperInvariant();
        var reporting = await _reporting.GetAsync(cancellationToken);
        var policy = await _settings.GetFinancialAsync(cancellationToken);
        var canOverride = await _permissions.HasPermissionAsync(PermissionCodes.FxOverride, cancellationToken);
        var manualAllowed = policy.FxManualAllowed != false;
        var reasonRequired = policy.FxOverrideReasonRequired != false;
        if (string.Equals(currency, reporting, StringComparison.OrdinalIgnoreCase))
        {
            return new FxPreviewDto(
                currency, reporting, true, 1m, FxSourceTypes.Identity, null, null, amount, amount,
                true, false, manualAllowed, reasonRequired, null);
        }

        var book = await ResolveBookAsync(currency, reporting, asOf, stub, cancellationToken);
        if (book is null)
        {
            return new FxPreviewDto(
                currency, reporting, false, null, null, null, null, amount, null, false,
                canOverride, manualAllowed, reasonRequired,
                $"Chưa có tỷ giá {currency}→{reporting} ngày {asOf:dd/MM/yyyy}. Cập nhật sổ tỷ giá hoặc nhập tỷ giá tay.");
        }

        return new FxPreviewDto(
            currency, reporting, false, book.Rate, book.SourceType, book.SourceName, book.RateDate,
            amount, amount is null ? null : FxMath.ToReporting(amount.Value, book.Rate),
            true, canOverride, manualAllowed, reasonRequired, null);
    }

    public void Apply(IReportingFx target, FxSnapshotResult snapshot, string objectType, Guid objectId)
    {
        var before = target.FxStatus == FxStatuses.Converted
            ? new { rate = target.FxRate, source = target.FxSourceType, reason = target.FxOverrideReason }
            : null;
        target.ReportingCurrencyCode = snapshot.ReportingCurrencyCode;
        target.FxRate = snapshot.Rate;
        target.FxSourceType = snapshot.SourceType;
        target.FxSourceName = snapshot.SourceName;
        target.FxRateDate = snapshot.RateDate;
        target.FxRateId = snapshot.FxRateId;
        target.FxOverrideReason = snapshot.OverrideReason;
        target.FxAppliedBy = _user.UserId;
        target.FxAppliedAt = DateTimeOffset.UtcNow;
        target.FxStatus = FxStatuses.Converted;

        if (snapshot.SourceType is FxSourceTypes.Manual or FxSourceTypes.Override
            && (before is null || before.rate != snapshot.Rate || before.source != snapshot.SourceType))
        {
            _audit.Append(
                snapshot.SourceType == FxSourceTypes.Override ? "fx.override" : "fx.manual",
                objectType,
                objectId,
                beforeJson: JsonSerializer.Serialize(new
                {
                    providerRate = snapshot.ProviderRate,
                    previousRate = before?.rate,
                    previousSource = before?.source
                }),
                afterJson: JsonSerializer.Serialize(new
                {
                    currency = snapshot.CurrencyCode,
                    reportingCurrency = snapshot.ReportingCurrencyCode,
                    rate = snapshot.Rate,
                    source = snapshot.SourceType,
                    rateDate = snapshot.RateDate
                }),
                reason: snapshot.OverrideReason);
        }
    }

    public FxSnapshotResult? FromRecord(IReportingFx record)
    {
        if (record.FxStatus != FxStatuses.Converted
            || record.FxRate is not { } rate
            || rate <= 0
            || string.IsNullOrWhiteSpace(record.ReportingCurrencyCode)
            || string.IsNullOrWhiteSpace(record.FxSourceType))
        {
            return null;
        }

        var reporting = _reporting.Get();
        if (!string.Equals(record.ReportingCurrencyCode, reporting, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var currency = record.CurrencyCode.Trim().ToUpperInvariant();
        if (record.FxSourceType == FxSourceTypes.Identity
            && !string.Equals(currency, reporting, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (record.FxSourceType != FxSourceTypes.Identity
            && string.Equals(currency, reporting, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new FxSnapshotResult(
            currency,
            reporting,
            rate,
            record.FxSourceType!,
            record.FxSourceName,
            record.FxRateDate,
            record.FxRateId,
            record.FxOverrideReason,
            null);
    }

    private async Task<FxSnapshotResult?> ResolveBookAsync(
        string currency,
        string reporting,
        DateOnly asOf,
        FxStubPolicy? stub,
        CancellationToken cancellationToken)
    {
        var resolved = await _lookup.ResolveAsync(currency, reporting, asOf, cancellationToken);
        if (resolved is not null)
        {
            var (type, name) = DescribeBookSource(resolved.Source);
            if (resolved.Inverse)
            {
                name = $"{name} (nghịch đảo)";
            }

            return new FxSnapshotResult(
                currency, reporting, resolved.Rate, type, name, resolved.RateDate, resolved.FxRateId, null, null);
        }

        if (stub is { Allowed: true, Rates: not null }
            && string.Equals(stub.BaseCurrency, reporting, StringComparison.OrdinalIgnoreCase)
            && stub.Rates.TryGetValue(currency, out var stubRate)
            && stubRate > 0)
        {
            return new FxSnapshotResult(
                currency, reporting, stubRate, FxSourceTypes.Policy, "Tỷ giá cấu hình (stub)", null, null, null, null);
        }

        return null;
    }

    public static (string Type, string Name) DescribeBookSource(string? source) =>
        (source ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            FxRateSources.Vcb => (FxSourceTypes.AutoProvider, "Vietcombank"),
            FxRateSources.Import => (FxSourceTypes.Policy, "Sổ tỷ giá · import"),
            FxRateSources.StubSeed => (FxSourceTypes.Policy, "Sổ tỷ giá · mẫu"),
            FxRateSources.Manual => (FxSourceTypes.Policy, "Sổ tỷ giá · nhập tay"),
            var other => (FxSourceTypes.Policy, $"Sổ tỷ giá · {other}")
        };

    private static ValidationAppException MissingRate(
        string currency,
        string reporting,
        DateOnly asOf,
        FxStubPolicy? stub)
    {
        var message = stub is { Allowed: true }
                      && string.Equals(stub.BaseCurrency, reporting, StringComparison.OrdinalIgnoreCase)
            ? $"Chưa có tỷ giá quy đổi từ {currency} sang {reporting}. Thêm fx_rates hoặc khai báo {stub.ConfigKey}:StubFxRatesToBase."
            : $"Chưa có tỷ giá ngày hiệu lực từ {currency} sang {reporting} ({asOf:dd/MM/yyyy}). Khai báo trên sổ tỷ giá hoặc nhập tỷ giá tay trước khi ghi số tiền.";
        return new ValidationAppException(new Dictionary<string, string[]>
        {
            ["CurrencyCode"] = [message]
        });
    }

    private static ValidationAppException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>AP/AR snapshot: inherit the source Cost/Revenue snapshot, else resolve at the exposure date.</summary>
public static class OpenItemFx
{
    public static async Task SnapshotAsync(
        IFxSnapshotService fx,
        IReportingFx target,
        IReportingFx? source,
        DateOnly asOf,
        string objectType,
        Guid objectId,
        CancellationToken cancellationToken)
    {
        var inherited = source is null
                        || !string.Equals(source.CurrencyCode, target.CurrencyCode, StringComparison.OrdinalIgnoreCase)
            ? null
            : fx.FromRecord(source);
        if (inherited is not null)
        {
            target.ReportingCurrencyCode = source!.ReportingCurrencyCode;
            target.FxRate = source.FxRate;
            target.FxSourceType = source.FxSourceType;
            target.FxSourceName = source.FxSourceName;
            target.FxRateDate = source.FxRateDate;
            target.FxRateId = source.FxRateId;
            target.FxOverrideReason = source.FxOverrideReason;
            target.FxAppliedBy = source.FxAppliedBy;
            target.FxAppliedAt = source.FxAppliedAt;
            target.FxStatus = FxStatuses.Converted;
            return;
        }

        try
        {
            var snap = await fx.ResolveAsync(new FxResolveRequest(target.CurrencyCode, asOf), cancellationToken);
            fx.Apply(target, snap, objectType, objectId);
        }
        catch (ValidationAppException)
        {
            target.FxStatus = FxStatuses.Missing;
        }
    }

    public static decimal? ReportingOf(IReportingFx record, decimal amount) =>
        record.FxStatus == FxStatuses.Converted && record.FxRate is { } rate && rate > 0
            ? FxMath.ToReporting(amount, rate)
            : null;
}

public static class FxMath
{
    public static decimal ToReporting(decimal amount, decimal rate) =>
        decimal.Round(amount * rate, 4, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Largest-remainder split of a reporting total so parts sum exactly to the source (AC-FX-017).
    /// </summary>
    public static decimal[] SplitReporting(decimal total, IReadOnlyList<decimal> weights, int decimals = 4)
    {
        var result = new decimal[weights.Count];
        var weightSum = weights.Sum();
        if (weights.Count == 0 || weightSum == 0)
        {
            return result;
        }

        var unit = 1m;
        for (var i = 0; i < decimals; i++)
        {
            unit /= 10m;
        }

        var raw = weights.Select(w => total * w / weightSum).ToArray();
        for (var i = 0; i < raw.Length; i++)
        {
            result[i] = decimal.Round(raw[i], decimals, MidpointRounding.ToZero);
        }

        var remainder = total - result.Sum();
        var order = Enumerable.Range(0, raw.Length)
            .OrderByDescending(i => Math.Abs(raw[i] - result[i]))
            .ThenBy(i => i)
            .ToArray();
        var step = remainder >= 0 ? unit : -unit;
        var k = 0;
        while (remainder != 0 && k < order.Length * 4)
        {
            result[order[k % order.Length]] += step;
            remainder -= step;
            k++;
        }

        return result;
    }
}

public static class FxStubMath
{
    public static decimal ConvertWithStubOnly(string currencyCode, decimal amount, string reporting, FxStubPolicy stub)
    {
        var currency = currencyCode.Trim().ToUpperInvariant();
        var rounded = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
        if (string.Equals(currency, reporting, StringComparison.OrdinalIgnoreCase))
        {
            return rounded;
        }

        if (!stub.Allowed || !string.Equals(stub.BaseCurrency, reporting, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["CurrencyCode"] =
                [
                    $"Chưa có tỷ giá ngày hiệu lực từ {currency} sang {reporting}. Khai báo trên sổ tỷ giá trước khi ghi số tiền."
                ]
            });
        }

        if (stub.Rates is null || !stub.Rates.TryGetValue(currency, out var rate) || rate <= 0)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["CurrencyCode"] =
                [
                    $"Chưa có tỷ giá quy đổi từ {currency} sang {reporting}. Thêm fx_rates hoặc khai báo {stub.ConfigKey}:StubFxRatesToBase."
                ]
            });
        }

        return decimal.Round(rounded * rate, 4, MidpointRounding.AwayFromZero);
    }
}
