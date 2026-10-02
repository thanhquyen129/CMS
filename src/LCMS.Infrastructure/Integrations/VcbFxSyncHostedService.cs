using LCMS.Application.Fx;
using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LCMS.Infrastructure.Integrations;

public sealed class VcbFxSyncOptions
{
    public const string SectionName = "VcbFxSync";

    /// <summary>Whether the daily sync is active.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// UTC hour to trigger the sync (default 1 = 08:00 ICT).
    /// Adjust for dst: VCB publishes rates around 08:00–09:00 local time.
    /// </summary>
    public int UtcHour { get; set; } = 1;

    /// <summary>UTC minute within UtcHour (default 30 → 08:30 ICT).</summary>
    public int UtcMinute { get; set; } = 30;

    /// <summary>ToCurrencyCode stored in fx_rates (Vietcombank quotes vs VND).</summary>
    public string BaseCurrency { get; set; } = "VND";
}

/// <summary>
/// Daily background job: fetch Vietcombank transfer rates and upsert into fx_rates
/// for every active tenant. Runs once per UTC day at configured UtcHour:UtcMinute.
/// Source tag: "vcb". Idempotent — same from/to/date/rate is a no-op.
/// </summary>
public sealed class VcbFxSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VcbFxSyncOptions _options;
    private readonly ILogger<VcbFxSyncHostedService> _logger;

    public VcbFxSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<VcbFxSyncOptions> options,
        ILogger<VcbFxSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("VcbFxSync disabled — skipping.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ComputeDelayToNextRun();
            _logger.LogInformation("VcbFxSync: next run in {Minutes:F0} min (UTC {Hour:D2}:{Minute:D2}).",
                delay.TotalMinutes, _options.UtcHour, _options.UtcMinute);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunSyncAsync(stoppingToken);
        }
    }

    private TimeSpan ComputeDelayToNextRun()
    {
        var now = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, now.Day,
            _options.UtcHour, _options.UtcMinute, 0, DateTimeKind.Utc);

        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }

    private async Task RunSyncAsync(CancellationToken ct)
    {
        _logger.LogInformation("VcbFxSync: fetching rates from Vietcombank.");

        FxProviderSnapshot snapshot;
        try
        {
            var provider = new VietcombankFxRateProvider();
            snapshot = await provider.FetchAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError("VcbFxSync: fetch failed — ErrorType={ErrorType} Message={Message}",
                ex.GetType().Name, ex.Message);
            return;
        }

        if (snapshot.Quotes.Count == 0)
        {
            _logger.LogWarning("VcbFxSync: VCB returned 0 quotes — skipping upsert.");
            return;
        }

        _logger.LogInformation("VcbFxSync: received {Count} quotes for {Date}.",
            snapshot.Quotes.Count, snapshot.RateDate);

        using var scope = _scopeFactory.CreateScope();
        // Use the concrete DbContext directly: background service has no HTTP tenant context.
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();

        var tenantIds = await db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.DeletedAt == null)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (tenantIds.Count == 0)
        {
            _logger.LogInformation("VcbFxSync: no active tenants — nothing to write.");
            return;
        }

        int upserted = 0;
        int skipped = 0;

        foreach (var tenantId in tenantIds)
        {
            foreach (var quote in snapshot.Quotes)
            {
                var from = quote.FromCurrencyCode;
                var to = snapshot.ToCurrencyCode;
                var date = snapshot.RateDate;
                var rate = decimal.Round(quote.Rate, 8, MidpointRounding.AwayFromZero);

                // Find latest version for this from/to/date/tenant (bypass global filter).
                var latest = await db.FxRates
                    .IgnoreQueryFilters()
                    .Where(r => r.TenantId == tenantId
                             && r.FromCurrencyCode == from
                             && r.ToCurrencyCode == to
                             && r.RateDate == date
                             && r.DeletedAt == null)
                    .OrderByDescending(r => r.Version)
                    .FirstOrDefaultAsync(ct);

                if (latest is not null && latest.Rate == rate && latest.Source == FxRateSources.Vcb)
                {
                    skipped++;
                    continue; // Idempotent: same rate already stored.
                }

                var version = latest is null ? 1 : latest.Version + 1;

                db.FxRates.Add(new FxRate
                {
                    TenantId = tenantId,
                    FromCurrencyCode = from,
                    ToCurrencyCode = to,
                    RateDate = date,
                    Rate = rate,
                    Source = FxRateSources.Vcb,
                    Version = version,
                    Note = "Auto-sync VCB"
                });
                upserted++;
            }

            if (upserted > 0 || skipped > 0)
            {
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(
                        "VcbFxSync: save failed for tenant {TenantId} — ErrorType={ErrorType}",
                        tenantId, ex.GetType().Name);
                }
            }
        }

        _logger.LogInformation(
            "VcbFxSync: done. Upserted={Upserted} Skipped={Skipped} Tenants={Tenants}.",
            upserted, skipped, tenantIds.Count);
    }
}
