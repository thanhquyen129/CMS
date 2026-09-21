import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { FilterBar, ListPageHeader, StatCardGrid } from "@/components/list";
import { ListPagination } from "@/components/ListPagination";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { operationalStatusLabel, transportModeLabel } from "@/lib/bills-shared";
import {
  parsePage,
  parsePageSize,
  slicePage,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";
import { formatDateTimeVi } from "@/lib/money";
import { listOrders, sourceSystemLabel } from "@/lib/operational-refs";

type SearchParams = Promise<{ q?: string; status?: string; page?: string; pageSize?: string }>;

export default async function OrdersPage({ searchParams }: { searchParams: SearchParams }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");

  const { q, status, page: pageRaw, pageSize: pageSizeRaw } = await searchParams;
  const terms = await fetchTerminology();
  const pageSize = parsePageSize(pageSizeRaw);
  const result = await listOrders(q);
  let rows = result.ok ? result.data : [];
  if (status?.trim()) {
    const s = status.trim().toLowerCase();
    rows = rows.filter((r) => r.operationalStatus?.toLowerCase() === s);
  }
  const pages = calcTotalPages(rows.length, pageSize);
  const page = parsePage(pageRaw, pages);
  const pageRows = slicePage(rows, page, pageSize);
  const draftCount = rows.filter((r) => r.operationalStatus?.toLowerCase() === "draft").length;
  const linked = rows.filter((r) => (r.relatedBillCount ?? 0) > 0).length;
  const statusOptions = Array.from(new Set(rows.map((r) => r.operationalStatus).filter(Boolean)));

  return (
    <AppShell terms={terms} active="bills" navChild="orders">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/bills", label: "Đơn hàng vận chuyển" },
            { label: "Danh sách đơn hàng" },
          ]}
          title="Danh sách đơn hàng"
          lede="Tham chiếu vận hành của khách hàng — neo cho Bill và Shipment. Không phải sổ điều vận TMS."
          action={
            <Link className="btn" href="/orders/new">
              + Tạo đơn hàng
            </Link>
          }
        />
        <FilterBar
          action="/orders"
          resetHref={q || status ? "/orders" : undefined}
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm đơn hàng",
              placeholder: "Số đơn, reference, tuyến…",
              defaultValue: q,
            },
            {
              kind: "select",
              name: "status",
              label: "Trạng thái",
              defaultValue: status,
              emptyLabel: "Tất cả trạng thái",
              options: statusOptions.map((s) => ({ value: s, label: operationalStatusLabel(s) })),
            },
          ]}
        />
        {result.ok ? (
          <StatCardGrid
            cards={[
              { key: "all", label: "Tổng số đơn", value: rows.length, tone: "primary" },
              { key: "draft", label: "Nháp", value: draftCount, tone: "warning" },
              { key: "linked", label: "Đã gắn Bill", value: linked, tone: "success" },
              { key: "page", label: "Trên trang này", value: pageRows.length },
            ]}
          />
        ) : null}
        {!result.ok ? (
          <div className="alert alert-error" role="alert">{result.message}</div>
        ) : rows.length === 0 ? (
          <div className="empty-state" role="status">
            {q || status ? (
              "Không có đơn hàng khớp bộ lọc."
            ) : (
              <>
                Chưa có đơn hàng. <Link className="row-link" href="/orders/new">Tạo đơn hàng</Link>.
              </>
            )}
          </div>
        ) : (
          <>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Mã đơn</th>
                    <th>Khách hàng</th>
                    <th>Tuyến</th>
                    <th>Phương thức</th>
                    <th>Nguồn</th>
                    <th>Ngày tạo</th>
                    <th>Trạng thái</th>
                    <th>Bill</th>
                  </tr>
                </thead>
                <tbody>
                  {pageRows.map((o) => (
                    <tr key={o.id}>
                      <td>
                        <Link className="row-link" href={`/operations/orders/${o.id}`}>
                          {o.orderNo}
                        </Link>
                      </td>
                      <td>{o.customerName || "—"}</td>
                      <td>{o.routeCode || "—"}</td>
                      <td>{transportModeLabel(o.transportMode)}</td>
                      <td>{sourceSystemLabel(o.sourceSystem)}</td>
                      <td className="muted small">{formatDateTimeVi(o.createdAt)}</td>
                      <td>{operationalStatusLabel(o.operationalStatus)}</td>
                      <td>{o.relatedBillCount ?? 0}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <ListPagination
              basePath="/orders"
              params={{ q, status }}
              page={page}
              pageSize={pageSize}
              totalCount={rows.length}
              totalPages={pages}
            />
          </>
        )}
      </section>
    </AppShell>
  );
}
