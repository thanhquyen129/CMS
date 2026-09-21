import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { LicenseModulesForm } from "@/components/LicenseModulesForm";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { getTenantLicense } from "@/lib/tenant-admin";

export default async function SettingsLicensePage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const result = await getTenantLicense();

  return (
    <AppShell terms={terms} active="settings">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/settings", label: "Hệ thống" },
            { label: "License" },
          ]}
          title="Quản lý license"
          lede="Gói, chỗ người dùng và module. Tắt module chỉ ẩn UI — không xóa dữ liệu, không fork schema."
        />
        <SettingsHubNav active="license" />
        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : (
          <LicenseModulesForm license={result.data} />
        )}
      </section>
    </AppShell>
  );
}
