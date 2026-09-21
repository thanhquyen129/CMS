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
import { listShipments, sourceSystemLabel } from "@/lib/operational-refs";

type SearchParams = Promise<{ q?: string; status?: string; page?: string; pageSize?: string }>;

export default async function ShipmentsPage({ searchParams }: { searchParams: SearchParams }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");

  const { q, status, page: pageRaw, pageSize: pageSizeRaw } = await searchParams;
  const terms = await fetchTerminology();
  const pageSize = parsePageSize(pageSizeRaw);
  const result = await listShipments(q);
  let rows = result.ok ? result.data : [];
  if (status?.trim()) {
    const s = status.trim().toLowerCase();
    rows = rows.filter((r) => r.operationalStatus?.toLowerCase() === s);
  }
  const pages = calcTotalPages(rows.length, pageSize);
  const page = parsePage(pageRaw, pages);
  const pageRows = slicePage(rows, page, pageSize);
  const linked = rows.filter((r) => (r.relatedBillCount ?? 0) > 0).length;
  const withLegs = rows.filter((r) => (r.legCount ?? 0) > 0).length;
  const statusOptions = Array.from(new Set(rows.map((r) => r.operationalStatus).filter(Boolean)));

  return (
    <AppShell terms={terms} active="bills" navChild="shipments">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { href: "/bills", label: "Đơn hàng vận chuyển" },
            { label: "Danh sách Shipment" },
          ]}
          title="Danh sách Shipment"
          lede="Lô gom/hành trình — điểm tập hợp chi phí và phân bổ xuống Bill. Không điều phối chuyến."
          action={
            <Link className="btn" href="/shipments/new">
              + Tạo Shipment
            </Link>
          }
        />
        <FilterBar
          action="/shipments"
          resetHref={q || status ? "/shipments" : undefined}
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm Shipment",
              placeholder: "Số Shipment, reference, tuyến…",
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
              { key: "all", label: "Tổng số Shipment", value: rows.length, tone: "primary" },
              { key: "bills", label: "Đã gắn Bill", value: linked, tone: "success" },
              { key: "legs", label: "Có chặng", value: withLegs, tone: "info" },
              { key: "page", label: "Trên trang này", value: pageRows.length },
            ]}
          />
        ) : null}
        {!result.ok ? (
          <div className="alert alert-error" role="alert">{result.message}</div>
        ) : rows.length === 0 ? (
          <div className="empty-state" role="status">
            {q || status ? (
              "Không có Shipment khớp bộ lọc."
            ) : (
              <>
                Chưa có Shipment. <Link className="row-link" href="/shipments/new">Tạo Shipment</Link>.
              </>
            )}
          </div>
        ) : (
          <>
            <div className="table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Mã Shipment</th>
                    <th>Tuyến</th>
                    <th>Phương thức</th>
                    <th>Nguồn</th>
                    <th>Ngày tạo</th>
                    <th>Trạng thái</th>
                    <th>Bill</th>
                    <th>Chặng</th>
                  </tr>
                </thead>
                <tbody>
                  {pageRows.map((s) => (
                    <tr key={s.id}>
                      <td>
                        <Link className="row-link" href={`/operations/shipments/${s.id}`}>
                          {s.shipmentNo}
                        </Link>
                      </td>
                      <td>{s.routeCode || "—"}</td>
                      <td>{transportModeLabel(s.transportMode)}</td>
                      <td>{sourceSystemLabel(s.sourceSystem)}</td>
                      <td className="muted small">{formatDateTimeVi(s.createdAt)}</td>
                      <td>{operationalStatusLabel(s.operationalStatus)}</td>
                      <td>{s.relatedBillCount ?? 0}</td>
                      <td>{s.legCount ?? 0}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <ListPagination
              basePath="/shipments"
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
