using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Application.OperationalReferences.Edit;
using LCMS.Domain.Identity;
using MediatR;

namespace LCMS.Application.Ratings.Queries;

public sealed record RatingReadinessDto(
    bool Ready,
    Guid BillId,
    Guid RateVersionId,
    string? RateCardCode,
    int VersionNo,
    IReadOnlyList<string> RuleCodes,
    decimal? Quantity,
    string QuantityBasis,
    string QuantityUom,
    string? QuantityRuleCode,
    bool RequiresOverride,
    IReadOnlyList<RatingMissingFieldDto> Missing,
    ChargeableWeightStateDto Chargeable,
    RatingReadinessContextDto Context);

public sealed record RatingReadinessContextDto(
    string? TransportMode,
    string? ServiceType,
    string? RouteCode,
    string? OriginCode,
    string? DestinationCode,
    string? CommodityCode,
    decimal? GrossWeightKg,
    decimal? VolumeCbm);

/// <summary>Pre-check before "Tính giá": same resolution as rating, but never persists.</summary>
public sealed record GetRatingReadinessQuery(RatingInput Input) : IRequest<RatingReadinessDto>;

public sealed class GetRatingReadinessQueryHandler : IRequestHandler<GetRatingReadinessQuery, RatingReadinessDto>
{
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;
    private readonly RatingContextResolver _resolver;

    public GetRatingReadinessQueryHandler(ITenantContext tenant, IPermissionService permissions, RatingContextResolver resolver)
    {
        _tenant = tenant;
        _permissions = permissions;
        _resolver = resolver;
    }

    public async Task<RatingReadinessDto> Handle(GetRatingReadinessQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(PermissionCodes.BillRead, "Bạn không có quyền xem Bill.", cancellationToken);
        var ctx = await _resolver.ResolveAsync(request.Input, cancellationToken);
        var mode = ctx.TransportMode ?? ctx.Card?.TransportMode;
        return new RatingReadinessDto(
            ctx.Missing.Count == 0,
            ctx.Bill.Id,
            ctx.Version.Id,
            ctx.Card?.Code,
            ctx.Version.VersionNo,
            ctx.Selected.Select(r => r.Code).ToList(),
            ctx.Missing.Count == 0 ? ctx.Quantity : null,
            ctx.Basis,
            ChargeableWeightPolicy.UomFor(mode),
            ctx.QuantityRuleCode,
            ctx.RequiresOverride,
            ctx.Missing,
            ctx.BillChargeable,
            new RatingReadinessContextDto(
                mode,
                ctx.ServiceType,
                ctx.RouteCode,
                ctx.OriginCode,
                ctx.DestinationCode,
                ctx.CommodityCode,
                ctx.Gross,
                ctx.Volume));
    }
}
