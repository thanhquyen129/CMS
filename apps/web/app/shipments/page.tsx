import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { QuerySelectLink } from "@/components/QuerySelectLink";
import { ShipmentListWorkspace } from "@/components/ShipmentListWorkspace";
import {
  DeadlineList,
  FilterBar,
  KpiGlyph,
  ListPageHeader,
  MiniWidget,
  StatCardGrid,
} from "@/components/list";
import { ListPagination } from "@/components/ListPagination";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology } from "@/lib/api";
import { operationalStatusLabel } from "@/lib/bills-shared";
import {
  isoInRange,
  textMatches,
  uniqueSorted,
  upcomingDeadlines,
} from "@/lib/list-workspace";
import {
  parsePage,
  parsePageSize,
  slicePage,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";
import { listShipments, type ShipmentListItem } from "@/lib/operational-refs";

type SearchParams = Promise<{
  q?: string;
  status?: string;
  from?: string;
  to?: string;
  route?: string;
  selected?: string;
  page?: string;
  pageSize?: string;
}>;

function filterShipments(
  items: ShipmentListItem[],
  f: { q?: string; status?: string; from?: string; to?: string; route?: string }
): ShipmentListItem[] {
  const status = f.status?.trim().toLowerCase();
  const route = f.route?.trim();
  return items.filter((s) => {
    if (
      !textMatches(
        [s.shipmentNo, s.routeCode, s.customerReference, s.originCode, s.destinationCode],
        f.q
      )
    ) {
      return false;
    }
    if (status && s.operationalStatus?.toLowerCase() !== status) return false;
    if (!isoInRange(s.createdAt, f.from, f.to)) return false;
    if (route && s.routeCode !== route) return false;
    return true;
  });
}

export default async function ShipmentsPage({ searchParams }: { searchParams: SearchParams }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");

  const { q, status, from, to, route, selected, page: pageRaw, pageSize: pageSizeRaw } =
    await searchParams;
  const terms = await fetchTerminology();
  const pageSize = parsePageSize(pageSizeRaw);
  const result = await listShipments();
  const all = result.ok ? result.data : [];
  const filtered = filterShipments(all, { q, status, from, to, route });
  const pages = calcTotalPages(filtered.length, pageSize);
  const page = parsePage(pageRaw, pages);
  const pageRows = slicePage(filtered, page, pageSize);
  const hasFilters = Boolean(q || status || from || to || route);

  const draftCount = filtered.filter((r) => r.operationalStatus?.toLowerCase() === "draft").length;
  const linked = filtered.filter((r) => (r.relatedBillCount ?? 0) > 0).length;
  const unlinked = filtered.filter((r) => (r.relatedBillCount ?? 0) === 0);
  const withLegs = filtered.filter((r) => (r.legCount ?? 0) > 0);
  const statusOptions = uniqueSorted(all.map((r) => r.operationalStatus));
  const routeOptions = uniqueSorted(all.map((r) => r.routeCode));

  const deadlines = upcomingDeadlines(filtered, (s) => ({
    code: s.shipmentNo,
    party: s.routeCode || "—",
    item: "ETD",
  }));

  const listParams = { q, status, from, to, route, page: pageRaw, pageSize: pageSizeRaw };

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
          lede="Lô gom / hành trình — điểm tập hợp chi phí và phân bổ xuống Bill."
          action={
            <Link className="btn" href="/shipments/new">
              + Tạo Shipment
            </Link>
          }
        />
        <FilterBar
          action="/shipments"
          showLabels
          resetHref="/shipments"
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm kiếm",
              placeholder: "Số Shipment, tuyến, reference…",
              defaultValue: q,
            },
            {
              kind: "date",
              name: "from",
              label: "Từ ngày",
              defaultValue: from,
            },
            {
              kind: "date",
              name: "to",
              label: "Đến ngày",
              defaultValue: to,
            },
            {
              kind: "select",
              name: "status",
              label: "Trạng thái",
              defaultValue: status,
              emptyLabel: "Tất cả",
              options: statusOptions.map((s) => ({
                value: s,
                label: operationalStatusLabel(s),
              })),
            },
            {
              kind: "select",
              name: "route",
              label: "Tuyến",
              defaultValue: route,
              emptyLabel: "Tất cả",
              options: routeOptions.map((r) => ({ value: r, label: r })),
            },
          ]}
        />
        {result.ok ? (
          <StatCardGrid
            cards={[
              {
                key: "all",
                label: "Tổng số Shipment",
                value: filtered.length,
                tone: "primary",
                icon: <KpiGlyph name="order" />,
                hint: hasFilters ? `Trong ${all.length} bản ghi` : "Trong phạm vi của bạn",
              },
              {
                key: "draft",
                label: "Nháp",
                value: draftCount,
                tone: "warning",
                icon: <KpiGlyph name="draft" />,
              },
              {
                key: "linked",
                label: "Đã gắn Bill",
                value: linked,
                tone: "success",
                icon: <KpiGlyph name="link" />,
              },
              {
                key: "legs",
                label: "Có chặng",
                value: withLegs.length,
                tone: "info",
                icon: <KpiGlyph name="confirmed" />,
              },
            ]}
          />
        ) : null}
        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : filtered.length === 0 ? (
          <div className="empty-state" role="status">
            {hasFilters ? (
              "Không có Shipment khớp bộ lọc."
            ) : (
              <>
                Chưa có Shipment.{" "}
                <Link className="row-link" href="/shipments/new">
                  Tạo Shipment
                </Link>
                .
              </>
            )}
          </div>
        ) : (
          <ShipmentListWorkspace
            shipments={pageRows}
            filteredCount={filtered.length}
            initialSelectedId={selected ?? null}
            listParams={listParams}
            pagination={
              <ListPagination
                basePath="/shipments"
                params={{ q, status, from, to, route, selected }}
                page={page}
                pageSize={pageSize}
                totalCount={filtered.length}
                totalPages={pages}
              />
            }
            lower={
              <div className="list-workspace-lower">
                <MiniWidget title="Shipment chưa gắn Bill">
                  {unlinked.length === 0 ? (
                    <p className="muted" role="status">
                      Mọi Shipment trên bộ lọc đã gắn Bill.
                    </p>
                  ) : (
                    <ul className="stack-list">
                      {unlinked.slice(0, 5).map((s) => (
                        <li key={s.id}>
                          <QuerySelectLink
                            className="row-link"
                            href={`/shipments?selected=${encodeURIComponent(s.id)}`}
                          >
                            {s.shipmentNo}
                          </QuerySelectLink>
                          <div className="muted small">{s.routeCode || "—"}</div>
                        </li>
                      ))}
                    </ul>
                  )}
                </MiniWidget>
                <MiniWidget title="Shipment sắp đến hạn ETD">
                  <DeadlineList
                    rows={deadlines}
                    hrefFor={(id) => `/shipments?selected=${encodeURIComponent(id)}`}
                    partyColumn="Tuyến"
                    empty="Không có Shipment có ETD trong 14 ngày tới."
                  />
                </MiniWidget>
              </div>
            }
          />
        )}
      </section>
    </AppShell>
  );
}
