namespace LCMS.Application.Abstractions;

/// <summary>
/// Request correlation id from X-Correlation-Id (set by CorrelationIdMiddleware).
/// </summary>
public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
