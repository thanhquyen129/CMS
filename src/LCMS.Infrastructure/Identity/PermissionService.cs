using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
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
        _ = await EnsureAndResolveDataScopeAsync(actionCode, vietnameseDeniedMessage, cancellationToken);
    }

    public async Task<string> EnsureAndResolveDataScopeAsync(
        string actionCode,
        string vietnameseDeniedMessage,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        // Soft bootstrap: no actor identity ⇒ allow with full scope (Dev header path).
        if (!_userContext.HasUser)
        {
            return DataScopes.All;
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var userId = _userContext.UserId!.Value;

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is not null && !user.IsActive)
        {
            throw new ForbiddenAppException("Tài khoản không còn hiệu lực.");
        }

        // Soft bootstrap: tenant has no roles seeded ⇒ allow.
        var hasRoles = await _db.Roles.AnyAsync(r => r.TenantId == tenantId, cancellationToken);
        if (!hasRoles)
        {
            return DataScopes.All;
        }

        var scopes = await (
            from ur in _db.UserRoles
            join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in _db.Permissions on rp.PermissionId equals p.Id
            where ur.TenantId == tenantId
                  && ur.UserId == userId
                  && rp.TenantId == tenantId
                  && p.ActionCode == actionCode
            select rp.DataScope).ToListAsync(cancellationToken);

        if (scopes.Count == 0)
        {
            throw new ForbiddenAppException(vietnameseDeniedMessage);
        }

        return DataScopes.Widen(scopes);
    }

    public async Task<bool> HasPermissionAsync(
        string actionCode,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            return false;
        }

        if (!_userContext.HasUser)
        {
            return true;
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var userId = _userContext.UserId!.Value;

        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is not null && !user.IsActive)
        {
            return false;
        }

        var hasRoles = await _db.Roles.AnyAsync(r => r.TenantId == tenantId, cancellationToken);
        if (!hasRoles)
        {
            return true;
        }

        return await (
            from ur in _db.UserRoles
            join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in _db.Permissions on rp.PermissionId equals p.Id
            where ur.TenantId == tenantId
                  && ur.UserId == userId
                  && rp.TenantId == tenantId
                  && p.ActionCode == actionCode
            select rp.Id).AnyAsync(cancellationToken);
    }
}
