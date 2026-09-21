import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { OrderListWorkspace } from "@/components/OrderListWorkspace";
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
import { listOrders, type OrderListItem } from "@/lib/operational-refs";

type SearchParams = Promise<{
  q?: string;
  status?: string;
  from?: string;
  to?: string;
  route?: string;
  customer?: string;
  selected?: string;
  page?: string;
  pageSize?: string;
}>;

function filterOrders(
  items: OrderListItem[],
  f: { q?: string; status?: string; from?: string; to?: string; route?: string; customer?: string }
): OrderListItem[] {
  const status = f.status?.trim().toLowerCase();
  const route = f.route?.trim();
  const customer = f.customer?.trim();
  return items.filter((o) => {
    if (!textMatches([o.orderNo, o.customerName, o.routeCode, o.customerReference], f.q)) {
      return false;
    }
    if (status && o.operationalStatus?.toLowerCase() !== status) return false;
    if (!isoInRange(o.createdAt, f.from, f.to)) return false;
    if (route && o.routeCode !== route) return false;
    if (customer && o.customerName !== customer) return false;
    return true;
  });
}

export default async function OrdersPage({ searchParams }: { searchParams: SearchParams }) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) redirect("/login");

  const {
    q,
    status,
    from,
    to,
    route,
    customer,
    selected,
    page: pageRaw,
    pageSize: pageSizeRaw,
  } = await searchParams;
  const terms = await fetchTerminology();
  const pageSize = parsePageSize(pageSizeRaw);
  const result = await listOrders();
  const all = result.ok ? result.data : [];
  const filtered = filterOrders(all, { q, status, from, to, route, customer });
  const pages = calcTotalPages(filtered.length, pageSize);
  const page = parsePage(pageRaw, pages);
  const pageRows = slicePage(filtered, page, pageSize);
  const hasFilters = Boolean(q || status || from || to || route || customer);

  const draftCount = filtered.filter((r) => r.operationalStatus?.toLowerCase() === "draft").length;
  const linked = filtered.filter((r) => (r.relatedBillCount ?? 0) > 0).length;
  const unlinked = filtered.filter((r) => (r.relatedBillCount ?? 0) === 0);
  const statusOptions = uniqueSorted(all.map((r) => r.operationalStatus));
  const routeOptions = uniqueSorted(all.map((r) => r.routeCode));
  const customerOptions = uniqueSorted(all.map((r) => r.customerName));

  const deadlines = upcomingDeadlines(filtered, (o) => ({
    code: o.orderNo,
    party: o.customerName || "—",
    item: "ETD",
  }));

  const listParams = { q, status, from, to, route, customer, page: pageRaw, pageSize: pageSizeRaw };

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
          lede="Tham chiếu vận hành của khách hàng — neo cho Bill và Shipment."
          action={
            <Link className="btn" href="/orders/new">
              + Tạo đơn hàng
            </Link>
          }
        />
        <FilterBar
          action="/orders"
          showLabels
          resetHref="/orders"
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm kiếm",
              placeholder: "Số đơn, khách hàng, tuyến, reference…",
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
            {
              kind: "select",
              name: "customer",
              label: "Khách hàng",
              defaultValue: customer,
              emptyLabel: "Tất cả",
              options: customerOptions.map((c) => ({ value: c, label: c })),
            },
          ]}
        />
        {result.ok ? (
          <StatCardGrid
            cards={[
              {
                key: "all",
                label: "Tổng số đơn",
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
                key: "unlinked",
                label: "Chưa gắn Bill",
                value: unlinked.length,
                tone: "info",
                icon: <KpiGlyph name="unlink" />,
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
              "Không có đơn hàng khớp bộ lọc."
            ) : (
              <>
                Chưa có đơn hàng.{" "}
                <Link className="row-link" href="/orders/new">
                  Tạo đơn hàng
                </Link>
                .
              </>
            )}
          </div>
        ) : (
          <OrderListWorkspace
            orders={pageRows}
            filteredCount={filtered.length}
            initialSelectedId={selected ?? null}
            listParams={listParams}
            pagination={
              <ListPagination
                basePath="/orders"
                params={{ q, status, from, to, route, customer, selected }}
                page={page}
                pageSize={pageSize}
                totalCount={filtered.length}
                totalPages={pages}
              />
            }
            lower={
              <div className="list-workspace-lower">
                <MiniWidget title="Đơn chưa gắn Bill">
                  {unlinked.length === 0 ? (
                    <p className="muted" role="status">
                      Mọi đơn trên bộ lọc đã gắn Bill.
                    </p>
                  ) : (
                    <ul className="stack-list">
                      {unlinked.slice(0, 5).map((o) => (
                        <li key={o.id}>
                          <Link className="row-link" href={`/orders?selected=${encodeURIComponent(o.id)}`}>
                            {o.orderNo}
                          </Link>
                          <div className="muted small">
                            {o.customerName || "—"} · {o.routeCode || "—"}
                          </div>
                        </li>
                      ))}
                    </ul>
                  )}
                </MiniWidget>
                <MiniWidget title="Đơn sắp đến hạn ETD">
                  <DeadlineList
                    rows={deadlines}
                    hrefFor={(id) => `/orders?selected=${encodeURIComponent(id)}`}
                    empty="Không có đơn có ETD trong 14 ngày tới."
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
