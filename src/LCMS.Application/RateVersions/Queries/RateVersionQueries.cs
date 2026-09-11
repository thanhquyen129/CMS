using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.RateVersions.Queries;

public sealed record RateVersionDto(
    Guid Id,
    Guid RateCardId,
    int VersionNo,
    string Status,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset? PublishedAt,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record GetRateVersionByIdQuery(Guid Id) : IRequest<RateVersionDto>;

public sealed class GetRateVersionByIdQueryHandler : IRequestHandler<GetRateVersionByIdQuery, RateVersionDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetRateVersionByIdQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<RateVersionDto> Handle(GetRateVersionByIdQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var version = await _db.RateVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);

        if (version is null)
        {
            throw new NotFoundAppException("Không tìm thấy phiên bản bảng giá.");
        }

        return ToDto(version);
    }

    internal static RateVersionDto ToDto(Domain.Entities.RateVersion v) =>
        new(v.Id, v.RateCardId, v.VersionNo, v.Status, v.EffectiveFrom, v.EffectiveTo, v.PublishedAt, v.Note, v.CreatedAt);
}

public sealed record ListRateVersionsQuery(Guid RateCardId) : IRequest<IReadOnlyList<RateVersionDto>>;

public sealed class ListRateVersionsQueryHandler : IRequestHandler<ListRateVersionsQuery, IReadOnlyList<RateVersionDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListRateVersionsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<RateVersionDto>> Handle(
        ListRateVersionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var cardExists = await _db.RateCards.AnyAsync(c => c.Id == request.RateCardId, cancellationToken);
        if (!cardExists)
        {
            throw new NotFoundAppException("Không tìm thấy bảng giá.");
        }

        return await _db.RateVersions
            .AsNoTracking()
            .Where(v => v.RateCardId == request.RateCardId)
            .OrderByDescending(v => v.VersionNo)
            .Select(v => new RateVersionDto(
                v.Id, v.RateCardId, v.VersionNo, v.Status,
                v.EffectiveFrom, v.EffectiveTo, v.PublishedAt, v.Note, v.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
