using Prometheus;

namespace LCMS.Api.Observability;

/// <summary>Prometheus gauges for outbox + integration error queue (M28 / NFR-02).</summary>
public static class IntegrationJobMetrics
{
    public static readonly Counter OutboxProcessed = Metrics.CreateCounter(
        "lcms_outbox_processed_total",
        "Outbox messages marked processed (sampled via job-health).");

    public static readonly Gauge OutboxPending = Metrics.CreateGauge(
        "lcms_outbox_pending",
        "Pending outbox messages (last sample).");

    public static readonly Gauge IntegrationErrorsPending = Metrics.CreateGauge(
        "lcms_integration_errors_pending",
        "Pending integration errors (last sample).");

    public static readonly Gauge IntegrationErrorsDeadLetter = Metrics.CreateGauge(
        "lcms_integration_errors_dead_letter",
        "Dead-letter integration errors (last sample).");
}
