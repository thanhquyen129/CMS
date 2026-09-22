namespace LCMS.Domain.Identity;

/// <summary>Stable Action codes for Role × Permission (Sprint 1 + P24 fine-grained).</summary>
public static class PermissionCodes
{
    public const string BillCreate = "bill.create";
    public const string BillRead = "bill.read";
    /// <summary>Update Bill financial-ops context (customer/route/note) — not ledger overwrite.</summary>
    public const string BillUpdate = "bill.update";
    public const string CostCreate = "cost.create";
    public const string CostRead = "cost.read";
    public const string CostConfirm = "cost.confirm";
    public const string CostActualize = "cost.actualize";
    public const string CostAllocationOverride = "cost.allocation.override";
    /// <summary>View revenue — independent of cost.read (H View Cost ≠ Revenue).</summary>
    public const string RevenueRead = "revenue.read";
    public const string RevenueConfirm = "revenue.confirm";
    public const string RevenueActualize = "revenue.actualize";
    public const string RevenueMappingOverride = "revenue.mapping.override";
    public const string ApWriteOff = "ap.write_off";
    public const string ArWriteOff = "ar.write_off";
    public const string MasterOrgManage = "master.org.manage";
    public const string MasterPartyManage = "master.party.manage";
    public const string MasterCurrencyManage = "master.currency.manage";
    /// <summary>Manage Cost/Revenue/Service type catalogs (D02).</summary>
    public const string MasterCatalogManage = "master.catalog.manage";
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";
    public const string SettingsManage = "settings.manage";
    public const string AuditRead = "audit.read";
    public const string LicenseManage = "license.manage";
    public const string BackupManage = "backup.manage";
    public const string NotificationManage = "notification.manage";
    public const string RateBuyRead = "rate.buy.read";
    public const string RateBuyWrite = "rate.buy.write";
    public const string RateBuyPublish = "rate.buy.publish";
    public const string RateSellRead = "rate.sell.read";
    public const string RateSellWrite = "rate.sell.write";
    public const string RateSellPublish = "rate.sell.publish";
    public const string RateQuantityOverride = "rate.quantity.override";
    public const string RateRerate = "rate.rerate";

    public static readonly IReadOnlyList<(string Code, string Name)> CoreCatalog =
    [
        (BillCreate, "Tạo Bill"),
        (BillRead, "Xem Bill"),
        (BillUpdate, "Cập nhật ngữ cảnh Bill"),
        (CostCreate, "Tạo chi phí"),
        (CostRead, "Xem chi phí"),
        (CostConfirm, "Xác nhận chi phí"),
        (CostActualize, "Thực tế hóa chi phí"),
        (CostAllocationOverride, "Sửa kết quả phân bổ tự động"),
        (RevenueRead, "Xem doanh thu"),
        (RevenueConfirm, "Xác nhận doanh thu"),
        (RevenueActualize, "Thực tế hóa doanh thu"),
        (RevenueMappingOverride, "Sửa kết quả chia doanh thu tự động"),
        (ApWriteOff, "Xóa nợ phải trả"),
        (ArWriteOff, "Xóa nợ phải thu"),
        (MasterOrgManage, "Quản lý tổ chức"),
        (MasterPartyManage, "Quản lý đối tác"),
        (MasterCurrencyManage, "Quản lý tiền tệ"),
        (MasterCatalogManage, "Quản lý danh mục loại"),
        (UserManage, "Quản lý người dùng"),
        (RoleManage, "Quản lý vai trò"),
        (SettingsManage, "Quản lý cài đặt thuê bao"),
        (AuditRead, "Xem nhật ký hệ thống"),
        (LicenseManage, "Quản lý license"),
        (BackupManage, "Sao lưu và khôi phục danh mục"),
        (NotificationManage, "Cài đặt thông báo"),
        (RateBuyRead, "Xem bảng giá mua"),
        (RateBuyWrite, "Sửa bảng giá mua"),
        (RateBuyPublish, "Phát hành bảng giá mua"),
        (RateSellRead, "Xem bảng giá bán"),
        (RateSellWrite, "Sửa bảng giá bán"),
        (RateSellPublish, "Phát hành bảng giá bán"),
        (RateQuantityOverride, "Ghi đè số lượng tính giá"),
        (RateRerate, "Tính lại giá")
    ];

    public static readonly string[] CoreActionCodes =
    [
        BillCreate,
        BillRead,
        BillUpdate,
        CostCreate,
        CostRead,
        CostConfirm,
        CostActualize,
        CostAllocationOverride,
        RevenueRead,
        RevenueConfirm,
        RevenueActualize,
        RevenueMappingOverride,
        ApWriteOff,
        ArWriteOff,
        MasterOrgManage,
        MasterPartyManage,
        MasterCurrencyManage,
        MasterCatalogManage,
        UserManage,
        RoleManage,
        SettingsManage,
        AuditRead,
        LicenseManage,
        BackupManage,
        NotificationManage,
        RateBuyRead,
        RateBuyWrite,
        RateBuyPublish,
        RateSellRead,
        RateSellWrite,
        RateSellPublish,
        RateQuantityOverride,
        RateRerate
    ];
}

