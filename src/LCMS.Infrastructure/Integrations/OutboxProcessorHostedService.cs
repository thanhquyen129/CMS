using LCMS.Domain.Entities;
using LCMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LCMS.Infrastructure.Integrations;

public sealed class OutboxWorkerOptions
{
    public const string SectionName = "OutboxWorker";
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
}

/// <summary>
/// In-process outbox poller (P23). Marks pending → processed; no external broker yet.
/// </summary>
public sealed class OutboxProcessorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxWorkerOptions _options;
    private readonly ILogger<OutboxProcessorHostedService> _logger;

    public OutboxProcessorHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxWorkerOptions> options,
        ILogger<OutboxProcessorHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Outbox worker disabled.");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Clamp(_options.PollIntervalSeconds, 1, 60));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox worker batch failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LcmsDbContext>();
        var take = Math.Clamp(_options.BatchSize, 1, 100);

        var pending = await db.OutboxMessages
            .IgnoreQueryFilters()
            .Where(m => m.DeletedAt == null && m.Status == OutboxMessageStatuses.Pending)
            .OrderBy(m => m.Id)
            .Take(take)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return;
        }

        var utc = DateTimeOffset.UtcNow;
        foreach (var message in pending)
        {
            message.Status = OutboxMessageStatuses.Processed;
            message.ProcessedAt = utc;
            message.AttemptNo += 1;
            message.LastError = null;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Outbox processed {Count} message(s).", pending.Count);
    }
}
