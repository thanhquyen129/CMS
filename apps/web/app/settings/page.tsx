import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { SettingsForm } from "@/components/SettingsForm";
import { TenantFinancialSettingsForm } from "@/components/TenantFinancialSettingsForm";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

export default async function SettingsPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();

  return (
    <AppShell terms={terms} active="settings">
      <div className="panel panel-wide">
        <p className="breadcrumb">
          <a href="/dashboard">Trang chủ</a>
          {" / "}
          Hệ thống &amp; Cài đặt
        </p>
        <h1>Hệ thống &amp; Cài đặt</h1>
        <p className="lede">
          Giao diện (cookie), cấu hình nghiệp vụ thuê bao, và liên kết quản trị.
          Ngưỡng ghi đè node khi đã lưu.
        </p>
        <SettingsForm />
        <TenantFinancialSettingsForm />
      </div>
    </AppShell>
  );
}
