namespace LCMS.Domain.Identity;

/// <summary>Stable Action codes for Role × Permission (Sprint 1 skeleton).</summary>
public static class PermissionCodes
{
    public const string BillCreate = "bill.create";
    public const string BillRead = "bill.read";
    public const string MasterOrgManage = "master.org.manage";
    public const string MasterPartyManage = "master.party.manage";
    public const string MasterCurrencyManage = "master.currency.manage";
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";

    public static readonly IReadOnlyList<(string Code, string Name)> CoreCatalog =
    [
        (BillCreate, "Tạo Bill"),
        (BillRead, "Xem Bill"),
        (MasterOrgManage, "Quản lý tổ chức"),
        (MasterPartyManage, "Quản lý đối tác"),
        (MasterCurrencyManage, "Quản lý tiền tệ"),
        (UserManage, "Quản lý người dùng"),
        (RoleManage, "Quản lý vai trò")
    ];

    public static readonly string[] CoreActionCodes =
    [
        BillCreate,
        BillRead,
        MasterOrgManage,
        MasterPartyManage,
        MasterCurrencyManage,
        UserManage,
        RoleManage
    ];
}
