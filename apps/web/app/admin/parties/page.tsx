import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AdminPartyListWorkspace } from "@/components/AdminPartyListWorkspace";
import { CreateBusinessPartyForm } from "@/components/CreateBusinessPartyForm";
import { FilterBar, ListPageHeader } from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listAdminParties } from "@/lib/master-data";
import { PARTY_ROLE_OPTIONS } from "@/lib/party";

type Search = {
  q?: string;
  role?: string;
  status?: string;
};

export default async function AdminPartiesPage({
  searchParams,
}: {
  searchParams: Promise<Search>;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const sp = await searchParams;
  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");
  const result = await listAdminParties({
    search: sp.q,
    roleCode: sp.role,
    isActive:
      sp.status === "active"
        ? "true"
        : sp.status === "inactive"
          ? "false"
          : undefined,
  });

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: dashboardLabel },
            { href: "/admin", label: "Danh mục" },
            { label: "Đối tác" },
          ]}
          title="Đối tác kinh doanh"
          lede="Hồ sơ đối tác chuẩn tài chính: MST, vai trò, điều khoản thanh toán, hạn mức công nợ, tài khoản ngân hàng và người liên hệ."
        />

        <div className="hub-module-tabs" role="tablist" aria-label="Danh mục dữ liệu">
          <Link className="hub-module-tab is-active" href="/admin/parties" role="tab" aria-selected="true">
            <strong>Đối tác</strong>
            <span>Khách hàng / NCC</span>
          </Link>
          <Link className="hub-module-tab" href="/admin/access" role="tab" aria-selected="false">
            <strong>Phân quyền</strong>
            <span>Vai trò × quyền</span>
          </Link>
          <Link className="hub-module-tab" href="/admin/organizations" role="tab" aria-selected="false">
            <strong>Đơn vị / Tổ chức</strong>
            <span>Phạm vi dữ liệu</span>
          </Link>
          <Link className="hub-module-tab" href="/admin/currencies" role="tab" aria-selected="false">
            <strong>Tiền tệ</strong>
            <span>Danh mục tiền tệ</span>
          </Link>
        </div>

        <FilterBar
          action="/admin/parties"
          resetHref={sp.q || sp.role || sp.status ? "/admin/parties" : undefined}
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm kiếm",
              placeholder: "Mã, tên, MST…",
              defaultValue: sp.q,
            },
            {
              kind: "select",
              name: "role",
              label: "Vai trò",
              defaultValue: sp.role,
              emptyLabel: "Tất cả vai trò",
              options: PARTY_ROLE_OPTIONS.map((r) => ({
                value: r.code,
                label: r.label,
              })),
            },
            {
              kind: "select",
              name: "status",
              label: "Trạng thái",
              defaultValue: sp.status,
              emptyLabel: "Tất cả trạng thái",
              options: [
                { value: "active", label: "Đang dùng" },
                { value: "inactive", label: "Ngừng" },
              ],
            },
          ]}
        />

        <div className="layout-cols-2" style={{ marginTop: "1rem" }}>
          <fieldset className="group-box">
            <legend>Danh sách</legend>
            {!result.ok ? (
              <div className="alert alert-error" role="alert">
                {result.message}
              </div>
            ) : result.data.length === 0 ? (
              <div className="empty-state" role="status">
                Chưa có đối tác khớp bộ lọc.
              </div>
            ) : (
              <AdminPartyListWorkspace parties={result.data} />
            )}
          </fieldset>

          <fieldset className="group-box">
            <legend>Thêm đối tác</legend>
            <CreateBusinessPartyForm />
          </fieldset>
        </div>
      </section>
    </AppShell>
  );
}
