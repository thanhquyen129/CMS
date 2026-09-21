import Link from "next/link";

type Props = {
  active:
    | "company"
    | "users"
    | "access"
    | "business"
    | "system"
    | "integrations"
    | "audit"
    | "license"
    | "notifications"
    | "backup"
    | "sample-data";
};

const TABS: { id: Props["active"]; href: string; title: string; desc: string }[] = [
  { id: "company", href: "/settings/company", title: "Thông tin doanh nghiệp", desc: "MST, địa chỉ, múi giờ" },
  { id: "users", href: "/settings/users", title: "Người dùng", desc: "Tạo, ngừng, mật khẩu" },
  { id: "access", href: "/admin/access", title: "Vai trò & Phân quyền", desc: "Action × phạm vi" },
  { id: "business", href: "/settings/business", title: "Cấu hình nghiệp vụ", desc: "Ngưỡng xóa nợ / phê duyệt" },
  { id: "system", href: "/settings", title: "Cấu hình hệ thống", desc: "Giao diện trình duyệt" },
  { id: "integrations", href: "/settings/integrations", title: "Tích hợp API", desc: "Bản ghi và lỗi đồng bộ" },
  { id: "audit", href: "/settings/audit", title: "Nhật ký hệ thống", desc: "Audit theo ngày / hành động" },
  { id: "license", href: "/settings/license", title: "Quản lý license", desc: "Gói, chỗ, module" },
  { id: "notifications", href: "/settings/notifications", title: "Cài đặt thông báo", desc: "In-app và email" },
  { id: "backup", href: "/settings/backup", title: "Sao lưu & Khôi phục", desc: "Danh mục / cấu hình" },
  { id: "sample-data", href: "/settings/sample-data", title: "Dữ liệu mẫu", desc: "≥120 bản ghi mỗi loại" },
];

export function SettingsHubNav({ active }: Props) {
  return (
    <div className="hub-module-tabs" role="tablist" aria-label="Hệ thống và cài đặt">
      {TABS.map((item) => (
        <Link
          key={item.id}
          href={item.href}
          className={`hub-module-tab${active === item.id ? " is-active" : ""}`}
          role="tab"
          aria-selected={active === item.id}
        >
          <strong>{item.title}</strong>
          <span>{item.desc}</span>
        </Link>
      ))}
    </div>
  );
}
