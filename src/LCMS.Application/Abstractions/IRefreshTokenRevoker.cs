namespace LCMS.Application.Abstractions;

public interface IRefreshTokenRevoker
{
    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken);
}
