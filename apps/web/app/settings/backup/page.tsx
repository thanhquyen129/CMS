import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { BackupWorkspace } from "@/components/BackupWorkspace";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { getTenantProfile, listTenantBackups } from "@/lib/tenant-admin";

export default async function SettingsBackupPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const [profileResult, backupsResult] = await Promise.all([
    getTenantProfile(),
    listTenantBackups(),
  ]);

  return (
    <AppShell terms={terms} active="settings">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/settings", label: "Hệ thống" },
            { label: "Sao lưu" },
          ]}
          title="Sao lưu &amp; Khôi phục"
          lede="Bản sao logic danh mục/cấu hình của thuê bao. Khôi phục không đụng sổ tiền. PITR database là việc vận hành máy chủ."
        />
        <SettingsHubNav active="backup" />
        {!profileResult.ok ? (
          <div className="alert alert-error" role="alert">
            {profileResult.message}
          </div>
        ) : !backupsResult.ok ? (
          <div className="alert alert-error" role="alert">
            {backupsResult.message}
          </div>
        ) : (
          <BackupWorkspace tenantCode={profileResult.data.code} items={backupsResult.data} />
        )}
      </section>
    </AppShell>
  );
}
