namespace LCMS.Domain.Identity;

/// <summary>Stable Action codes for Role × Permission (Sprint 1 skeleton).</summary>
public static class PermissionCodes
{
    public const string BillCreate = "bill.create";
    public const string BillRead = "bill.read";
    public const string CostCreate = "cost.create";
    public const string CostRead = "cost.read";
    /// <summary>View revenue — independent of cost.read (H View Cost ≠ Revenue).</summary>
    public const string RevenueRead = "revenue.read";
    public const string MasterOrgManage = "master.org.manage";
    public const string MasterPartyManage = "master.party.manage";
    public const string MasterCurrencyManage = "master.currency.manage";
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";
    public const string SettingsManage = "settings.manage";

    public static readonly IReadOnlyList<(string Code, string Name)> CoreCatalog =
    [
        (BillCreate, "Tạo Bill"),
        (BillRead, "Xem Bill"),
        (CostCreate, "Tạo chi phí"),
        (CostRead, "Xem chi phí"),
        (RevenueRead, "Xem doanh thu"),
        (MasterOrgManage, "Quản lý tổ chức"),
        (MasterPartyManage, "Quản lý đối tác"),
        (MasterCurrencyManage, "Quản lý tiền tệ"),
        (UserManage, "Quản lý người dùng"),
        (RoleManage, "Quản lý vai trò"),
        (SettingsManage, "Quản lý cài đặt thuê bao")
    ];

    public static readonly string[] CoreActionCodes =
    [
        BillCreate,
        BillRead,
        CostCreate,
        CostRead,
        RevenueRead,
        MasterOrgManage,
        MasterPartyManage,
        MasterCurrencyManage,
        UserManage,
        RoleManage,
        SettingsManage
    ];
}
