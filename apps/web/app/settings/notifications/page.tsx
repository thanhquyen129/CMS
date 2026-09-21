import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { NotificationInbox } from "@/components/NotificationInbox";
import { NotificationSettingsForm } from "@/components/NotificationSettingsForm";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { getNotificationSettings, listInbox } from "@/lib/tenant-admin";

export default async function SettingsNotificationsPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const [settingsResult, inboxResult] = await Promise.all([
    getNotificationSettings(),
    listInbox(false),
  ]);

  return (
    <AppShell terms={terms} active="settings">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/settings", label: "Hệ thống" },
            { label: "Thông báo" },
          ]}
          title="Cài đặt thông báo"
          lede="In-app theo tài khoản. Email qua outbox. Nếu SMTP chưa cấu hình, hệ thống ghi ‘chưa gửi’ — không giả đã gửi."
        />
        <SettingsHubNav active="notifications" />
        <div className="layout-cols-2">
          <fieldset className="group-box">
            <legend>Kênh và sự kiện</legend>
            {!settingsResult.ok ? (
              <div className="alert alert-error" role="alert">
                {settingsResult.message}
              </div>
            ) : (
              <NotificationSettingsForm settings={settingsResult.data} />
            )}
          </fieldset>
          <fieldset className="group-box">
            <legend>Hộp thư của tôi</legend>
            {!inboxResult.ok ? (
              <div className="alert alert-error" role="alert">
                {inboxResult.message}
              </div>
            ) : (
              <NotificationInbox items={inboxResult.data} />
            )}
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
