using LCMS.Application.Abstractions;

namespace LCMS.Infrastructure.Tenancy;

/// <summary>Holds correlation id for the current request scope.</summary>
public sealed class HttpCorrelationContext : ICorrelationContext
{
    public string? CorrelationId { get; set; }
}
