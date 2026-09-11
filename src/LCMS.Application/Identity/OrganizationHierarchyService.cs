using LCMS.Application.Abstractions;
using LCMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Identity;

/// <summary>Resolves organization subtree ids for Data Scope = organization.</summary>
public interface IOrganizationHierarchyService
{
    /// <summary>Returns home org + all descendants (same tenant). Empty when home is null.</summary>
    Task<IReadOnlySet<Guid>> GetSubtreeIdsAsync(Guid? homeOrganizationId, CancellationToken cancellationToken = default);
}

public sealed class OrganizationHierarchyService : IOrganizationHierarchyService
{
    private readonly ILcmsDbContext _db;

    public OrganizationHierarchyService(ILcmsDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlySet<Guid>> GetSubtreeIdsAsync(
        Guid? homeOrganizationId,
        CancellationToken cancellationToken = default)
    {
        if (homeOrganizationId is null || homeOrganizationId == Guid.Empty)
        {
            return new HashSet<Guid>();
        }

        var orgs = await _db.Organizations.AsNoTracking()
            .Select(o => new { o.Id, o.ParentId })
            .ToListAsync(cancellationToken);

        var childrenByParent = orgs
            .Where(o => o.ParentId.HasValue)
            .GroupBy(o => o.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(homeOrganizationId.Value);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!result.Add(id))
            {
                continue;
            }

            if (childrenByParent.TryGetValue(id, out var children))
            {
                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        return result;
    }
}

/// <summary>Applies Data Scope filters for Bill / Cost list+get.</summary>
public static class DataScopeAccess
{
    public static bool Allows(
        string dataScope,
        Guid? actorUserId,
        Guid? actorOrganizationId,
        IReadOnlySet<Guid> orgSubtree,
        Guid? recordCreatedBy,
        Guid? recordOrganizationId)
    {
        var scope = dataScope.Trim().ToLowerInvariant();
        return scope switch
        {
            DataScopes.All => true,
            DataScopes.Own => actorUserId.HasValue && recordCreatedBy == actorUserId,
            DataScopes.Organization =>
                recordOrganizationId.HasValue && orgSubtree.Contains(recordOrganizationId.Value),
            _ => false
        };
    }
}
