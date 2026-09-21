import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { SampleDataWorkspace } from "@/components/SampleDataWorkspace";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { getSampleDataStatus } from "@/lib/tenant-admin";

export default async function SettingsSampleDataPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const status = await getSampleDataStatus();

  return (
    <AppShell terms={terms} active="settings">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/settings", label: "Hệ thống" },
            { label: "Dữ liệu mẫu" },
          ]}
          title="Dữ liệu mẫu kiểm thử"
          lede="Bổ sung ≥120 bản ghi mỗi loại (đơn, Bill, Shipment, chi phí, doanh thu, chứng từ, AP/AR…). Không sinh Cost/Revenue từ chứng từ. Idempotent — bấm lại chỉ điền phần còn thiếu."
        />
        <SettingsHubNav active="sample-data" />
        {!status.ok ? (
          <div className="alert alert-error" role="alert">
            {status.message}
          </div>
        ) : (
          <SampleDataWorkspace status={status.data} />
        )}
      </section>
    </AppShell>
  );
}
