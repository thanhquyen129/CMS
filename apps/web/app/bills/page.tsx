import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { BillListWorkspace } from "@/components/BillListWorkspace";
import { ListPagination } from "@/components/ListPagination";
import {
  DeadlineList,
  FilterBar,
  KpiGlyph,
  ListPageHeader,
  MiniWidget,
  RankList,
  StatCardGrid,
} from "@/components/list";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  listBills,
  operationalStatusLabel,
  type BillListItem,
} from "@/lib/bills";
import {
  isoInRange,
  textMatches,
  uniqueSorted,
  upcomingDeadlines,
  topByAmount,
} from "@/lib/list-workspace";
import {
  parsePage,
  parsePageSize,
  slicePage,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";
import { formatMoney } from "@/lib/money";

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

function filterBills(
  items: BillListItem[],
  f: { q?: string; status?: string; from?: string; to?: string; route?: string; customer?: string },
  serverMatchIds?: Set<string>
): BillListItem[] {
  const status = f.status?.trim().toLowerCase();
  const route = f.route?.trim();
  const customer = f.customer?.trim();
  return items.filter((b) => {
    const textOk = textMatches(
      [b.billNo, b.externalId, b.masterBillNo, b.customerReference, b.customerName, b.routeCode],
      f.q
    );
    if (!textOk && !serverMatchIds?.has(b.id)) return false;
    if (status && b.operationalStatus?.toLowerCase() !== status) return false;
    if (!isoInRange(b.createdAt, f.from, f.to)) return false;
    if (route && b.routeCode !== route) return false;
    if (customer && b.customerName !== customer) return false;
    return true;
  });
}

export default async function BillsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

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
  const billLabel = term(terms, "BILL", "Bill");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const costLabel = term(terms, "COST", "Chi phí");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");
  const expectedLabel = term(terms, "EXPECTED", "Dự kiến");
  const confirmedLabel = term(terms, "CONFIRMED", "Đã xác nhận");
  const actualLabel = term(terms, "ACTUAL", "Thực tế");

  const pageSize = parsePageSize(pageSizeRaw);
  const needle = q?.trim();
  const [result, matched] = await Promise.all([
    listBills(),
    needle ? listBills(needle) : Promise.resolve(null),
  ]);
  const all = (result.ok ? result.data.items : [])
    .slice()
    .sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt) || a.billNo.localeCompare(b.billNo));
  const serverMatchIds = matched?.ok
    ? new Set(matched.data.items.map((b) => b.id))
    : undefined;
  const filtered = filterBills(all, { q, status, from, to, route, customer }, serverMatchIds);
  const pages = calcTotalPages(filtered.length, pageSize);
  const page = parsePage(pageRaw, pages);
  const pageRows = slicePage(filtered, page, pageSize);
  const hasFilters = Boolean(q || status || from || to || route || customer);

  let sumRevExpected = 0;
  let sumRevConfirmed = 0;
  let sumRevActual = 0;
  let rollCurrency = "VND";
  for (const b of filtered) {
    if (b.summaryCurrencyCode) rollCurrency = b.summaryCurrencyCode;
    sumRevExpected += b.revenueExpectedTotal ?? 0;
    sumRevConfirmed += b.revenueConfirmedTotal ?? 0;
    sumRevActual += b.revenueActualTotal ?? 0;
  }

  const statusOptions = uniqueSorted(all.map((b) => b.operationalStatus));
  const routeOptions = uniqueSorted(all.map((b) => b.routeCode));
  const customerOptions = uniqueSorted(all.map((b) => b.customerName));

  const deadlines = upcomingDeadlines(filtered, (b) => ({
    code: b.billNo,
    party: b.customerName || "—",
    item: "ETD",
  }));

  const customerTotals = new Map<string, { amount: number; currency: string }>();
  for (const b of filtered) {
    const name = b.customerName?.trim();
    if (!name || b.revenueBestAvailable == null) continue;
    const cur = b.summaryCurrencyCode || rollCurrency;
    const prev = customerTotals.get(name);
    if (!prev) {
      customerTotals.set(name, { amount: b.revenueBestAvailable, currency: cur });
    } else if (prev.currency === cur) {
      prev.amount += b.revenueBestAvailable;
    }
  }
  const topCustomers = topByAmount(
    Array.from(customerTotals.entries()).map(([name, v]) => ({
      name,
      amount: v.amount,
      currency: v.currency,
    }))
  );

  const listParams = { q, status, from, to, route, customer, page: pageRaw, pageSize: pageSizeRaw };

  return (
    <AppShell terms={terms} active="bills" navChild="bills">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: "Đơn hàng vận chuyển" },
            { label: `Danh sách ${billLabel}` },
          ]}
          title={`Danh sách ${billLabel}`}
          lede={`Quản lý vận đơn (${billLabel}) và thông tin tài chính liên quan.`}
          action={
            <Link className="btn" href="/bills/new">
              + Tạo {billLabel}
            </Link>
          }
        />

        <FilterBar
          action="/bills"
          showLabels
          resetHref="/bills"
          fields={[
            {
              kind: "search",
              name: "q",
              label: "Tìm kiếm",
              placeholder: `Số ${billLabel}, khách hàng, tuyến, reference…`,
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
                key: "count",
                label: `Tổng số ${billLabel}`,
                value: filtered.length,
                tone: "primary",
                icon: <KpiGlyph name="doc" />,
                hint: hasFilters ? `Trong ${all.length} bản ghi` : "Trong phạm vi của bạn",
              },
              {
                key: "rev-e",
                label: `${revenueLabel} (${expectedLabel})`,
                value: formatMoney(sumRevExpected, rollCurrency),
                tone: "success",
                icon: <KpiGlyph name="revenue" />,
                hint: "Không cộng gộp đa tiền tệ trên cùng một ô.",
              },
              {
                key: "rev-c",
                label: `${revenueLabel} (${confirmedLabel})`,
                value: formatMoney(sumRevConfirmed, rollCurrency),
                tone: "warning",
                icon: <KpiGlyph name="confirmed" />,
              },
              {
                key: "rev-a",
                label: `${revenueLabel} (${actualLabel})`,
                value: formatMoney(sumRevActual, rollCurrency),
                tone: "info",
                icon: <KpiGlyph name="actual" />,
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
              `Không có ${billLabel} khớp bộ lọc. Thử điều kiện khác.`
            ) : (
              <>
                Chưa có {billLabel} nào trong phạm vi của bạn.{" "}
                <Link className="row-link" href="/bills/new">
                  Tạo {billLabel}
                </Link>
                .
              </>
            )}
          </div>
        ) : (
          <BillListWorkspace
            bills={pageRows}
            filteredCount={filtered.length}
            initialSelectedId={selected ?? null}
            terms={terms}
            listParams={listParams}
            labels={{
              bill: billLabel,
              revenue: revenueLabel,
              cost: costLabel,
              profit: profitLabel,
              expected: expectedLabel,
              confirmed: confirmedLabel,
              actual: actualLabel,
            }}
            pagination={
              <ListPagination
                basePath="/bills"
                params={{ q, status, from, to, route, customer, selected }}
                page={page}
                pageSize={pageSize}
                totalCount={filtered.length}
                totalPages={pages}
              />
            }
            lower={
              <div className="list-workspace-lower">
                <MiniWidget title={`${billLabel} sắp đến hạn ETD`}>
                  <DeadlineList
                    rows={deadlines}
                    hrefFor={(id) => `/bills?selected=${encodeURIComponent(id)}`}
                    empty={`Không có ${billLabel} có ETD trong 14 ngày tới.`}
                  />
                </MiniWidget>
                <MiniWidget title="Top 5 khách hàng theo doanh thu">
                  <RankList
                    rows={topCustomers}
                    empty="Chưa có doanh thu trên bộ lọc hiện tại."
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
