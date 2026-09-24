using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Tenants.Queries;

public sealed record TenantReadinessItemDto(string Code, string Label, bool Done, string Href);

public sealed record TenantReadinessDto(
    bool ReadyForBill,
    string Note,
    IReadOnlyList<TenantReadinessItemDto> Items);

public sealed record GetTenantReadinessQuery : IRequest<TenantReadinessDto>;

public sealed class GetTenantReadinessQueryHandler : IRequestHandler<GetTenantReadinessQuery, TenantReadinessDto>
{
    private static readonly string[] BillRoles =
    [
        SystemRoleCatalog.Admin,
        SystemRoleCatalog.FinancialController,
        SystemRoleCatalog.CostAccountant,
        SystemRoleCatalog.RevenueAccountant,
        SystemRoleCatalog.Ops
    ];

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;

    public GetTenantReadinessQueryHandler(ILcmsDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<TenantReadinessDto> Handle(GetTenantReadinessQuery request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thuê bao.");

        var roleCodes = await _db.Roles.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsSystem)
            .Select(r => r.Code)
            .ToListAsync(cancellationToken);
        var rolesDone = BillRoles.All(code =>
            roleCodes.Contains(code, StringComparer.OrdinalIgnoreCase));

        var vndActive = await _db.Currencies.AsNoTracking()
            .AnyAsync(c => c.Code == TenantDefaults.CurrencyCode && c.IsActive, cancellationToken);
        var vndDone = vndActive
            && string.Equals(tenant.DefaultCurrencyCode, TenantDefaults.CurrencyCode, StringComparison.OrdinalIgnoreCase);

        var customerDone = await (
            from role in _db.PartyRoles.AsNoTracking()
            join party in _db.BusinessParties.AsNoTracking() on role.PartyId equals party.Id
            where role.RoleCode == PartyRoleCodes.Customer && role.IsActive && party.IsActive
            select role.Id).AnyAsync(cancellationToken);

        var operatorDone = await _db.Users.AsNoTracking()
            .AnyAsync(
                u => u.IsActive && u.PasswordHash != null && u.PasswordHash != "",
                cancellationToken);

        var items = new TenantReadinessItemDto[]
        {
            new("roles", "Vai trò hệ thống: Quản trị, Kiểm soát, Kế toán chi phí, Kế toán doanh thu, Điều vận", rolesDone, "/admin/access"),
            new("vnd", "Tiền mặc định là VND và VND đang dùng được", vndDone, "/settings/company"),
            new("customer", "Có đối tác khách hàng đang hoạt động", customerDone, "/admin/parties/new"),
            new("operator", "Có người dùng đang hoạt động và đã đặt mật khẩu", operatorDone, "/settings/users")
        };

        return new TenantReadinessDto(
            items.All(i => i.Done),
            "Không cần kết nối hệ thống vận hành. Bốn mục trên đủ để lập Bill.",
            items);
    }
}
