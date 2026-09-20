using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.PartyContacts.Queries;

public sealed record PartyContactDto(
    Guid Id,
    Guid PartyId,
    string FullName,
    string? Title,
    string? FunctionCode,
    string? Phone,
    string? Email,
    bool IsPrimary,
    bool IsActive,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record ListPartyContactsQuery(Guid PartyId) : IRequest<IReadOnlyList<PartyContactDto>>;

public sealed class ListPartyContactsQueryHandler
    : IRequestHandler<ListPartyContactsQuery, IReadOnlyList<PartyContactDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListPartyContactsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<PartyContactDto>> Handle(
        ListPartyContactsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        if (!await _db.BusinessParties.AnyAsync(p => p.Id == request.PartyId, cancellationToken))
        {
            throw new NotFoundAppException("Không tìm thấy đối tác.");
        }

        return await _db.PartyContacts.AsNoTracking()
            .Where(c => c.PartyId == request.PartyId)
            .OrderByDescending(c => c.IsPrimary)
            .ThenBy(c => c.FullName)
            .Select(c => new PartyContactDto(
                c.Id,
                c.PartyId,
                c.FullName,
                c.Title,
                c.FunctionCode,
                c.Phone,
                c.Email,
                c.IsPrimary,
                c.IsActive,
                c.Note,
                c.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
