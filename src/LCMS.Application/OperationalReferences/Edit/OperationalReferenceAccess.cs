using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.OperationalReferences.Edit;

/// <summary>Permission + data scope for operational reference view/edit. Out of scope ⇒ 404 (no existence leak).</summary>
internal sealed class OperationalReferenceAccess
{
    private readonly ILcmsDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserContext _user;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public OperationalReferenceAccess(
        ILcmsDbContext db,
        IPermissionService permissions,
        ICurrentUserContext user,
        IOrganizationHierarchyService orgHierarchy)
    {
        _db = db;
        _permissions = permissions;
        _user = user;
        _orgHierarchy = orgHierarchy;
    }

    public static string EditPermission(string type) =>
        type == OperationalObjectTypes.Bill ? PermissionCodes.BillUpdate : PermissionCodes.OperationalReferenceUpdate;

    public async Task EnsureAsync(OperationalReferenceRecord record, string actionCode, string deniedMessage, CancellationToken cancellationToken)
    {
        var (scope, subtree) = await DataScopeFilter.ResolveAsync(
            _permissions, _user, _db, _orgHierarchy, actionCode, deniedMessage, cancellationToken);
        if (scope == DataScopes.All)
        {
            return;
        }

        var actor = _user.HasUser ? _user.UserId : null;
        if (record.Type == OperationalObjectTypes.Bill)
        {
            if (!DataScopeFilter.AllowsViaBillOrg(scope, actor, subtree, record.CreatedBy, record.OrganizationId))
            {
                throw new NotFoundAppException("Không tìm thấy Bill.");
            }

            return;
        }

        if (actor is not null && record.CreatedBy == actor)
        {
            return;
        }

        var billIds = await LinkedBillIdsAsync(_db, record.Type, record.Id, cancellationToken);
        if (billIds.Count > 0)
        {
            var bills = await _db.Bills.AsNoTracking()
                .Where(b => billIds.Contains(b.Id))
                .Select(b => new { b.CreatedBy, b.OrganizationId })
                .ToListAsync(cancellationToken);
            if (bills.Any(b => DataScopeFilter.AllowsViaBillOrg(scope, actor, subtree, b.CreatedBy, b.OrganizationId)))
            {
                return;
            }
        }

        throw new NotFoundAppException($"Không tìm thấy {OperationalFieldCatalog.ObjectLabel(record.Type).ToLowerInvariant()}.");
    }

    /// <summary>Bills whose ratings may depend on this reference. Never the whole tenant.</summary>
    public static async Task<List<Guid>> LinkedBillIdsAsync(
        ILcmsDbContext db,
        string type,
        Guid id,
        CancellationToken cancellationToken)
    {
        switch (type)
        {
            case OperationalObjectTypes.Bill:
                return [id];
            case OperationalObjectTypes.Order:
                return await db.OrderBillLinks.AsNoTracking()
                    .Where(l => l.OrderId == id)
                    .Select(l => l.BillId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            case OperationalObjectTypes.Shipment:
                return await db.BillShipmentLinks.AsNoTracking()
                    .Where(l => l.ShipmentId == id)
                    .Select(l => l.BillId)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            case OperationalObjectTypes.Leg:
            {
                var direct = await db.BillLegLinks.AsNoTracking()
                    .Where(l => l.TransportLegId == id)
                    .Select(l => l.BillId)
                    .ToListAsync(cancellationToken);
                var shipmentId = await db.TransportLegs.AsNoTracking()
                    .Where(l => l.Id == id)
                    .Select(l => l.ShipmentId)
                    .FirstOrDefaultAsync(cancellationToken);
                var viaShipment = await db.BillShipmentLinks.AsNoTracking()
                    .Where(l => l.ShipmentId == shipmentId)
                    .Select(l => l.BillId)
                    .ToListAsync(cancellationToken);
                return direct.Concat(viaShipment).Distinct().ToList();
            }
            case OperationalObjectTypes.Movement:
            {
                var direct = await db.BillMovementLinks.AsNoTracking()
                    .Where(l => l.TransportMovementId == id)
                    .Select(l => l.BillId)
                    .ToListAsync(cancellationToken);
                var legIds = await db.LegMovementLinks.AsNoTracking()
                    .Where(l => l.TransportMovementId == id)
                    .Select(l => l.TransportLegId)
                    .ToListAsync(cancellationToken);
                var viaLegs = legIds.Count == 0
                    ? []
                    : await db.BillLegLinks.AsNoTracking()
                        .Where(l => legIds.Contains(l.TransportLegId))
                        .Select(l => l.BillId)
                        .ToListAsync(cancellationToken);
                return direct.Concat(viaLegs).Distinct().ToList();
            }
            default:
                return [];
        }
    }
}
