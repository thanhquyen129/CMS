import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CompanyLogoForm } from "@/components/CompanyLogoForm";
import { CompanyProfileForm } from "@/components/CompanyProfileForm";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { listCurrencies } from "@/lib/master-data";
import { getTenantProfile } from "@/lib/tenant-admin";

export default async function SettingsCompanyPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const [profileResult, currenciesResult] = await Promise.all([
    getTenantProfile(),
    listCurrencies(),
  ]);

  return (
    <AppShell terms={terms} active="settings">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/settings", label: "Hệ thống" },
            { label: "Thông tin doanh nghiệp" },
          ]}
          title="Thông tin doanh nghiệp"
          lede="Hồ sơ pháp lý của thuê bao: MST, địa chỉ, múi giờ hiển thị, tiền tệ mặc định, logo chứng từ."
        />
        <SettingsHubNav active="company" />
        {!profileResult.ok ? (
          <div className="alert alert-error" role="alert">
            {profileResult.message}
          </div>
        ) : (
          <div className="layout-cols-2">
            <fieldset className="group-box">
              <legend>Hồ sơ</legend>
              <CompanyProfileForm
                profile={profileResult.data}
                currencies={currenciesResult.ok ? currenciesResult.data : []}
              />
            </fieldset>
            <fieldset className="group-box">
              <legend>Logo</legend>
              <CompanyLogoForm hasLogo={profileResult.data.hasLogo} />
            </fieldset>
          </div>
        )}
      </section>
    </AppShell>
  );
}
