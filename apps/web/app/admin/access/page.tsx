import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { RolePermissionToggleMatrix } from "@/components/RolePermissionToggleMatrix";
import { UserRoleAssignPanel } from "@/components/UserRoleAssignPanel";
import { AUTH_COOKIE } from "@/lib/auth";
import {
  getRolePermissionMatrix,
  listAccessRoles,
  listAccessUsers,
  listUserRoles,
  type UserRoleItem,
} from "@/lib/access";
import { fetchTerminology, term } from "@/lib/api";

type Search = { role?: string };

export default async function AdminAccessPage({
  searchParams,
}: {
  searchParams: Promise<Search>;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const sp = await searchParams;

  const [rolesResult, usersResult] = await Promise.all([
    listAccessRoles(),
    listAccessUsers(),
  ]);

  if (!rolesResult.ok || !usersResult.ok) {
    const message =
      (!rolesResult.ok && rolesResult.message) ||
      (!usersResult.ok && usersResult.message) ||
      "Không tải được phân quyền.";
    return (
      <AppShell terms={terms} active="admin">
        <section className="panel panel-wide">
          <p className="breadcrumb">
            <Link href="/dashboard">{dashboardLabel}</Link>
            {" / "}
            <Link href="/admin">Danh mục</Link>
            {" / "}
            Phân quyền
          </p>
          <h1>Phân quyền</h1>
          <div className="alert alert-error" role="alert">
            {message}
          </div>
        </section>
      </AppShell>
    );
  }

  const roles = rolesResult.data;
  const users = usersResult.data;
  const selectedCode = sp.role ?? "Admin";
  const selected =
    roles.find((r) => r.code === selectedCode) ?? roles[0] ?? null;

  const matrixResult = selected
    ? await getRolePermissionMatrix(selected.id)
    : null;

  const userRolesByUserId: Record<string, UserRoleItem[]> = {};
  await Promise.all(
    users.map(async (u) => {
      const r = await listUserRoles(u.id);
      userRolesByUserId[u.id] = r.ok ? r.data : [];
    })
  );

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">{dashboardLabel}</Link>
          {" / "}
          <Link href="/admin">Danh mục</Link>
          {" / "}
          Phân quyền
        </p>
        <h1>Phân quyền</h1>
        <p className="lede">
          Gán vai trò cho thành viên và bật/tắt quyền theo nhóm nghiệp vụ. Chi
          phí và doanh thu độc lập; phê duyệt không thay thế quyền.
        </p>

        <UserRoleAssignPanel
          users={users}
          roles={roles}
          userRolesByUserId={userRolesByUserId}
        />

        <fieldset className="group-box" style={{ marginTop: "1.25rem" }}>
          <legend>Vai trò hệ thống</legend>
          <div className="card-grid">
            {roles.map((r) => (
              <Link
                key={r.id}
                href={`/admin/access?role=${encodeURIComponent(r.code)}`}
                className="panel"
                style={{
                  textDecoration: "none",
                  display: "block",
                  outline:
                    selected?.id === r.id
                      ? "2px solid var(--accent, #1a5f4a)"
                      : undefined,
                }}
              >
                <h3 className="section-title">{r.name}</h3>
                <p className="mono-id muted">{r.code}</p>
                <p className="muted">{r.summaryVi || "Vai trò tùy chỉnh."}</p>
              </Link>
            ))}
          </div>
        </fieldset>

        {selected && matrixResult?.ok ? (
          <div style={{ marginTop: "1.25rem" }}>
            <RolePermissionToggleMatrix
              roleId={selected.id}
              roleName={selected.name}
              items={matrixResult.data}
            />
          </div>
        ) : selected && matrixResult && !matrixResult.ok ? (
          <div className="alert alert-error" role="alert">
            {matrixResult.message}
          </div>
        ) : null}
      </section>
    </AppShell>
  );
}
