namespace LCMS.Application.Abstractions;

/// <summary>Role × Action check (Data Scope stub). Soft bootstrap when no user identity.</summary>
public interface IPermissionService
{
    /// <summary>
    /// Ensures current user may perform <paramref name="actionCode"/>.
    /// No X-User-Id ⇒ allow (bootstrap). No roles on tenant ⇒ allow.
    /// Otherwise deny with Vietnamese 403.
    /// </summary>
    Task EnsureAsync(string actionCode, string vietnameseDeniedMessage, CancellationToken cancellationToken = default);
}
