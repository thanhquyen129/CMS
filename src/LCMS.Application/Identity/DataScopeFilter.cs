using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Identity;

/// <summary>Shared data-scope resolution for list/get (P21).</summary>
public static class DataScopeFilter
{
    public static async Task<(string Scope, IReadOnlySet<Guid> OrgSubtree)> ResolveAsync(
        IPermissionService permissions,
        ICurrentUserContext user,
        ILcmsDbContext db,
        IOrganizationHierarchyService orgHierarchy,
        string actionCode,
        string deniedMessage,
        CancellationToken cancellationToken)
    {
        var scope = await permissions.EnsureAndResolveDataScopeAsync(
            actionCode, deniedMessage, cancellationToken);

        IReadOnlySet<Guid> orgSubtree = new HashSet<Guid>();
        if (scope == DataScopes.Organization && user.HasUser)
        {
            var actor = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == user.UserId, cancellationToken);
            orgSubtree = await orgHierarchy.GetSubtreeIdsAsync(actor?.OrganizationId, cancellationToken);
        }

        return (scope, orgSubtree);
    }

    public static async Task<HashSet<Guid>> BillIdsInOrgSubtreeAsync(
        ILcmsDbContext db,
        IReadOnlySet<Guid> orgSubtree,
        CancellationToken cancellationToken)
    {
        if (orgSubtree.Count == 0)
        {
            return [];
        }

        var ids = await db.Bills.AsNoTracking()
            .Where(b => b.OrganizationId != null && orgSubtree.Contains(b.OrganizationId.Value))
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    public static async Task<Dictionary<Guid, string>> LoadBillNosAsync(
        ILcmsDbContext db,
        IEnumerable<Guid?> billIds,
        CancellationToken cancellationToken)
    {
        var ids = billIds.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Bills.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.BillNo, cancellationToken);
    }

    public static async Task<Guid?> BillOrganizationIdAsync(
        ILcmsDbContext db,
        Guid? billId,
        CancellationToken cancellationToken)
    {
        if (!billId.HasValue)
        {
            return null;
        }

        return await db.Bills.AsNoTracking()
            .Where(b => b.Id == billId.Value)
            .Select(b => b.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static bool AllowsViaBillOrg(
        string scope,
        Guid? actorUserId,
        IReadOnlySet<Guid> orgSubtree,
        Guid? recordCreatedBy,
        Guid? billOrganizationId) =>
        DataScopeAccess.Allows(
            scope,
            actorUserId,
            actorOrganizationId: null,
            orgSubtree,
            recordCreatedBy,
            billOrganizationId);
}
