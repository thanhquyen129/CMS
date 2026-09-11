namespace LCMS.Application.Abstractions;

/// <summary>
/// Current actor from X-User-Id until JWT claims (Sprint 1 bootstrap).
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    bool HasUser => UserId.HasValue && UserId.Value != Guid.Empty;
}
