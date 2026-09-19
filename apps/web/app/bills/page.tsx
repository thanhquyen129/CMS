import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { BillListWorkspace } from "@/components/BillListWorkspace";
import { ListPagination } from "@/components/ListPagination";
import {
  FilterBar,
  ListPageHeader,
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
  parsePage,
  parsePageSize,
  slicePage,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{
  q?: string;
  status?: string;
  selected?: string;
  page?: string;
  pageSize?: string;
}>;

export default async function BillsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { q, status, selected, page: pageRaw, pageSize: pageSizeRaw } =
    await searchParams;
  const terms = await fetchTerminology();
  const billLabel = term(terms, "BILL", "Bill");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const costLabel = term(terms, "COST", "Chi phí");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");
  const expectedLabel = term(terms, "EXPECTED", "Dự kiến");
  const confirmedLabel = term(terms, "CONFIRMED", "Đã xác nhận");
  const actualLabel = term(terms, "ACTUAL", "Thực tế");

  const pageSize = parsePageSize(pageSizeRaw);
  const statusFilter = status?.trim() || undefined;
  const pageHint = Math.max(
    1,
    Number.parseInt(String(pageRaw ?? "1"), 10) || 1
  );

  let result;
  let pageRows: BillListItem[];
  let totalCount: number;
  let page: number;
  let pages: number;
  let kpiBills: BillListItem[];
  let statusOptionsSource: BillListItem[];

  if (statusFilter) {
    // listBills has no status param — fetch unpaged, then client filter + slice.
    result = await listBills(q);
    let bills: BillListItem[] = result.ok ? result.data.items : [];
    const s = statusFilter.toLowerCase();
    bills = bills.filter((b) => b.operationalStatus?.toLowerCase() === s);
    pages = calcTotalPages(bills.length, pageSize);
    page = parsePage(pageRaw, pages);
    pageRows = slicePage(bills, page, pageSize);
    totalCount = bills.length;
    kpiBills = bills;
    statusOptionsSource = result.ok ? result.data.items : [];
  } else {
    result = await listBills(q, { page: pageHint, pageSize });
    totalCount = result.ok ? result.data.totalCount : 0;
    pages = calcTotalPages(totalCount, pageSize);
    page = parsePage(pageRaw, pages);
    pageRows = result.ok ? result.data.items : [];
    kpiBills = pageRows;
    statusOptionsSource = pageRows;
  }

  let sumRevExpected = 0;
  let sumRevConfirmed = 0;
  let sumRevActual = 0;
  let rollCurrency = "VND";
  for (const b of kpiBills) {
    if (b.summaryCurrencyCode) rollCurrency = b.summaryCurrencyCode;
    sumRevExpected += b.revenueExpectedTotal ?? 0;
    sumRevConfirmed += b.revenueConfirmedTotal ?? 0;
    sumRevActual += b.revenueActualTotal ?? 0;
  }

  const statusOptions = Array.from(
    new Set(statusOptionsSource.map((b) => b.operationalStatus).filter(Boolean))
  );

  return (
    <AppShell terms={terms} active="bills">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: "Đơn hàng vận chuyển" },
            { label: `Danh sách ${billLabel}` },
          ]}
          title={`Danh sách ${billLabel}`}
          lede={
            <>
              Quản lý vận đơn ({billLabel}) và thông tin tài chính liên quan —{" "}
              {expectedLabel} / {confirmedLabel} / {actualLabel}. {billLabel} là neo
              tài chính (Financial Anchor). Chọn dòng để xem panel chi tiết.
            </>
          }
          action={
            <Link className="btn" href="/bills/new">
              + Tạo vận đơn
            </Link>
          }
        />

        <FilterBar
          action="/bills"
          resetHref={q || status ? "/bills" : undefined}
          fields={[
            {
              kind: "search",
              name: "q",
              label: `Tìm ${billLabel}`,
              placeholder: `Tìm theo số ${billLabel}, reference…`,
              defaultValue: q,
            },
            {
              kind: "select",
              name: "status",
              label: "Trạng thái",
              defaultValue: status,
              emptyLabel: "Tất cả trạng thái",
              options: statusOptions.map((s) => ({
                value: s,
                label: operationalStatusLabel(s),
              })),
            },
          ]}
        />

        {result.ok ? (
          <StatCardGrid
            cards={[
              {
                key: "count",
                label: `Tổng số ${billLabel}`,
                value: statusFilter ? kpiBills.length : totalCount,
                hint: statusFilter
                  ? result.data.totalCount !== kpiBills.length
                    ? `Trong ${result.data.totalCount} bản ghi`
                    : "Trong phạm vi của bạn"
                  : "Trong phạm vi của bạn",
              },
              {
                key: "rev-e",
                label: `${revenueLabel} (${expectedLabel})`,
                value: formatMoney(sumRevExpected, rollCurrency),
                hint: statusFilter
                  ? "Projection từ API list"
                  : "Trên trang hiện tại",
              },
              {
                key: "rev-c",
                label: `${revenueLabel} (${confirmedLabel})`,
                value: formatMoney(sumRevConfirmed, rollCurrency),
                hint: "Không cộng gộp đa tiền tệ",
              },
              {
                key: "rev-a",
                label: `${revenueLabel} (${actualLabel})`,
                value: formatMoney(sumRevActual, rollCurrency),
                hint: "Theo tiền tệ chính từng Bill",
              },
            ]}
          />
        ) : null}

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : totalCount === 0 ? (
          <div className="empty-state" role="status">
            {q?.trim() || status ? (
              `Không có ${billLabel} khớp bộ lọc. Thử điều kiện khác.`
            ) : (
              <>
                Chưa có {billLabel} nào trong phạm vi của bạn.{" "}
                <Link className="row-link" href="/bills/new">
                  Tạo vận đơn
                </Link>
                .
              </>
            )}
          </div>
        ) : (
          <>
            <BillListWorkspace
              bills={pageRows}
              initialSelectedId={selected ?? null}
              terms={terms}
              listParams={{
                q,
                status,
                page: pageRaw,
                pageSize: pageSizeRaw,
              }}
              labels={{
                bill: billLabel,
                revenue: revenueLabel,
                cost: costLabel,
                profit: profitLabel,
                expected: expectedLabel,
                confirmed: confirmedLabel,
                actual: actualLabel,
              }}
            />
            <ListPagination
              basePath="/bills"
              params={{ q, status }}
              page={page}
              pageSize={pageSize}
              totalCount={totalCount}
              totalPages={pages}
            />
          </>
        )}
      </section>
    </AppShell>
  );
}
