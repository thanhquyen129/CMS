using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.BusinessParties.Queries;

public sealed record BusinessPartyDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record GetBusinessPartyByIdQuery(Guid Id) : IRequest<BusinessPartyDto>;

public sealed class GetBusinessPartyByIdQueryHandler : IRequestHandler<GetBusinessPartyByIdQuery, BusinessPartyDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetBusinessPartyByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<BusinessPartyDto> Handle(GetBusinessPartyByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var party = await _db.BusinessParties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (party is null)
        {
            throw new NotFoundAppException("Không tìm thấy đối tác.");
        }

        return new BusinessPartyDto(party.Id, party.Code, party.Name, party.IsActive, party.CreatedAt);
    }
}

public sealed record ListBusinessPartiesQuery : IRequest<IReadOnlyList<BusinessPartyDto>>;

public sealed class ListBusinessPartiesQueryHandler
    : IRequestHandler<ListBusinessPartiesQuery, IReadOnlyList<BusinessPartyDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListBusinessPartiesQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<BusinessPartyDto>> Handle(
        ListBusinessPartiesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        return await _db.BusinessParties
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new BusinessPartyDto(p.Id, p.Code, p.Name, p.IsActive, p.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
