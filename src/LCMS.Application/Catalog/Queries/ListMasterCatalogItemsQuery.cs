using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Catalog.Queries;

public sealed record MasterCatalogItemDto(
    Guid Id,
    string Kind,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    int SortOrder,
    DateTimeOffset CreatedAt);

public sealed record ListMasterCatalogItemsQuery(string? Kind, bool? ActiveOnly)
    : IRequest<IReadOnlyList<MasterCatalogItemDto>>;

/// <summary>Lists tenant taxonomy items for UI-13 dropdowns and admin.</summary>
public sealed class ListMasterCatalogItemsQueryHandler
    : IRequestHandler<ListMasterCatalogItemsQuery, IReadOnlyList<MasterCatalogItemDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ListMasterCatalogItemsQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<MasterCatalogItemDto>> Handle(
        ListMasterCatalogItemsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var query = _db.MasterCatalogItems.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            var kind = request.Kind.Trim().ToLowerInvariant();
            query = query.Where(i => i.Kind == kind);
        }

        if (request.ActiveOnly == true)
        {
            query = query.Where(i => i.IsActive);
        }

        return await query
            .OrderBy(i => i.Kind)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Code)
            .Select(i => new MasterCatalogItemDto(
                i.Id,
                i.Kind,
                i.Code,
                i.Name,
                i.Description,
                i.IsActive,
                i.SortOrder,
                i.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
