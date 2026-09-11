using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Infrastructure.Identity;

public sealed class PermissionService : IPermissionService
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;

    public PermissionService(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
    }

    public async Task EnsureAsync(
        string actionCode,
        string vietnameseDeniedMessage,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        // Soft bootstrap: no actor identity yet (JWT deferred) ⇒ allow.
        if (!_userContext.HasUser)
        {
            return;
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var userId = _userContext.UserId!.Value;

        // Soft bootstrap: tenant has no roles seeded ⇒ allow.
        var hasRoles = await _db.Roles.AnyAsync(r => r.TenantId == tenantId, cancellationToken);
        if (!hasRoles)
        {
            return;
        }

        var allowed = await (
            from ur in _db.UserRoles
            join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in _db.Permissions on rp.PermissionId equals p.Id
            where ur.TenantId == tenantId
                  && ur.UserId == userId
                  && rp.TenantId == tenantId
                  && p.ActionCode == actionCode
            select p.Id).AnyAsync(cancellationToken);

        if (!allowed)
        {
            throw new ForbiddenAppException(vietnameseDeniedMessage);
        }
    }
}
