using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Surcharges;

public sealed record ActivationServiceDto(
    string ServiceCode,
    string DisplayName,
    string? Description,
    bool IsActive,
    IReadOnlyList<string> AllowedContextTypes,
    string DefaultUom);

public sealed record ListActivationServicesQuery(bool? ActiveOnly = true)
    : IRequest<IReadOnlyList<ActivationServiceDto>>;

public sealed class ListActivationServicesQueryHandler
    : IRequestHandler<ListActivationServicesQuery, IReadOnlyList<ActivationServiceDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListActivationServicesQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ActivationServiceDto>> Handle(
        ListActivationServicesQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenant.TenantId!.Value;

        // Ensure baseline exists for this tenant
        var hasAny = await _db.MasterCatalogItems.IgnoreQueryFilters()
            .AnyAsync(m => m.TenantId == tenantId && m.Kind == MasterCatalogKinds.OptionalService && m.DeletedAt == null, cancellationToken);
        if (!hasAny)
        {
            await OptionalServiceCatalogSeeder.EnsureForTenantAsync(_db, tenantId, cancellationToken);
        }

        var query = _db.MasterCatalogItems.AsNoTracking()
            .Where(m => m.Kind == MasterCatalogKinds.OptionalService);

        if (request.ActiveOnly == true)
        {
            query = query.Where(m => m.IsActive);
        }

        var items = await query
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Code)
            .ToListAsync(cancellationToken);

        return items.Select(m =>
        {
            var attrs = OptionalServiceAttributesParser.Parse(m.AttributesJson);
            return new ActivationServiceDto(
                m.Code,
                m.Name,
                m.Description,
                m.IsActive,
                attrs.AllowedContextTypes,
                attrs.DefaultUom);
        }).ToList();
    }
}

