namespace LCMS.Application.Abstractions;

/// <summary>
/// Role × Action × Data Scope. Action permission and Data Scope are independent dimensions.
/// Soft bootstrap when no user identity (Dev header path without actor).
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Ensures current user may perform <paramref name="actionCode"/>.
    /// Inactive user ⇒ Vietnamese 403. No actor ⇒ allow (bootstrap). No roles on tenant ⇒ allow.
    /// </summary>
    Task EnsureAsync(string actionCode, string vietnameseDeniedMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures action permission and returns the widest Data Scope among matching RolePermissions
    /// (<c>all</c> &gt; <c>organization</c> &gt; <c>own</c>). Bootstrap (no actor / no roles) ⇒ <c>all</c>.
    /// </summary>
    Task<string> EnsureAndResolveDataScopeAsync(
        string actionCode,
        string vietnameseDeniedMessage,
        CancellationToken cancellationToken = default);
}
