using LCMS.Application.Abstractions;
using LCMS.Application.Common.Exceptions;
using LCMS.Domain.Entities;
using LCMS.Domain.Identity;
using LCMS.Domain.Terminology;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LCMS.Application.Mobile;

public sealed record MobileUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    Guid? OrganizationId,
    Guid TenantId);

public sealed record MobileTenantDto(
    Guid Id,
    string Code,
    string Name,
    string DefaultCurrencyCode,
    string DateFormat,
    string TimeZoneId);

public sealed record MobilePermissionDto(
    string ActionCode,
    string DataScope);

public sealed record MobileFinancialVisibilityDto(
    bool CanViewCost,
    bool CanViewRevenue,
    bool CanViewMargin);

public sealed record MobileBadgesDto(
    int UnreadNotifications,
    int PendingApprovals,
    int OpenExceptions,
    int OpenVariances,
    int FxExceptions);

public sealed record MobileNavTabDto(
    string Key,
    string Label,
    string Icon,
    string Route,
    int BadgeCount);

public sealed record MobileModuleItemDto(
    string Code,
    string Label,
    string Description,
    string Icon,
    string Route,
    string Category,
    int BadgeCount);

public sealed record MobileBootstrapDto(
    MobileUserDto User,
    MobileTenantDto Tenant,
    IReadOnlyList<string> RoleCodes,
    string PrimaryPersona,
    IReadOnlyList<MobilePermissionDto> Permissions,
    MobileFinancialVisibilityDto FinancialVisibility,
    IReadOnlyList<string> EnabledModules,
    IReadOnlyList<MobileNavTabDto> BottomTabs,
    IReadOnlyList<MobileModuleItemDto> Modules,
    MobileBadgesDto Badges,
    IReadOnlyDictionary<string, string> Terminology);

public sealed record GetMobileBootstrapQuery : IRequest<MobileBootstrapDto>;

public sealed class GetMobileBootstrapQueryHandler
    : IRequestHandler<GetMobileBootstrapQuery, MobileBootstrapDto>
{
    private static readonly string[] DefaultAllModuleCodes =
    [
        "dashboard",
        "bills",
        "rates",
        "costs",
        "revenues",
        "documents",
        "ap",
        "ar",
        "settlements",
        "control",
        "closes",
        "reports",
        "master",
        "settings"
    ];

    private readonly ILcmsDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _userContext;
    private readonly IPermissionService _permissionService;

    public GetMobileBootstrapQueryHandler(
        ILcmsDbContext db,
        ITenantContext tenantContext,
        ICurrentUserContext userContext,
        IPermissionService permissionService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _permissionService = permissionService;
    }

    public async Task<MobileBootstrapDto> Handle(
        GetMobileBootstrapQuery request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.HasTenant)
        {
            throw new TenantRequiredAppException();
        }

        var tenantId = _tenantContext.TenantId!.Value;
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundAppException("Không tìm thấy thuê bao.");

        var userId = _userContext.HasUser ? _userContext.UserId!.Value : Guid.Empty;
        var user = userId != Guid.Empty
            ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            : null;

        if (user is not null && !user.IsActive)
        {
            throw new ForbiddenAppException("Tài khoản không còn hiệu lực.");
        }

        var roleCodes = userId != Guid.Empty
            ? await (
                from ur in _db.UserRoles.AsNoTracking()
                join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where ur.TenantId == tenantId && ur.UserId == userId
                select r.Code).Distinct().ToListAsync(cancellationToken)
            : new List<string>();

        var rawPermissions = userId != Guid.Empty
            ? await (
                from ur in _db.UserRoles.AsNoTracking()
                join rp in _db.RolePermissions.AsNoTracking() on ur.RoleId equals rp.RoleId
                join p in _db.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
                where ur.TenantId == tenantId
                      && ur.UserId == userId
                      && rp.TenantId == tenantId
                select new { p.ActionCode, rp.DataScope }).ToListAsync(cancellationToken)
            : [];

        List<MobilePermissionDto> effectivePermissions;
        if (rawPermissions.Count > 0)
        {
            effectivePermissions = rawPermissions
                .GroupBy(x => x.ActionCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => new MobilePermissionDto(
                    g.Key,
                    DataScopes.Widen(g.Select(x => x.DataScope))))
                .OrderBy(x => x.ActionCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            // Bootstrap fallback when no user roles assigned yet (Dev header or first operator)
            var canBootstrap = await _permissionService.HasPermissionAsync(
                PermissionCodes.BillRead,
                cancellationToken);
            if (canBootstrap)
            {
                if (roleCodes.Count == 0)
                {
                    roleCodes.Add(SystemRoleCatalog.Admin);
                }

                effectivePermissions = PermissionCodes.CoreActionCodes
                    .Select(code => new MobilePermissionDto(code, DataScopes.All))
                    .ToList();
            }
            else
            {
                effectivePermissions = [];
            }
        }

        var canViewCost = await _permissionService.HasPermissionAsync(
            PermissionCodes.CostRead,
            cancellationToken);
        var canViewRevenue = await _permissionService.HasPermissionAsync(
            PermissionCodes.RevenueRead,
            cancellationToken);
        var canViewMargin = canViewCost && canViewRevenue;
        var canViewBill = await _permissionService.HasPermissionAsync(
            PermissionCodes.BillRead,
            cancellationToken);

        var tenantLicenses = await _db.TenantLicenses.AsNoTracking()
            .ToListAsync(cancellationToken);
        var activeLicense = tenantLicenses
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefault();

        IReadOnlyList<string> enabledModules;
        if (activeLicense is not null)
        {
            var mods = await _db.TenantLicenseModules.AsNoTracking()
                .Where(m => m.LicenseId == activeLicense.Id && m.IncludedInPlan && m.IsEnabled)
                .Select(m => m.ModuleCode)
                .ToListAsync(cancellationToken);
            enabledModules = mods.Count > 0 ? mods : DefaultAllModuleCodes;
        }
        else
        {
            enabledModules = DefaultAllModuleCodes;
        }

        var enabledSet = new HashSet<string>(enabledModules, StringComparer.OrdinalIgnoreCase);

        var unreadNotifications = userId != Guid.Empty
            ? await _db.InAppNotifications.AsNoTracking()
                .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken)
            : 0;

        var pendingApprovals = (canViewCost || canViewRevenue)
            ? await _db.Approvals.AsNoTracking()
                .CountAsync(a => a.Status == ApprovalStatuses.Pending, cancellationToken)
            : 0;

        var openExceptions = canViewBill
            ? await _db.Exceptions.AsNoTracking()
                .CountAsync(
                    e => e.Status == ExceptionStatuses.Open || e.Status == ExceptionStatuses.Escalated,
                    cancellationToken)
            : 0;

        var openVariances = (canViewCost || canViewRevenue)
            ? await _db.Variances.AsNoTracking()
                .CountAsync(v => v.Status == VarianceStatuses.Open, cancellationToken)
            : 0;

        var fxCostExceptions = canViewCost
            ? await _db.Costs.AsNoTracking()
                .CountAsync(
                    c => c.RecordStatus == "active" && c.FxStatus == FxStatuses.RequiresReview,
                    cancellationToken)
            : 0;
        var fxRevenueExceptions = canViewRevenue
            ? await _db.Revenues.AsNoTracking()
                .CountAsync(
                    r => r.RecordStatus == "active" && r.FxStatus == FxStatuses.RequiresReview,
                    cancellationToken)
            : 0;
        var fxExceptions = fxCostExceptions + fxRevenueExceptions;

        var badges = new MobileBadgesDto(
            unreadNotifications,
            pendingApprovals,
            openExceptions,
            openVariances,
            fxExceptions);

        var primaryPersona = ResolvePrimaryPersona(roleCodes, canViewCost, canViewRevenue);
        var modules = BuildAvailableModules(
            enabledSet,
            effectivePermissions,
            canViewBill,
            canViewCost,
            canViewRevenue,
            canViewMargin,
            badges);
        var bottomTabs = BuildRoleAdaptiveBottomTabs(primaryPersona, badges);

        var userDto = new MobileUserDto(
            user?.Id ?? userId,
            user?.Email ?? "operator@lcms.local",
            user?.DisplayName ?? "Người vận hành",
            user?.OrganizationId,
            tenantId);

        var tenantDto = new MobileTenantDto(
            tenant.Id,
            tenant.Code,
            tenant.Name,
            tenant.DefaultCurrencyCode,
            tenant.DateFormat,
            tenant.TimeZoneId);

        return new MobileBootstrapDto(
            userDto,
            tenantDto,
            roleCodes,
            primaryPersona,
            effectivePermissions,
            new MobileFinancialVisibilityDto(canViewCost, canViewRevenue, canViewMargin),
            enabledModules,
            bottomTabs,
            modules,
            badges,
            VietnameseUiTerms.All);
    }

    private static string ResolvePrimaryPersona(
        IReadOnlyList<string> roleCodes,
        bool canViewCost,
        bool canViewRevenue)
    {
        if (roleCodes.Contains(SystemRoleCatalog.Admin, StringComparer.OrdinalIgnoreCase)
            || roleCodes.Contains(SystemRoleCatalog.FinancialController, StringComparer.OrdinalIgnoreCase))
        {
            return "executive_control";
        }

        if (roleCodes.Contains(SystemRoleCatalog.CostAccountant, StringComparer.OrdinalIgnoreCase))
        {
            return "cost_accountant";
        }

        if (roleCodes.Contains(SystemRoleCatalog.RevenueAccountant, StringComparer.OrdinalIgnoreCase))
        {
            return "revenue_accountant";
        }

        if (roleCodes.Contains(SystemRoleCatalog.Ops, StringComparer.OrdinalIgnoreCase))
        {
            return "field_ops";
        }

        if (roleCodes.Contains(SystemRoleCatalog.MasterData, StringComparer.OrdinalIgnoreCase))
        {
            return "master_data";
        }

        if (canViewCost && canViewRevenue)
        {
            return "executive_control";
        }

        if (canViewCost)
        {
            return "cost_accountant";
        }

        if (canViewRevenue)
        {
            return "revenue_accountant";
        }

        return "viewer";
    }

    private static IReadOnlyList<MobileNavTabDto> BuildRoleAdaptiveBottomTabs(
        string persona,
        MobileBadgesDto badges)
    {
        return persona switch
        {
            "executive_control" =>
            [
                new("dashboard", "Tổng quan", "bar-chart-2", "/(tabs)/dashboard", 0),
                new("approvals", "Phê duyệt", "check-circle", "/(tabs)/approvals", badges.PendingApprovals),
                new("control", "Kiểm soát", "shield-alert", "/(tabs)/control", badges.OpenExceptions + badges.OpenVariances),
                new("bills", "Bill & Công nợ", "file-text", "/(tabs)/bills", 0),
                new("modules", "Phân hệ", "grid", "/(tabs)/modules", badges.UnreadNotifications)
            ],
            "cost_accountant" =>
            [
                new("dashboard", "Tổng quan CP", "pie-chart", "/(tabs)/dashboard", 0),
                new("costs", "Chi phí", "trending-down", "/(tabs)/costs", 0),
                new("ap", "Công nợ AP", "credit-card", "/(tabs)/ap", 0),
                new("documents", "Chứng từ & Chi", "file-check", "/(tabs)/documents", 0),
                new("modules", "Phân hệ", "grid", "/(tabs)/modules", badges.UnreadNotifications)
            ],
            "revenue_accountant" =>
            [
                new("dashboard", "Tổng quan DT", "trending-up", "/(tabs)/dashboard", 0),
                new("revenues", "Doanh thu", "dollar-sign", "/(tabs)/revenues", 0),
                new("ar", "Công nợ AR", "wallet", "/(tabs)/ar", 0),
                new("documents", "Chứng từ & Thu", "file-check", "/(tabs)/documents", 0),
                new("modules", "Phân hệ", "grid", "/(tabs)/modules", badges.UnreadNotifications)
            ],
            "field_ops" =>
            [
                new("bills", "Vận hành", "truck", "/(tabs)/bills", 0),
                new("scanner", "Quét & Chụp", "camera", "/(tabs)/scanner", 0),
                new("costs", "CP Dự kiến", "plus-circle", "/(tabs)/costs", 0),
                new("offline", "Đồng bộ", "cloud-upload", "/(tabs)/offline", 0),
                new("modules", "Phân hệ", "grid", "/(tabs)/modules", badges.UnreadNotifications)
            ],
            "master_data" =>
            [
                new("bills", "Tra cứu Bill", "search", "/(tabs)/bills", 0),
                new("parties", "Đối tác", "users", "/(tabs)/parties", 0),
                new("routes", "Tuyến & Điểm", "map-pin", "/(tabs)/routes", 0),
                new("catalog", "Danh mục & FX", "layers", "/(tabs)/catalog", 0),
                new("modules", "Phân hệ", "grid", "/(tabs)/modules", badges.UnreadNotifications)
            ],
            _ =>
            [
                new("bills", "Danh sách Bill", "file-text", "/(tabs)/bills", 0),
                new("scanner", "Quét mã", "camera", "/(tabs)/scanner", 0),
                new("notifications", "Thông báo", "bell", "/(tabs)/notifications", badges.UnreadNotifications),
                new("modules", "Phân hệ", "grid", "/(tabs)/modules", 0)
            ]
        };
    }

    private static IReadOnlyList<MobileModuleItemDto> BuildAvailableModules(
        HashSet<string> enabledSet,
        IReadOnlyList<MobilePermissionDto> permissions,
        bool canViewBill,
        bool canViewCost,
        bool canViewRevenue,
        bool canViewMargin,
        MobileBadgesDto badges)
    {
        bool HasPerm(string code) =>
            permissions.Any(p => string.Equals(p.ActionCode, code, StringComparison.OrdinalIgnoreCase));

        var list = new List<MobileModuleItemDto>();

        if (enabledSet.Contains("dashboard") && (canViewBill || canViewCost || canViewRevenue))
        {
            list.Add(new("dashboard", "Trang chủ & KPI", "Tổng quan số liệu theo quyền hạn", "bar-chart-2", "/modules/dashboard", "core", 0));
        }

        if (enabledSet.Contains("bills") && canViewBill)
        {
            list.Add(new("bills", "Đơn hàng, Bill & Chặng/Chuyến", "Quản lý vận hành, xác nhận CW & ngữ cảnh tính giá", "truck", "/modules/bills", "operations", 0));
            list.Add(new("scanner", "Quét mã vận đơn & Chụp chứng từ", "Quét Barcode/QR HAWB/MAWB và đính kèm ảnh hiện trường", "camera", "/modules/scanner", "operations", 0));
        }

        if (enabledSet.Contains("rates") && (HasPerm(PermissionCodes.RateBuyRead) || HasPerm(PermissionCodes.RateSellRead)))
        {
            list.Add(new("rates", "Bảng giá & Tính giá", "Tính giá Bill, so sánh giá, phụ phí & tỷ giá VCB", "calculator", "/modules/rates", "pricing", badges.FxExceptions));
        }

        // Strict SoD: Cost module only when canViewCost
        if (enabledSet.Contains("costs") && canViewCost)
        {
            list.Add(new("costs", "Quản lý Chi phí & Phân bổ", "Chi phí trực tiếp, chi phí chung & phân bổ theo Bill", "trending-down", "/modules/costs", "finance", 0));
        }

        // Strict SoD: Revenue module only when canViewRevenue
        if (enabledSet.Contains("revenues") && canViewRevenue)
        {
            var desc = canViewMargin
                ? "Doanh thu theo Bill, chia doanh thu & báo cáo lợi nhuận"
                : "Doanh thu theo Bill & chia doanh thu";
            list.Add(new("revenues", "Quản lý Doanh thu", desc, "trending-up", "/modules/revenues", "finance", 0));
        }

        if (enabledSet.Contains("documents") && (canViewCost || canViewRevenue))
        {
            list.Add(new("documents", "Chứng từ tài chính & Đối khớp", "Nhận chứng từ, đính kèm ảnh/PDF & khớp N:N", "file-check", "/modules/documents", "finance", 0));
        }

        if (enabledSet.Contains("ap") && canViewCost)
        {
            list.Add(new("ap", "Công nợ phải trả (AP)", "Exposure phải trả, Sổ công nợ AP, điều chỉnh & xóa nợ", "credit-card", "/modules/ap", "apar", 0));
        }

        if (enabledSet.Contains("ar") && canViewRevenue)
        {
            list.Add(new("ar", "Công nợ phải thu (AR)", "Exposure phải thu, Sổ công nợ AR, điều chỉnh & xóa nợ", "wallet", "/modules/ar", "apar", 0));
        }

        if (enabledSet.Contains("settlements") && (canViewCost || canViewRevenue))
        {
            list.Add(new("settlements", "Thanh toán & Thu tiền", "Phiếu chi, phiếu thu & phân bổ tất toán công nợ", "repeat", "/modules/settlements", "apar", 0));
        }

        if (enabledSet.Contains("control") && (canViewCost || canViewRevenue || canViewBill))
        {
            list.Add(new("approvals", "Hàng đợi Phê duyệt", "Duyệt nhanh bằng FaceID/Vân tay kèm xem trước Before → After", "check-circle", "/modules/approvals", "control", badges.PendingApprovals));
            list.Add(new("control", "Kiểm soát, Ngoại lệ & Đối soát", "Xử lý ngoại lệ, chênh lệch, sao kê ngân hàng & đối soát số dư", "shield-alert", "/modules/control", "control", badges.OpenExceptions + badges.OpenVariances));
        }

        if (enabledSet.Contains("closes") && (canViewCost || canViewRevenue))
        {
            list.Add(new("closes", "Chốt kỳ tài chính", "Tạo bản chốt bất biến (Snapshot) & mở lại có kiểm soát", "lock", "/modules/closes", "control", 0));
        }

        if (enabledSet.Contains("reports") && (canViewCost || canViewRevenue))
        {
            list.Add(new("reports", "Báo cáo & Phân tích", "Báo cáo chi phí, doanh thu, lợi nhuận theo Bill & tuổi nợ", "pie-chart", "/modules/reports", "reports", 0));
        }

        if (enabledSet.Contains("master")
            && (HasPerm(PermissionCodes.MasterPartyManage)
                || HasPerm(PermissionCodes.MasterCatalogManage)
                || HasPerm(PermissionCodes.MasterCurrencyManage)
                || HasPerm(PermissionCodes.MasterOrgManage)))
        {
            list.Add(new("master", "Danh mục dữ liệu", "Đối tác, Tuyến, Địa điểm, Loại hàng, Dịch vụ & Tiền tệ", "database", "/modules/master", "admin", 0));
        }

        if (enabledSet.Contains("settings")
            && (HasPerm(PermissionCodes.SettingsManage)
                || HasPerm(PermissionCodes.UserManage)
                || HasPerm(PermissionCodes.RoleManage)
                || HasPerm(PermissionCodes.NotificationManage)))
        {
            list.Add(new("settings", "Quản trị & Cài đặt", "Người dùng, Vai trò, Hồ sơ doanh nghiệp, Thông báo & Sao lưu", "settings", "/modules/settings", "admin", 0));
        }

        list.Add(new("offline", "Hàng đợi Đồng bộ Offline", "Quản lý các bản ghi lưu nháp khi mất sóng tại kho/cảng", "cloud-upload", "/modules/offline", "system", 0));

        return list;
    }
}
