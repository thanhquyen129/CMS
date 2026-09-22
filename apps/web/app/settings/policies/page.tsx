import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { PolicyRegistryPanel } from "@/components/PolicyRegistryPanel";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";

export default async function SettingsPoliciesPage() {
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
            { label: "Sổ chính sách" },
          ]}
          title="Sổ chính sách"
          lede="13 khóa chuẩn MASTER (FR-011 / POL-01): chủ sở hữu, phiên bản, ngày hiệu lực. Không silent overwrite bản đang hiệu lực."
        />
        <SettingsHubNav active="policies" />
        <PolicyRegistryPanel />
      </section>
    </AppShell>
  );
}
