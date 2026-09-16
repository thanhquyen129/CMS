import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { SettingsForm } from "@/components/SettingsForm";
import { TenantFinancialSettingsForm } from "@/components/TenantFinancialSettingsForm";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

export default async function SettingsPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();

  const links = [
    {
      href: "/admin/access",
      title: "Phân quyền",
      desc: "Vai trò, gán thành viên, quyền hành động × phạm vi.",
    },
    {
      href: "/admin",
      title: "Danh mục dữ liệu",
      desc: "Đối tác, đơn vị tổ chức, tiền tệ.",
    },
    {
      href: "/integration-errors",
      title: "Lỗi tích hợp",
      desc: "Hàng đợi lỗi đồng bộ / dead-letter cần xử lý.",
    },
    {
      href: "/workflow",
      title: "Bản đồ luồng hệ thống",
      desc: "Điều hướng end-to-end theo 11 bước nghiệp vụ.",
    },
  ];

  return (
    <AppShell terms={terms} active="settings">
      <div className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: "Hệ thống & Cài đặt" },
          ]}
          title="Hệ thống & Cài đặt"
          lede="Giao diện (cookie), cấu hình nghiệp vụ thuê bao, và liên kết quản trị. Ngưỡng ghi đè node khi đã lưu."
        />

        <div className="hub-module-tabs" role="navigation" aria-label="Liên kết hệ thống">
          {links.map((item) => (
            <Link key={item.href} href={item.href} className="hub-module-tab">
              <strong>{item.title}</strong>
              <span>{item.desc}</span>
            </Link>
          ))}
        </div>

        <SettingsForm />
        <TenantFinancialSettingsForm />
      </div>
    </AppShell>
  );
}
