import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { RevenueListWorkspace } from "@/components/RevenueListWorkspace";
import { ListPagination } from "@/components/ListPagination";
import { AnalyticsRow, AnalyticsPanel } from "@/components/list/AnalyticsRow";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { StatCardGrid, type StatCardModel } from "@/components/list/StatCardGrid";
import { FinColors, StackedCompositionBar } from "@/components/charts/FinanceCharts";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listRevenues } from "@/lib/costs-revenues-server";
import {
  parsePage,
  parsePageSize,
  slicePage,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{
  maturity?: string;
  page?: string;
  pageSize?: string;
}>;

function revenuesHref(maturity?: string): string {
  if (!maturity) return "/revenues";
  return `/revenues?maturity=${encodeURIComponent(maturity)}`;
}

export default async function RevenuesPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { maturity, page: pageRaw, pageSize: pageSizeRaw } = await searchParams;
  const maturityFilter =
    maturity === "expected" || maturity === "confirmed" || maturity === "actual"
      ? maturity
      : undefined;

  const terms = await fetchTerminology();
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const profitLabel = term(terms, "PROFIT", "Lợi nhuận");
  const billLabel = term(terms, "BILL", "Bill");
  const expectedLabel = term(terms, "EXPECTED", "Dự kiến");
  const confirmedLabel = term(terms, "CONFIRMED", "Đã xác nhận");
  const actualLabel = term(terms, "ACTUAL", "Thực tế");

  const [result, kpiRes] = await Promise.all([
    listRevenues({ financialMaturity: maturityFilter }),
    listRevenues(),
  ]);
  const kpiItems = kpiRes.ok ? kpiRes.data.items : [];
  const countByMaturity = (m: string) =>
    kpiItems.filter((r) => r.financialMaturity?.toLowerCase() === m).length;
  const sumByMaturity = (m: string) =>
    kpiItems
      .filter((r) => r.financialMaturity?.toLowerCase() === m)
      .reduce((s, r) => s + (r.amount ?? 0), 0);
  const kpiCurrency = kpiItems[0]?.currencyCode ?? "VND";

  const allRows = result.ok ? result.data.items : [];
  const pageSize = parsePageSize(pageSizeRaw);
  const pages = calcTotalPages(
    result.ok ? result.data.totalCount : allRows.length,
    pageSize
  );
  const page = parsePage(pageRaw, pages);
  const pageRows = slicePage(allRows, page, pageSize);
  const totalCount = result.ok ? result.data.totalCount : allRows.length;

  const statCards: StatCardModel[] = [
    { key: "count", label: "Số dòng", value: kpiItems.length },
    {
      key: "expected",
      label: expectedLabel,
      value: formatMoney(sumByMaturity("expected"), kpiCurrency),
    },
    {
      key: "confirmed",
      label: confirmedLabel,
      value: formatMoney(sumByMaturity("confirmed"), kpiCurrency),
    },
    {
      key: "actual",
      label: actualLabel,
      value: formatMoney(sumByMaturity("actual"), kpiCurrency),
    },
    {
      key: "profit",
      label: profitLabel,
      value: (
        <Link className="row-link" href="/reports">
          Xem báo cáo →
        </Link>
      ),
    },
  ];

  return (
    <AppShell terms={terms} active="revenues">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: `${revenueLabel} & ${profitLabel}` },
          ]}
          title={`${revenueLabel} & ${profitLabel}`}
          lede={`${revenueLabel} ≠ hóa đơn / AR / thu tiền. ${profitLabel} suy ra từ chi phí và ${revenueLabel.toLowerCase()} theo ${billLabel}.`}
          action={
            <Link className="btn" href="/bills">
              + Ghi trên {billLabel}
            </Link>
          }
        />

        {kpiRes.ok ? <StatCardGrid cards={statCards} /> : null}

        {kpiRes.ok ? (
          <AnalyticsRow columns={1}>
            <AnalyticsPanel>
              <StackedCompositionBar
                caption={`Phân tách độ chín ${revenueLabel.toLowerCase()} (số dòng)`}
                segments={[
                  {
                    key: "e",
                    label: expectedLabel,
                    value: countByMaturity("expected"),
                    color: FinColors.expected,
                  },
                  {
                    key: "c",
                    label: confirmedLabel,
                    value: countByMaturity("confirmed"),
                    color: FinColors.confirmed,
                  },
                  {
                    key: "a",
                    label: actualLabel,
                    value: countByMaturity("actual"),
                    color: FinColors.actual,
                  },
                ]}
              />
            </AnalyticsPanel>
          </AnalyticsRow>
        ) : null}

        <p className="cta-row" style={{ marginTop: "0.75rem" }}>
          <Link className="btn btn-ghost" href="/reports">
            Báo cáo &amp; Phân tích
          </Link>
        </p>

        <div className="filter-tabs" role="tablist" aria-label="Lọc độ chín">
          <Link className={!maturityFilter ? "active" : undefined} href={revenuesHref()}>
            Tất cả độ chín
          </Link>
          <Link
            className={maturityFilter === "expected" ? "active" : undefined}
            href={revenuesHref("expected")}
          >
            {expectedLabel}
          </Link>
          <Link
            className={maturityFilter === "confirmed" ? "active" : undefined}
            href={revenuesHref("confirmed")}
          >
            {confirmedLabel}
          </Link>
          <Link
            className={maturityFilter === "actual" ? "active" : undefined}
            href={revenuesHref("actual")}
          >
            {actualLabel}
          </Link>
        </div>

        {!result.ok ? (
          <div className="alert alert-error" role="alert">
            {result.message}
          </div>
        ) : allRows.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có dòng {revenueLabel.toLowerCase()}. Mở một {billLabel} và thêm doanh thu
            (Dự kiến → Đã xác nhận → Thực tế).
          </div>
        ) : (
          <>
            <RevenueListWorkspace
              terms={terms}
              revenues={pageRows}
              billLabel={billLabel}
              revenueLabel={revenueLabel}
              expectedLabel={expectedLabel}
              confirmedLabel={confirmedLabel}
              actualLabel={actualLabel}
            />
            <ListPagination
              basePath="/revenues"
              params={{ maturity: maturityFilter }}
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
