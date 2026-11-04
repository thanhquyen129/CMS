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

        var catalogItems = await _db.MasterCatalogItems.AsNoTracking()
            .Where(m => m.Kind == MasterCatalogKinds.OptionalService && m.IsActive)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.Code)
            .ToListAsync(cancellationToken);

        var byCode = new Dictionary<string, OptionalServiceOptionDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in catalogItems)
        {
            var attr = OptionalServiceAttributesParser.Parse(item.AttributesJson);
            var scope = attr.AllowedContextTypes.Contains(ServiceSelectionContexts.Bill, StringComparer.OrdinalIgnoreCase)
                ? ServiceSelectionContexts.Bill
                : attr.AllowedContextTypes.FirstOrDefault() ?? ServiceSelectionContexts.Bill;

            byCode[item.Code] = new OptionalServiceOptionDto(
                item.Code,
                item.Name,
                scope,
                HasBuyPrice: false,
                HasSellPrice: false);
        }

        var versions = await _db.SurchargeVersions.AsNoTracking()
            .Where(v => v.PublishStatus == SurchargeVersionStatuses.Published
                        && v.ActivationPolicy == SurchargeActivationPolicies.OptionalOptIn)
            .ToListAsync(cancellationToken);

        if (versions.Count > 0)
        {
            var surchargeIds = versions.Select(v => v.SurchargeId).Distinct().ToList();
            var masters = await _db.Surcharges.AsNoTracking()
                .Where(s => surchargeIds.Contains(s.Id) && s.Status == SurchargeStatuses.Active)
                .ToListAsync(cancellationToken);

            foreach (var master in masters)
            {
                var related = versions.Where(v => v.SurchargeId == master.Id).ToList();
                foreach (var version in related)
                {
                    var config = SurchargeActivationConfigParser.Parse(version.ActivationConfigJson);
                    var code = config.ServiceCode ?? master.Code;
                    var scope = config.SelectionScope ?? ServiceSelectionContexts.Bill;

                    var buy = string.Equals(master.Direction, SurchargeDirections.Buy, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(master.Direction, SurchargeDirections.Both, StringComparison.OrdinalIgnoreCase);
                    var sell = string.Equals(master.Direction, SurchargeDirections.Sell, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(master.Direction, SurchargeDirections.Both, StringComparison.OrdinalIgnoreCase);

                    if (byCode.TryGetValue(code, out var existing))
                    {
                        byCode[code] = existing with
                        {
                            SelectionScope = string.IsNullOrWhiteSpace(existing.SelectionScope) ? scope : existing.SelectionScope,
                            HasBuyPrice = existing.HasBuyPrice || buy,
                            HasSellPrice = existing.HasSellPrice || sell
                        };
                    }
                    else
                    {
                        byCode[code] = new OptionalServiceOptionDto(
                            code,
                            master.Name,
                            scope,
                            buy,
                            sell);
                    }
                }
            }
        }

        return byCode.Values.OrderBy(x => x.ServiceCode).ToList();
    }
}
