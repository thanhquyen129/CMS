using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.PartyRoles.Queries;

public sealed record PartyRoleDto(
    Guid Id,
    Guid PartyId,
    string RoleCode,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record ListPartyRolesQuery(Guid PartyId) : IRequest<IReadOnlyList<PartyRoleDto>>;

public sealed class ListPartyRolesQueryHandler : IRequestHandler<ListPartyRolesQuery, IReadOnlyList<PartyRoleDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListPartyRolesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<PartyRoleDto>> Handle(
        ListPartyRolesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var partyExists = await _db.BusinessParties.AnyAsync(p => p.Id == request.PartyId, cancellationToken);
        if (!partyExists)
        {
            throw new NotFoundAppException("Không tìm thấy đối tác.");
        }

        return await _db.PartyRoles.AsNoTracking()
            .Where(r => r.PartyId == request.PartyId)
            .OrderBy(r => r.RoleCode)
            .Select(r => new PartyRoleDto(r.Id, r.PartyId, r.RoleCode, r.IsActive, r.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
