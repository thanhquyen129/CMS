import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { SettingsForm } from "@/components/SettingsForm";
import { TenantReadinessPanel } from "@/components/TenantReadinessPanel";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { getTenantReadiness } from "@/lib/tenant-admin";

export default async function SettingsPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const readiness = await getTenantReadiness();

  return (
    <AppShell terms={terms} active="settings">
      <div className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: "Hệ thống & Cài đặt" },
          ]}
          title="Cấu hình hệ thống"
          lede="Giao diện trình duyệt (cookie máy này). Ngưỡng tài chính nằm ở Cấu hình nghiệp vụ. Không thay sổ tiền."
        />
        <SettingsHubNav active="system" />
        {readiness.ok ? <TenantReadinessPanel data={readiness.data} /> : null}
        <SettingsForm />
      </div>
    </AppShell>
  );
}
