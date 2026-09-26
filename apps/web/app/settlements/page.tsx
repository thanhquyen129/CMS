import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { SettlementListWorkspace } from "@/components/SettlementListWorkspace";
import { ListPagination } from "@/components/ListPagination";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { StatCardGrid, type StatCardModel } from "@/components/list/StatCardGrid";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import { listCollections, listPayments } from "@/lib/settlements";
import {
  parsePage,
  parsePageSize,
  slicePage,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";
import { formatMoney } from "@/lib/money";

type SearchParams = Promise<{ tab?: string; page?: string; pageSize?: string }>;

export default async function SettlementsPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { tab, page: pageRaw, pageSize: pageSizeRaw } = await searchParams;
  const activeTab = tab === "collections" ? "collections" : "payments";
  const pageSize = parsePageSize(pageSizeRaw);

  const terms = await fetchTerminology();
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const unappliedLabel = term(terms, "UNAPPLIED_AMOUNT", "Chưa áp dụng");
  const availableLabel = term(
    terms,
    "AVAILABLE_TO_ALLOCATE",
    "Còn phân bổ được"
  );
  const costLabel = term(terms, "COST", "Chi phí");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");
  const billLabel = term(terms, "BILL", "Bill");

  const [payRes, colRes] = await Promise.all([
    listPayments(),
    listCollections(),
  ]);

  const payments = payRes.ok ? payRes.data : [];
  const collections = colRes.ok ? colRes.data : [];
  const activeRows = activeTab === "collections" ? collections : payments;
  const activeRes = activeTab === "collections" ? colRes : payRes;
  const activeLabel = activeTab === "collections" ? collectionLabel : paymentLabel;
  const pages = calcTotalPages(activeRows.length, pageSize);
  const page = parsePage(pageRaw, pages);
  const pageRows = slicePage(activeRows, page, pageSize);
  const activeCurrency = activeRows[0]?.currencyCode ?? "VND";
  const totalAmount = activeRows.reduce((s, r) => s + r.amount, 0);
  const totalAllocated = activeRows.reduce((s, r) => s + r.allocatedAmount, 0);
  const totalUnapplied = activeRows.reduce((s, r) => s + r.unappliedAmount, 0);

  const statCards: StatCardModel[] = [
    { key: "count", label: `Số ${activeLabel.toLowerCase()}`, value: activeRows.length },
    {
      key: "amount",
      label: "Tổng số tiền",
      value: formatMoney(totalAmount, activeCurrency),
    },
    {
      key: "allocated",
      label: "Đã phân bổ",
      value: formatMoney(totalAllocated, activeCurrency),
    },
    {
      key: "unapplied",
      label: unappliedLabel,
      value: formatMoney(totalUnapplied, activeCurrency),
      tone: totalUnapplied > 0 ? "danger" : "default",
    },
  ];

  return (
    <AppShell terms={terms} active="settlements">
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            { label: `${paymentLabel} & ${collectionLabel}` },
          ]}
          title={`${paymentLabel} & ${collectionLabel}`}
          lede={
            <>
              {paymentLabel} ≠ {costLabel}; {collectionLabel} ≠ {revenueLabel}. Phân
              bổ nháp rồi <strong>chốt phân bổ</strong> mới giảm outstanding AP/AR.
            </>
          }
          action={
            activeTab === "payments" ? (
              <Link className="btn" href="/settlements/payments/new">
                + Tạo {paymentLabel.toLowerCase()}
              </Link>
            ) : (
              <Link className="btn" href="/settlements/collections/new">
                + Tạo {collectionLabel.toLowerCase()}
              </Link>
            )
          }
        />

        <div className="filter-tabs" role="tablist" aria-label="Loại dòng tiền">
          <Link
            className={activeTab === "payments" ? "active" : undefined}
            href="/settlements"
            role="tab"
            aria-selected={activeTab === "payments"}
          >
            {paymentLabel}
          </Link>
          <Link
            className={activeTab === "collections" ? "active" : undefined}
            href="/settlements?tab=collections"
            role="tab"
            aria-selected={activeTab === "collections"}
          >
            {collectionLabel}
          </Link>
        </div>

        {activeRes.ok && activeRows.length > 0 ? (
          <StatCardGrid cards={statCards} />
        ) : null}

        {!activeRes.ok ? (
          <div className="alert alert-error" role="alert">
            {activeRes.message}
          </div>
        ) : activeRows.length === 0 ? (
          <div className="empty-state" role="status">
            Chưa có {activeLabel.toLowerCase()}. Tạo mới rồi phân bổ vào{" "}
            {activeTab === "collections" ? "AR" : "AP"}.
          </div>
        ) : (
          <SettlementListWorkspace
            terms={terms}
            items={pageRows}
            kind={activeTab === "collections" ? "collection" : "payment"}
            billLabel={billLabel}
            unappliedLabel={unappliedLabel}
            availableLabel={availableLabel}
          />
          <ListPagination
            basePath="/settlements"
            params={{ tab: activeTab === "collections" ? "collections" : undefined }}
            page={page}
            pageSize={pageSize}
            totalCount={activeRows.length}
            totalPages={pages}
          />
        )}
      </section>
    </AppShell>
  );
}
