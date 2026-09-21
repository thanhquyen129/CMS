using LCMS.Domain.Entities;

namespace LCMS.Domain.Identity;

/// <summary>
/// Built-in tenant roles for logistics financial control (ADR-0016).
/// Permission = Action × Data Scope; Approval remains separate.
/// </summary>
public static class SystemRoleCatalog
{
    public const string Admin = "Admin";
    public const string FinancialController = "FinancialController";
    public const string CostAccountant = "CostAccountant";
    public const string RevenueAccountant = "RevenueAccountant";
    public const string Ops = "Ops";
    public const string MasterData = "MasterData";
    public const string Viewer = "Viewer";

    public sealed record RoleDefinition(
        string Code,
        string Name,
        string SummaryVi,
        IReadOnlyList<(string ActionCode, string DataScope)> Permissions);

    /// <summary>Seven system roles — templates Admin can toggle per tenant.</summary>
    public static readonly IReadOnlyList<RoleDefinition> All =
    [
        new(
            Admin,
            "Quản trị",
            "Toàn quyền thuê bao: người dùng, vai trò, cài đặt, master data và toàn bộ nghiệp vụ tiền.",
            PermissionCodes.CoreActionCodes
                .Select(c => (c, DataScopes.All))
                .ToArray()),

        new(
            FinancialController,
            "Kiểm soát tài chính",
            "Xem và chốt chi phí/doanh thu, xóa nợ AP/AR, cài đặt tài chính. Không quản lý user/role.",
            [
                (PermissionCodes.BillCreate, DataScopes.All),
                (PermissionCodes.BillRead, DataScopes.All),
                (PermissionCodes.BillUpdate, DataScopes.All),
                (PermissionCodes.CostCreate, DataScopes.All),
                (PermissionCodes.CostRead, DataScopes.All),
                (PermissionCodes.CostConfirm, DataScopes.All),
                (PermissionCodes.CostActualize, DataScopes.All),
                (PermissionCodes.RevenueRead, DataScopes.All),
                (PermissionCodes.RevenueConfirm, DataScopes.All),
                (PermissionCodes.RevenueActualize, DataScopes.All),
                (PermissionCodes.ApWriteOff, DataScopes.All),
                (PermissionCodes.ArWriteOff, DataScopes.All),
                (PermissionCodes.MasterOrgManage, DataScopes.All),
                (PermissionCodes.MasterPartyManage, DataScopes.All),
                (PermissionCodes.MasterCurrencyManage, DataScopes.All),
                (PermissionCodes.MasterCatalogManage, DataScopes.All),
                (PermissionCodes.SettingsManage, DataScopes.All),
                (PermissionCodes.AuditRead, DataScopes.All),
                (PermissionCodes.NotificationManage, DataScopes.All)
            ]),

        new(
            CostAccountant,
            "Kế toán chi phí",
            "Bill + chi phí (tạo/xem/xác nhận/thực tế) + xóa nợ AP. Không xem doanh thu/AR/margin.",
            [
                (PermissionCodes.BillCreate, DataScopes.Organization),
                (PermissionCodes.BillRead, DataScopes.Organization),
                (PermissionCodes.BillUpdate, DataScopes.Organization),
                (PermissionCodes.CostCreate, DataScopes.Organization),
                (PermissionCodes.CostRead, DataScopes.Organization),
                (PermissionCodes.CostConfirm, DataScopes.Organization),
                (PermissionCodes.CostActualize, DataScopes.Organization),
                (PermissionCodes.ApWriteOff, DataScopes.Organization)
            ]),

        new(
            RevenueAccountant,
            "Kế toán doanh thu",
            "Bill + doanh thu (xem/xác nhận/thực tế) + xóa nợ AR. Không xem chi phí/AP.",
            [
                (PermissionCodes.BillCreate, DataScopes.Organization),
                (PermissionCodes.BillRead, DataScopes.Organization),
                (PermissionCodes.BillUpdate, DataScopes.Organization),
                (PermissionCodes.RevenueRead, DataScopes.Organization),
                (PermissionCodes.RevenueConfirm, DataScopes.Organization),
                (PermissionCodes.RevenueActualize, DataScopes.Organization),
                (PermissionCodes.ArWriteOff, DataScopes.Organization)
            ]),

        new(
            Ops,
            "Điều vận",
            "Nhập Bill và chi phí dự kiến. Không xác nhận/thực tế hóa, không doanh thu, không xóa nợ, không admin.",
            [
                (PermissionCodes.BillCreate, DataScopes.Organization),
                (PermissionCodes.BillRead, DataScopes.Organization),
                (PermissionCodes.BillUpdate, DataScopes.Organization),
                (PermissionCodes.CostCreate, DataScopes.Organization),
                (PermissionCodes.CostRead, DataScopes.Organization)
            ]),

        new(
            MasterData,
            "Quản trị danh mục",
            "Tổ chức, đối tác, tiền tệ, danh mục loại. Chỉ xem Bill để đối chiếu — không sửa số tiền.",
            [
                (PermissionCodes.BillRead, DataScopes.All),
                (PermissionCodes.MasterOrgManage, DataScopes.All),
                (PermissionCodes.MasterPartyManage, DataScopes.All),
                (PermissionCodes.MasterCurrencyManage, DataScopes.All),
                (PermissionCodes.MasterCatalogManage, DataScopes.All)
            ]),

        new(
            Viewer,
            "Chỉ xem Bill",
            "Chỉ đọc Bill. Admin bật thêm cost.read / revenue.read khi cần (Cost ≠ Revenue).",
            [
                (PermissionCodes.BillRead, DataScopes.Organization)
            ])
    ];
}
