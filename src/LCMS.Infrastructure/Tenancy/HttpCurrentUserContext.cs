using LCMS.Application.Abstractions;

namespace LCMS.Infrastructure.Tenancy;

/// <summary>Resolves actor from X-User-Id until JWT (Sprint 1).</summary>
public sealed class HttpCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; set; }
}
