using LCMS.Application.Abstractions;

namespace LCMS.Infrastructure.Tenancy;

/// <summary>Resolves actor from JWT sub (preferred) or Dev header X-User-Id.</summary>
public sealed class HttpCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; set; }
}
