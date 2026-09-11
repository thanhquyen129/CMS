namespace LCMS.Application.Abstractions;

/// <summary>
/// Current actor from JWT <c>sub</c> (or Dev <c>X-User-Id</c> when header bootstrap is enabled).
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    bool HasUser => UserId.HasValue && UserId.Value != Guid.Empty;
}
