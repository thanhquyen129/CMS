using LCMS.Domain.Common;

namespace LCMS.Domain.Entities;

/// <summary>Table: tenant_licenses — commercial seat/plan for the tenant (ADR-0021).</summary>
public sealed class TenantLicense : TenantEntityBase
{
    public string PlanCode { get; set; } = TenantLicensePlans.Professional;
    public string PlanName { get; set; } = "Professional";
    public int SeatLimit { get; set; } = 50;
    public DateTimeOffset ValidFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ValidUntil { get; set; } = DateTimeOffset.UtcNow.AddYears(2);
    public string Status { get; set; } = TenantLicenseStatuses.Active;
    public string? Notes { get; set; }
}

public static class TenantLicensePlans
{
    public const string Starter = "starter";
    public const string Professional = "professional";
    public const string Enterprise = "enterprise";
}

public static class TenantLicenseStatuses
{
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Suspended = "suspended";
}

public static class TenantModules
{
    public const string Dashboard = "dashboard";
    public const string Bills = "bills";
    public const string Rates = "rates";
    public const string Costs = "costs";
    public const string Revenues = "revenues";
    public const string Documents = "documents";
    public const string Ap = "ap";
    public const string Ar = "ar";
    public const string Settlements = "settlements";
    public const string Control = "control";
    public const string Closes = "closes";
    public const string Reports = "reports";
    public const string Master = "master";
    public const string Admin = "admin";

    public static readonly IReadOnlyList<(string Code, string NameVi)> Catalog =
    [
        (Dashboard, "Trang chủ"),
        (Bills, "Đơn hàng vận chuyển"),
        (Rates, "Bảng giá & Tính giá"),
        (Costs, "Chi phí"),
        (Revenues, "Doanh thu"),
        (Documents, "Chứng từ tài chính"),
        (Ap, "Công nợ phải trả"),
        (Ar, "Công nợ phải thu"),
        (Settlements, "Thanh toán & Thu tiền"),
        (Control, "Kiểm soát tài chính"),
        (Closes, "Chốt tài chính"),
        (Reports, "Báo cáo & Phân tích"),
        (Master, "Danh mục dữ liệu"),
        (Admin, "Hệ thống & Cài đặt")
    ];

    public static IReadOnlyList<string> AllCodes => Catalog.Select(c => c.Code).ToArray();
}
