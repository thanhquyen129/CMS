using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Surcharges;

public sealed record SurchargeRuleDto(
    Guid Id,
    string CalculationMode,
    string? Basis,
    string CurrencyCode,
    decimal RateAmountPercent,
    int Priority,
    string? ContainerType,
    string? TransportMode,
    string? RouteCode,
    bool? DangerousGoods,
    Guid? RateCardId,
    Guid? RateVersionId,
    Guid? VendorPartyId = null,
    Guid? CustomerPartyId = null,
    string? CustomerGroupCode = null);

public sealed record SurchargeVersionDto(
    Guid Id,
    int VersionNo,
    string PublishStatus,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<SurchargeRuleDto> Rules,
    decimal? VatRate = null,
    Guid? EconomicChargeTypeId = null,
    string? EconomicChargeTypeCode = null,
    string? EconomicChargeTypeName = null);

public sealed record SurchargeDetailDto(
    Guid Id,
    string Code,
    string Name,
    string Direction,
    string Status,
    Guid? SourceLegacyComponentId,
    IReadOnlyList<SurchargeVersionDto> Versions);

public sealed record GetSurchargeQuery(Guid Id) : IRequest<SurchargeDetailDto>;

public sealed class GetSurchargeQueryHandler : IRequestHandler<GetSurchargeQuery, SurchargeDetailDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;

    public GetSurchargeQueryHandler(ILcmsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SurchargeDetailDto> Handle(GetSurchargeQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var surcharge = await _db.Surcharges.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy phụ phí.");
        var versions = await _db.SurchargeVersions.AsNoTracking()
            .Where(v => v.SurchargeId == surcharge.Id)
            .OrderByDescending(v => v.VersionNo)
            .ToListAsync(cancellationToken);
        var versionIds = versions.Select(v => v.Id).ToList();
        var rules = await _db.SurchargeRules.AsNoTracking()
            .Where(r => versionIds.Contains(r.SurchargeVersionId))
            .ToListAsync(cancellationToken);
        var ruleIds = rules.Select(r => r.Id).ToList();
        var conditions = await _db.SurchargeConditions.AsNoTracking()
            .Where(c => ruleIds.Contains(c.SurchargeRuleId))
            .ToListAsync(cancellationToken);
        var scopes = await _db.SurchargeScopes.AsNoTracking()
            .Where(s => ruleIds.Contains(s.SurchargeRuleId))
            .ToListAsync(cancellationToken);
        var chargeTypeIds = versions
            .Where(v => v.EconomicChargeTypeId != null)
            .Select(v => v.EconomicChargeTypeId!.Value)
            .Distinct()
            .ToList();
        var chargeTypes = await _db.EconomicChargeTypes.AsNoTracking()
            .Where(t => chargeTypeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var versionDtos = versions.Select(version =>
        {
            var ruleDtos = rules.Where(r => r.SurchargeVersionId == version.Id).Select(rule =>
            {
                string? Text(string dimension) => conditions
                    .FirstOrDefault(c => c.SurchargeRuleId == rule.Id && c.Dimension == dimension)?.ValueText;
                var dg = Text("dangerous_goods");
                var scope = scopes.FirstOrDefault(s => s.SurchargeRuleId == rule.Id);
                return new SurchargeRuleDto(
                    rule.Id,
                    rule.CalculationMode,
                    rule.Basis,
                    rule.CurrencyCode,
                    rule.RateAmountPercent,
                    rule.Priority,
                    rule.ContainerType,
                    Text("transport_mode"),
                    Text("route"),
                    dg is null ? null : string.Equals(dg, "true", StringComparison.OrdinalIgnoreCase),
                    scope?.RateCardId,
                    scope?.RateVersionId,
                    scope?.VendorPartyId,
                    scope?.CustomerPartyId,
                    scope?.CustomerGroupCode);
            }).ToList();
            chargeTypes.TryGetValue(version.EconomicChargeTypeId ?? Guid.Empty, out var chargeType);
            return new SurchargeVersionDto(
                version.Id,
                version.VersionNo,
                version.PublishStatus,
                version.ValidFrom,
                version.ValidTo,
                version.PublishedAt,
                ruleDtos,
                version.VatRate,
                version.EconomicChargeTypeId,
                chargeType?.Code,
                chargeType?.Name);
        }).ToList();

        return new SurchargeDetailDto(
            surcharge.Id,
            surcharge.Code,
            surcharge.Name,
            surcharge.Direction,
            surcharge.Status,
            surcharge.SourceLegacyComponentId,
            versionDtos);
    }
}
