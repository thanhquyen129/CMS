import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AdminPartyListWorkspace } from "@/components/AdminPartyListWorkspace";
import { FilterBar, ListPageHeader } from "@/components/list";
import { ListPagination } from "@/components/ListPagination";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  getPartyDirectorySummary,
  listPartyDirectory,
} from "@/lib/parties";
import { PARTY_KIND_OPTIONS, PARTY_ROLE_OPTIONS } from "@/lib/party";
import { parsePage, parsePageSize, totalPages } from "@/lib/list-paging";

type Search = {
  q?: string;
  role?: string;
  status?: string;
  kind?: string;
  group?: string;
  page?: string;
  pageSize?: string;
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
  const pageSize = parsePageSize(sp.pageSize);
  const requestedPage = Math.max(1, Number.parseInt(sp.page ?? "1", 10) || 1);
  const [result, summaryResult] = await Promise.all([
    listPartyDirectory({
      search: sp.q,
      roleCode: sp.role,
      status: sp.status,
      kind: sp.kind,
      groupCode: sp.group,
      page: String(requestedPage),
      pageSize: String(pageSize),
    }),
    getPartyDirectorySummary({
      search: sp.q,
      roleCode: sp.role,
      status: sp.status,
      kind: sp.kind,
      groupCode: sp.group,
    }),
  ]);

  const totalCount = result.ok ? result.data.totalCount : 0;
  const pages = totalPages(totalCount, pageSize);
  const page = parsePage(sp.page, pages);
  const exportQs = new URLSearchParams();
  if (sp.q) exportQs.set("search", sp.q);
  if (sp.role) exportQs.set("roleCode", sp.role);
  if (sp.status) exportQs.set("status", sp.status);
  if (sp.kind) exportQs.set("kind", sp.kind);
  if (sp.group) exportQs.set("groupCode", sp.group);
  const exportHref = `/bff/admin/parties/export${exportQs.toString() ? `?${exportQs}` : ""}`;

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: dashboardLabel },
            { href: "/admin", label: "Danh mục" },
            { label: "Đối tác" },
          ]}
          title="Khách hàng & đối tác"
          lede="Hồ sơ chuẩn tài chính: MST, vai trò, điều khoản, hạn mức, tài khoản ngân hàng, liên hệ và công nợ. Một đối tác có thể vừa là khách hàng vừa là nhà cung cấp."
          action={
            <div className="page-header-actions">
              <a className="btn btn-ghost" href={exportHref}>
                Xuất CSV
              </a>
              <Link className="btn" href="/admin/parties/new">
                Thêm đối tác
              </Link>
            </div>
          }
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

        {summaryResult.ok ? (
          <div className="po-kpi-row" role="list" style={{ margin: "1rem 0" }}>
            <div className="po-kpi" role="listitem">
              <span className="muted">Tổng hồ sơ</span>
              <strong>{summaryResult.data.total}</strong>
            </div>
            <div className="po-kpi po-kpi-rev" role="listitem">
              <span className="muted">Đang dùng</span>
              <strong>{summaryResult.data.active}</strong>
            </div>
            <div className="po-kpi" role="listitem">
              <span className="muted">Ngừng</span>
              <strong>{summaryResult.data.inactive}</strong>
            </div>
            <div className="po-kpi po-kpi-cost" role="listitem">
              <span className="muted">Bị chặn giao dịch</span>
              <strong>{summaryResult.data.blocked}</strong>
            </div>
          </div>
        ) : null}

        <FilterBar
          action="/admin/parties"
          resetHref={
            sp.q || sp.role || sp.status || sp.kind || sp.group
              ? "/admin/parties"
              : undefined
          }
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm kiếm",
              placeholder: "Mã, tên, MST, SĐT, email…",
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
                { value: "blocked", label: "Bị chặn" },
              ],
            },
            {
              kind: "select",
              name: "kind",
              label: "Loại",
              defaultValue: sp.kind,
              emptyLabel: "Tất cả loại",
              options: PARTY_KIND_OPTIONS.map((k) => ({
                value: k.code,
                label: k.label,
              })),
            },
            {
              kind: "search",
              name: "group",
              label: "Nhóm",
              placeholder: "Nhóm đối tác",
              defaultValue: sp.group,
            },
          ]}
        />

        <fieldset className="group-box" style={{ marginTop: "1rem" }}>
          <legend>Danh sách</legend>
          {!result.ok ? (
            <div className="alert alert-error" role="alert">
              {result.message}
            </div>
          ) : result.data.items.length === 0 ? (
            <div className="empty-state" role="status">
              Chưa có đối tác khớp bộ lọc.{" "}
              <Link href="/admin/parties/new">Thêm đối tác mới</Link>.
            </div>
          ) : (
            <AdminPartyListWorkspace parties={result.data.items} />
          )}
          {result.ok ? (
            <ListPagination
              basePath="/admin/parties"
              params={{
                q: sp.q,
                role: sp.role,
                status: sp.status,
                kind: sp.kind,
                group: sp.group,
              }}
              page={page}
              pageSize={pageSize}
              totalCount={totalCount}
              totalPages={pages}
            />
          ) : null}
        </fieldset>
      </section>
    </AppShell>
  );
}
