using LCMS.Application.Abstractions;
using LCMS.Application.Bills.Waybills;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Bills.Queries;

/// <summary>Loads the waybill profile for a Bill. 404 when none.</summary>
public sealed record GetBillWaybillQuery(Guid BillId) : IRequest<BillWaybillDto>;

public sealed class GetBillWaybillQueryHandler : IRequestHandler<GetBillWaybillQuery, BillWaybillDto>
{
    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IPermissionService _permissions;

    public GetBillWaybillQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        IPermissionService permissions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _permissions = permissions;
    }

    public async Task<BillWaybillDto> Handle(GetBillWaybillQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        await _permissions.EnsureAsync(
            PermissionCodes.BillRead,
            "Bạn không có quyền xem Bill.",
            cancellationToken);

        var bill = await _db.Bills.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == request.BillId, cancellationToken);
        if (bill is null)
        {
            throw new NotFoundAppException("Không tìm thấy Bill.");
        }

        var waybill = await _db.BillWaybills.AsNoTracking()
            .FirstOrDefaultAsync(w => w.BillId == request.BillId, cancellationToken);
        if (waybill is null)
        {
            throw new NotFoundAppException("Bill này chưa có hồ sơ vận đơn.");
        }

        var canSee = await BillWaybillMapper.CanSeeChargesAsync(
            _permissions,
            waybill.ChargeEconomicRole,
            cancellationToken);
        return BillWaybillMapper.ToDto(waybill, bill.BillNo, canSee);
    }
}
