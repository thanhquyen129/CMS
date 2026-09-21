import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { CreateUserForm } from "@/components/CreateUserForm";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { SettingsHubNav } from "@/components/SettingsHubNav";
import { StatCardGrid } from "@/components/list/StatCardGrid";
import { UserAccountTable } from "@/components/UserAccountTable";
import { AUTH_COOKIE } from "@/lib/auth";
import { listAccessRoles, listUserRoles, type UserRoleItem } from "@/lib/access";
import { fetchTerminology } from "@/lib/api";
import { listOrganizations } from "@/lib/master-data";
import { getTenantLicense, listAdminUsers } from "@/lib/tenant-admin";

export default async function SettingsUsersPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const [usersResult, orgsResult, rolesResult, licenseResult] = await Promise.all([
    listAdminUsers(),
    listOrganizations(),
    listAccessRoles(),
    getTenantLicense(),
  ]);

  if (!usersResult.ok) {
    return (
      <AppShell terms={terms} active="settings">
        <section className="panel panel-wide">
          <ListPageHeader
            breadcrumbs={[
              { href: "/dashboard", label: "Trang chủ" },
              { href: "/settings", label: "Hệ thống" },
              { label: "Người dùng" },
            ]}
            title="Người dùng"
          />
          <SettingsHubNav active="users" />
          <div className="alert alert-error" role="alert">
            {usersResult.message}
          </div>
        </section>
      </AppShell>
    );
  }

  const users = usersResult.data;
  const orgs = orgsResult.ok ? orgsResult.data : [];
  const roles = rolesResult.ok ? rolesResult.data : [];
  const license = licenseResult.ok ? licenseResult.data : null;
  const userRolesByUserId: Record<string, UserRoleItem[]> = {};
  await Promise.all(
    users.map(async (u) => {
      const r = await listUserRoles(u.id);
      userRolesByUserId[u.id] = r.ok ? r.data : [];
    })
  );
  const active = users.filter((u) => u.isActive).length;
  const unset = users.filter((u) => !u.passwordSet).length;

  return (
    <AppShell terms={terms} active="settings">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/settings", label: "Hệ thống" },
            { label: "Người dùng" },
          ]}
          title="Người dùng"
          lede="Tạo tài khoản, đặt mật khẩu, ngừng dùng. Chỗ license tính tài khoản đang hoạt động. Không xóa — ngừng để giữ audit."
        />
        <SettingsHubNav active="users" />
        <StatCardGrid
          cards={[
            { key: "all", label: "Tài khoản", value: users.length },
            { key: "active", label: "Đang dùng", value: active, tone: "success" },
            { key: "off", label: "Ngừng", value: users.length - active, tone: "warning" },
            {
              key: "seats",
              label: "Chỗ license",
              value: license ? `${license.seatsUsed}/${license.seatLimit}` : "—",
              tone: license && license.seatsUsed >= license.seatLimit ? "danger" : "info",
            },
            {
              key: "pw",
              label: "Chưa đặt mật khẩu",
              value: unset,
              tone: unset > 0 ? "danger" : "default",
            },
          ]}
        />
        <div className="stack-panels">
          <fieldset className="group-box">
            <legend>Danh sách</legend>
            <UserAccountTable
              users={users}
              organizations={orgs}
              userRolesByUserId={userRolesByUserId}
            />
          </fieldset>
          <fieldset className="group-box form-aside">
            <legend>Tạo người dùng</legend>
            <CreateUserForm organizations={orgs} roles={roles} />
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
