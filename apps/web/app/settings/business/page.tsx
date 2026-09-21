import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { TenantFinancialSettingsForm } from "@/components/TenantFinancialSettingsForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

export default async function SettingsBusinessPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();

  return (
    <AppShell terms={terms} active="settings">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/settings", label: "Hệ thống" },
            { label: "Cấu hình nghiệp vụ" },
          ]}
          title="Cấu hình nghiệp vụ"
          lede="Ngưỡng xóa nợ, phê duyệt xác nhận, chính sách ghi nhận doanh thu của thuê bao. Ghi đè node khi đã lưu."
        />
        <SettingsHubNav active="business" />
        <TenantFinancialSettingsForm />
      </section>
    </AppShell>
  );
}
