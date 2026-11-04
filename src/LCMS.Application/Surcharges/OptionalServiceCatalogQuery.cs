using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Surcharges;

public sealed record OptionalServiceOptionDto(
    string ServiceCode,
    string Name,
    string SelectionScope,
    bool HasBuyPrice,
    bool HasSellPrice);

public sealed record ListOptionalServiceOptionsQuery : IRequest<IReadOnlyList<OptionalServiceOptionDto>>;

public sealed class ListOptionalServiceOptionsQueryHandler
    : IRequestHandler<ListOptionalServiceOptionsQuery, IReadOnlyList<OptionalServiceOptionDto>>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public ListOptionalServiceOptionsQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<OptionalServiceOptionDto>> Handle(
        ListOptionalServiceOptionsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var versions = await _db.SurchargeVersions.AsNoTracking()
            .Where(v => v.PublishStatus == SurchargeVersionStatuses.Published
                        && v.ActivationPolicy == SurchargeActivationPolicies.OptionalOptIn)
            .ToListAsync(cancellationToken);
        if (versions.Count == 0)
        {
            return [];
        }

        var surchargeIds = versions.Select(v => v.SurchargeId).Distinct().ToList();
        var masters = await _db.Surcharges.AsNoTracking()
            .Where(s => surchargeIds.Contains(s.Id) && s.Status == SurchargeStatuses.Active)
            .ToListAsync(cancellationToken);

        var catalogItems = await _db.MasterCatalogItems.AsNoTracking()
            .Where(m => m.Kind == MasterCatalogKinds.OptionalService && m.IsActive)
            .ToListAsync(cancellationToken);
        var catalogByCode = catalogItems.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);

        var byCode = new Dictionary<string, OptionalServiceOptionDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var master in masters)
        {
            var related = versions.Where(v => v.SurchargeId == master.Id).ToList();
            foreach (var version in related)
            {
                var config = SurchargeActivationConfigParser.Parse(version.ActivationConfigJson);
                var code = config.ServiceCode ?? master.Code;
                var scope = config.SelectionScope ?? ServiceSelectionContexts.Bill;

                catalogByCode.TryGetValue(code, out var cat);
                var displayName = cat?.Name ?? master.Name;

                byCode.TryGetValue(code, out var existing);
                var buy = string.Equals(master.Direction, SurchargeDirections.Buy, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(master.Direction, SurchargeDirections.Both, StringComparison.OrdinalIgnoreCase);
                var sell = string.Equals(master.Direction, SurchargeDirections.Sell, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(master.Direction, SurchargeDirections.Both, StringComparison.OrdinalIgnoreCase);
                byCode[code] = new OptionalServiceOptionDto(
                    code,
                    displayName,
                    scope,
                    (existing?.HasBuyPrice ?? false) || buy,
                    (existing?.HasSellPrice ?? false) || sell);
            }
        }

        return byCode.Values.OrderBy(x => x.ServiceCode).ToList();
    }
}
