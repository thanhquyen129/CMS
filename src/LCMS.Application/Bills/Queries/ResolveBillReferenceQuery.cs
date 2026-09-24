using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.Identity;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

/// <summary>
/// Resolves a Bill business code, external id, or internal id inside the caller's data scope.
/// </summary>
public sealed record ResolveBillReferenceQuery(string? Raw) : IRequest<Guid?>;

public sealed class ResolveBillReferenceQueryHandler : IRequestHandler<ResolveBillReferenceQuery, Guid?>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ICurrentUserContext _user;
    private readonly IPermissionService _permissions;
    private readonly IOrganizationHierarchyService _orgHierarchy;

    public ResolveBillReferenceQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenant,
        ICurrentUserContext user,
        IPermissionService permissions,
        IOrganizationHierarchyService orgHierarchy)
    {
        _db = db;
        _tenant = tenant;
        _user = user;
        _permissions = permissions;
        _orgHierarchy = orgHierarchy;
    }

    public async Task<Guid?> Handle(ResolveBillReferenceQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Raw))
        {
            return null;
        }

        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var (scope, orgSubtree) = await DataScopeFilter.ResolveAsync(
            _permissions,
            _user,
            _db,
            _orgHierarchy,
            PermissionCodes.BillRead,
            "Bạn không có quyền gắn Bill.",
            cancellationToken);

        var text = request.Raw.Trim();
        List<BillHit> hits;
        if (Guid.TryParse(text, out var id))
        {
            hits = await _db.Bills.AsNoTracking()
                .Where(b => b.Id == id)
                .Select(b => new BillHit(b.Id, b.CreatedBy, b.OrganizationId))
                .Take(1)
                .ToListAsync(cancellationToken);
        }
        else
        {
            var code = text.ToLowerInvariant();
            hits = await _db.Bills.AsNoTracking()
                .Where(b =>
                    b.BillNo.ToLower() == code
                    || (b.ExternalId != null && b.ExternalId.ToLower() == code))
                .Select(b => new BillHit(b.Id, b.CreatedBy, b.OrganizationId))
                .Take(5)
                .ToListAsync(cancellationToken);
        }

        var visible = hits
            .Where(h => DataScopeFilter.AllowsViaBillOrg(
                scope, _user.UserId, orgSubtree, h.CreatedBy, h.OrganizationId))
            .ToList();

        if (visible.Count == 0)
        {
            throw new NotFoundAppException($"Không tìm thấy Bill \"{text}\" trong phạm vi của bạn.");
        }

        if (visible.Count > 1)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["billId"] = ["Có nhiều Bill trùng mã. Chọn đúng Bill từ danh sách."]
            });
        }

        return visible[0].Id;
    }

    private sealed record BillHit(Guid Id, Guid? CreatedBy, Guid? OrganizationId);
}
