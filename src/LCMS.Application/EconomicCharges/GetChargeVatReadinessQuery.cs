using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.EconomicCharges;

public sealed record ChargeVatReadinessDto(
    int RateVersionsMissingVat,
    int SurchargeVersionsMissingVat,
    int RatingDetailsMissingVat,
    int CostsMissingVat,
    int RevenuesMissingVat,
    int CostsUnmapped,
    int RevenuesUnmapped,
    int RatingDetailsUnmapped,
    string Note);

public sealed record GetChargeVatReadinessQuery : IRequest<ChargeVatReadinessDto>;

public sealed class GetChargeVatReadinessQueryHandler : IRequestHandler<GetChargeVatReadinessQuery, ChargeVatReadinessDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IPermissionService _permissions;

    public GetChargeVatReadinessQueryHandler(ILcmsDbContext db, ITenantContext tenant, IPermissionService permissions)
    {
        _db = db;
        _tenant = tenant;
        _permissions = permissions;
    }

    public async Task<ChargeVatReadinessDto> Handle(GetChargeVatReadinessQuery request, CancellationToken cancellationToken)
    {
        if (!_tenant.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        if (!await _permissions.HasPermissionAsync(PermissionCodes.RateBuyRead, cancellationToken)
            && !await _permissions.HasPermissionAsync(PermissionCodes.RateSellRead, cancellationToken))
        {
            throw new ForbiddenAppException("Bạn không có quyền xem tình trạng VAT và khoản mục.");
        }

        return new ChargeVatReadinessDto(
            await _db.RateVersions.AsNoTracking().CountAsync(v => v.VatRate == null, cancellationToken),
            await _db.SurchargeVersions.AsNoTracking().CountAsync(v => v.VatRate == null, cancellationToken),
            await _db.RatingDetails.AsNoTracking().CountAsync(d => d.VatRate == null, cancellationToken),
            await _db.Costs.AsNoTracking().CountAsync(c => c.RecordStatus == "active" && c.VatRate == null, cancellationToken),
            await _db.Revenues.AsNoTracking().CountAsync(r => r.RecordStatus == "active" && r.VatRate == null, cancellationToken),
            await _db.Costs.AsNoTracking().CountAsync(c => c.RecordStatus == "active" && c.EconomicChargeTypeId == null, cancellationToken),
            await _db.Revenues.AsNoTracking().CountAsync(r => r.RecordStatus == "active" && r.EconomicChargeTypeId == null, cancellationToken),
            await _db.RatingDetails.AsNoTracking().CountAsync(d => d.EconomicChargeTypeId == null, cancellationToken),
            "VAT để trống là chưa khai báo, không phải 0%. Báo cáo chỉ đọc, không sửa phiên bản đã phát hành hay chứng từ cũ. Giá lịch sử đã gồm VAT hay chưa phải đối soát trên dữ liệu thật trước khi coi giá niêm yết là trước VAT.");
    }
}
