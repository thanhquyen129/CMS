import { cookies } from "next/headers";
import Link from "next/link";
import { redirect } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { ApArListWorkspace } from "@/components/ApArListWorkspace";
import { ListPagination } from "@/components/ListPagination";
import { AnalyticsRow, AnalyticsPanel } from "@/components/list/AnalyticsRow";
import { ListPageHeader } from "@/components/list/ListPageHeader";
import { StatCardGrid, type StatCardModel } from "@/components/list/StatCardGrid";
import { FinColors, HorizontalBarChart } from "@/components/charts/FinanceCharts";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  agingBucketLabel,
  exposureStatusLabel,
  filterByApArStatus,
  getAgingSummary,
  listAccountsPayable,
  listAccountsReceivable,
  listPayableExposures,
  listReceivableExposures,
  parseApArStatusFilter,
  type AccountsPayableItem,
  type AccountsReceivableItem,
  type ApArStatusFilter,
} from "@/lib/ap-ar";
import {
  parsePage,
  parsePageSize,
  slicePage,
  totalPages as calcTotalPages,
} from "@/lib/list-paging";
import { formatMoney } from "@/lib/money";
import type { TerminologyMap } from "@/lib/terminology";

type SearchParams = Promise<{
  tab?: string;
  status?: string;
  page?: string;
  pageSize?: string;
}>;

function apArHref(opts: {
  tab?: string;
  status?: ApArStatusFilter;
}): string {
  const p = new URLSearchParams();
  if (opts.tab && opts.tab !== "ap") p.set("tab", opts.tab);
  if (opts.status && opts.status !== "outstanding") {
    p.set("status", opts.status);
  }
  const qs = p.toString();
  return qs ? `/ap-ar?${qs}` : "/ap-ar";
}

function statusFilterLabel(
  terms: TerminologyMap,
  filter: ApArStatusFilter
): string {
  if (filter === "settled") {
    return term(terms, "SETTLEMENT_SETTLED", "Đã tất toán");
  }
  if (filter === "all") return "Tất cả";
  return term(terms, "OUTSTANDING", "Còn dư");
}

const AGING_BUCKET_ORDER = [
  "current",
  "1_30",
  "31_60",
  "61_90",
  "90_plus",
  "no_due_date",
] as const;

function agingColor(bucket: string): string {
  switch (bucket.toLowerCase()) {
    case "current":
      return FinColors.agingCurrent;
    case "1_30":
      return FinColors.agingMid;
    case "31_60":
    case "61_90":
    case "90_plus":
      return FinColors.agingLate;
    default:
      return FinColors.agingNone;
  }
}

function agingOutstanding(
  buckets: { bucket: string; outstanding: number }[] | undefined,
  keys: readonly string[]
): number {
  if (!buckets) return 0;
  return buckets
    .filter((b) => keys.includes(b.bucket?.toLowerCase()))
    .reduce((s, b) => s + b.outstanding, 0);
}

export default async function ApArPage({
  searchParams,
}: {
  searchParams: SearchParams;
}) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { tab, status: statusRaw, page: pageRaw, pageSize: pageSizeRaw } =
    await searchParams;
  const activeTab =
    tab === "ar" || tab === "exposure" ? tab : "ap";
  const statusFilter = parseApArStatusFilter(statusRaw);

  const terms = await fetchTerminology();
  const apLabel = term(terms, "ACCOUNTS_PAYABLE", "Khoản phải trả");
  const arLabel = term(terms, "ACCOUNTS_RECEIVABLE", "Khoản phải thu");
  const outstandingLabel = term(terms, "OUTSTANDING", "Số dư còn lại");
  const settledLabel = term(terms, "SETTLEMENT_SETTLED", "Đã tất toán");
  const agingLabel = term(terms, "AGING", "Tuổi nợ");
  const payableExposureLabel = term(
    terms,
    "PAYABLE_EXPOSURE",
    "Nghĩa vụ phải trả (exposure)"
  );
  const receivableExposureLabel = term(
    terms,
    "RECEIVABLE_EXPOSURE",
    "Quyền thu dự kiến (exposure)"
  );
  const billLabel = term(terms, "BILL", "Bill");
  const costLabel = term(terms, "COST", "Chi phí");
  const paymentLabel = term(terms, "PAYMENT", "Thanh toán");
  const collectionLabel = term(terms, "COLLECTION", "Thu tiền");
  const revenueLabel = term(terms, "REVENUE", "Doanh thu");

  const [apRes, arRes, peRes, reRes, agingRes] = await Promise.all([
    listAccountsPayable(),
    listAccountsReceivable(),
    listPayableExposures(),
    listReceivableExposures(),
    getAgingSummary(),
  ]);

  const apItems = apRes.ok
    ? filterByApArStatus(apRes.data, statusFilter)
    : [];
  const arItems = arRes.ok
    ? filterByApArStatus(arRes.data, statusFilter)
    : [];
  const apSettledCount = apRes.ok
    ? apRes.data.filter((r) => r.settlementStatus?.toLowerCase() === "settled")
        .length
    : 0;
  const arSettledCount = arRes.ok
    ? arRes.data.filter((r) => r.settlementStatus?.toLowerCase() === "settled")
        .length
    : 0;
  const peItems = peRes.ok
    ? peRes.data.filter((e) => e.openAmount > 0 || e.status !== "recognized")
    : [];
  const reItems = reRes.ok
    ? reRes.data.filter((e) => e.openAmount > 0 || e.status !== "recognized")
    : [];

  const showSettledAmount =
    statusFilter === "settled" || statusFilter === "all";

  const navActive = activeTab === "ar" ? "ar" : "ap";

  // —— Paging (client-driven, GET query) for the active AP or AR table ——
  const activeRows: (AccountsPayableItem | AccountsReceivableItem)[] =
    activeTab === "ar" ? arItems : apItems;
  const pageSize = parsePageSize(pageSizeRaw);
  const pages = calcTotalPages(activeRows.length, pageSize);
  const page = parsePage(pageRaw, pages);
  const pageRows = slicePage(activeRows, page, pageSize);
  const pageParams = { tab: activeTab, status: statusFilter };

  // —— Denser aging KPI (not-due / 0-30 / >30 / settled) when data exists ——
  const agingSide =
    activeTab === "ar" ? agingRes.ok && agingRes.data.receivable : agingRes.ok && agingRes.data.payable;
  const agingBuckets =
    activeTab === "ar"
      ? agingRes.ok
        ? agingRes.data.receivable?.buckets
        : undefined
      : agingRes.ok
        ? agingRes.data.payable?.buckets
        : undefined;
  const agingCurrency =
    (activeTab === "ar"
      ? agingRes.ok && agingRes.data.receivable?.receivableItems?.[0]?.currencyCode
      : agingRes.ok && agingRes.data.payable?.payableItems?.[0]?.currencyCode) ||
    activeRows[0]?.currencyCode ||
    "VND";
  const notDue = agingOutstanding(agingBuckets, ["current", "no_due_date"]);
  const due0to30 = agingOutstanding(agingBuckets, ["1_30"]);
  const dueOver30 = agingOutstanding(agingBuckets, ["31_60", "61_90", "90_plus"]);

  const cashLabel = activeTab === "ar" ? collectionLabel : paymentLabel;
  const cashCreateHref =
    activeTab === "ar" ? "/settlements/collections/new" : "/settlements/payments/new";

  const statCards: StatCardModel[] = [
    {
      key: "count",
      label: `${activeTab === "ar" ? arLabel : apLabel} (đang xem)`,
      value: activeRows.length,
    },
    {
      key: "settled",
      label: `${settledLabel} (${activeTab === "ar" ? "AR" : "AP"})`,
      value: activeTab === "ar" ? arSettledCount : apSettledCount,
    },
  ];
  if (agingSide) {
    statCards.push(
      {
        key: "notdue",
        label: "Trong hạn",
        value: formatMoney(notDue, agingCurrency),
      },
      {
        key: "due30",
        label: "Quá hạn 1–30 ngày",
        value: formatMoney(due0to30, agingCurrency),
      },
      {
        key: "over30",
        label: "Quá hạn > 30 ngày",
        value: formatMoney(dueOver30, agingCurrency),
        tone: dueOver30 > 0 ? "danger" : "default",
      }
    );
  }

  return (
    <AppShell terms={terms} active={navActive}>
      <section className="panel panel-wide">
        <ListPageHeader
          breadcrumbs={[
            { href: "/dashboard", label: "Trang chủ" },
            {
              label:
                activeTab === "ar"
                  ? arLabel
                  : activeTab === "exposure"
                    ? "Exposure"
                    : apLabel,
            },
          ]}
          title={
            <>
              {activeTab === "ar"
                ? arLabel
                : activeTab === "exposure"
                  ? "Exposure"
                  : apLabel}
              {activeTab === "exposure"
                ? ""
                : activeTab === "ar"
                  ? " (AR)"
                  : " (AP)"}
            </>
          }
          lede={
            <>
              Đọc {outstandingLabel} đã ghi nhận và lịch sử {settledLabel.toLowerCase()}.{" "}
              {apLabel} ≠ {costLabel}; {arLabel} ≠ {revenueLabel}. Ghi nhận công nợ không
              tạo Cost/Revenue mới.
            </>
          }
          action={
            <Link className="btn" href="/ap-ar/aging">
              Tuổi nợ
            </Link>
          }
        />

        {activeTab !== "exposure" ? <StatCardGrid cards={statCards} /> : null}

        {activeTab !== "exposure" && agingSide && agingBuckets ? (
          <AnalyticsRow columns={1}>
            <AnalyticsPanel>
              <HorizontalBarChart
                caption={`${agingLabel} · ${activeTab === "ar" ? arLabel : apLabel} (dư nợ)`}
                series={AGING_BUCKET_ORDER.map((key) => {
                  const b = agingBuckets.find(
                    (x) => x.bucket?.toLowerCase() === key
                  );
                  return {
                    key,
                    label: agingBucketLabel(key),
                    value: b?.outstanding ?? 0,
                    color: agingColor(key),
                  };
                })}
                valueFormatter={(n) => formatMoney(n, agingCurrency)}
                emptyLabel={`Không có dư nợ ${activeTab === "ar" ? "AR" : "AP"} trong phạm vi.`}
              />
            </AnalyticsPanel>
          </AnalyticsRow>
        ) : null}

        <div className="toolbar-row" role="group" aria-label="Thao tác AP/AR">
          <Link className="btn btn-sm" href="/settlements">
            {paymentLabel} &amp; thu tiền
          </Link>
          <Link className="btn btn-sm" href="/ap-ar/exposures/new?kind=payable">
            Tạo exposure phải trả
          </Link>
          <Link
            className="btn btn-sm"
            href="/ap-ar/exposures/new?kind=receivable"
          >
            Tạo exposure phải thu
          </Link>
        </div>

        <div className="search-bar" role="tablist" aria-label="Chọn sổ">
          <Link
            className={activeTab === "ap" ? "btn" : "btn btn-ghost"}
            href={apArHref({ tab: "ap", status: statusFilter })}
            role="tab"
            aria-selected={activeTab === "ap"}
          >
            {apLabel}
          </Link>
          <Link
            className={activeTab === "ar" ? "btn" : "btn btn-ghost"}
            href={apArHref({ tab: "ar", status: statusFilter })}
            role="tab"
            aria-selected={activeTab === "ar"}
          >
            {arLabel}
          </Link>
          <Link
            className={activeTab === "exposure" ? "btn" : "btn btn-ghost"}
            href={apArHref({ tab: "exposure" })}
            role="tab"
            aria-selected={activeTab === "exposure"}
          >
            Exposure
          </Link>
        </div>

        {activeTab === "ap" || activeTab === "ar" ? (
          <div
            className="search-bar"
            role="group"
            aria-label="Lọc trạng thái tất toán"
          >
            <Link
              className={
                statusFilter === "outstanding" ? "btn btn-sm" : "btn btn-ghost btn-sm"
              }
              href={apArHref({ tab: activeTab, status: "outstanding" })}
            >
              Còn dư
            </Link>
            <Link
              className={
                statusFilter === "settled" ? "btn btn-sm" : "btn btn-ghost btn-sm"
              }
              href={apArHref({ tab: activeTab, status: "settled" })}
            >
              {settledLabel}
            </Link>
            <Link
              className={
                statusFilter === "all" ? "btn btn-sm" : "btn btn-ghost btn-sm"
              }
              href={apArHref({ tab: activeTab, status: "all" })}
            >
              Tất cả
            </Link>
          </div>
        ) : null}

        {activeTab === "ap" ? (
          <>
            <h2 className="section-title sm">
              {apLabel} — {statusFilterLabel(terms, statusFilter)}
            </h2>
            {!apRes.ok ? (
              <div className="alert alert-error" role="alert">
                {apRes.message}
              </div>
            ) : apItems.length === 0 ? (
              <div className="empty-state" role="status">
                {statusFilter === "outstanding" ? (
                  <>
                    Không có {apLabel.toLowerCase()} còn dư.
                    {apSettledCount > 0 ? (
                      <>
                        {" "}
                        Có {apSettledCount} khoản{" "}
                        <Link
                          className="row-link"
                          href={apArHref({ tab: "ap", status: "settled" })}
                        >
                          {settledLabel.toLowerCase()}
                        </Link>
                        .
                      </>
                    ) : (
                      <> Chưa ghi nhận từ exposure hoặc chưa tất toán.</>
                    )}
                  </>
                ) : statusFilter === "settled" ? (
                  <>Không có {apLabel.toLowerCase()} đã tất toán.</>
                ) : (
                  <>Chưa có {apLabel.toLowerCase()} nào.</>
                )}
              </div>
            ) : (
              <>
                <ApArListWorkspace
                  terms={terms}
                  items={pageRows}
                  kind="payable"
                  billLabel={billLabel}
                  outstandingLabel={outstandingLabel}
                  agingLabel={agingLabel}
                  showSettledAmount={showSettledAmount}
                  cashLabel={cashLabel}
                  cashCreateHref={cashCreateHref}
                />
                <ListPagination
                  basePath="/ap-ar"
                  params={pageParams}
                  page={page}
                  pageSize={pageSize}
                  totalCount={activeRows.length}
                  totalPages={pages}
                />
              </>
            )}
          </>
        ) : null}

        {activeTab === "ar" ? (
          <>
            <h2 className="section-title sm">
              {arLabel} — {statusFilterLabel(terms, statusFilter)}
            </h2>
            {!arRes.ok ? (
              <div className="alert alert-error" role="alert">
                {arRes.message}
              </div>
            ) : arItems.length === 0 ? (
              <div className="empty-state" role="status">
                {statusFilter === "outstanding" ? (
                  <>
                    Không có {arLabel.toLowerCase()} còn dư.
                    {arSettledCount > 0 ? (
                      <>
                        {" "}
                        Có {arSettledCount} khoản{" "}
                        <Link
                          className="row-link"
                          href={apArHref({ tab: "ar", status: "settled" })}
                        >
                          {settledLabel.toLowerCase()}
                        </Link>
                        .
                      </>
                    ) : null}
                  </>
                ) : statusFilter === "settled" ? (
                  <>Không có {arLabel.toLowerCase()} đã tất toán.</>
                ) : (
                  <>Chưa có {arLabel.toLowerCase()} nào.</>
                )}
              </div>
            ) : (
              <>
                <ApArListWorkspace
                  terms={terms}
                  items={pageRows}
                  kind="receivable"
                  billLabel={billLabel}
                  outstandingLabel={outstandingLabel}
                  agingLabel={agingLabel}
                  showSettledAmount={showSettledAmount}
                  cashLabel={cashLabel}
                  cashCreateHref={cashCreateHref}
                />
                <ListPagination
                  basePath="/ap-ar"
                  params={pageParams}
                  page={page}
                  pageSize={pageSize}
                  totalCount={activeRows.length}
                  totalPages={pages}
                />
              </>
            )}
          </>
        ) : null}

        {activeTab === "exposure" ? (
          <>
            <p className="note">
              Exposure = nghĩa vụ / quyền dự kiến trước khi ghi nhận AP/AR.
              Chưa phải {paymentLabel}. Ghi nhận tạo sổ {apLabel}/{arLabel}{" "}
              riêng — không đổi {costLabel}/{revenueLabel}.
            </p>
            <p className="cta-row" style={{ marginTop: 0 }}>
              <Link
                className="btn btn-sm"
                href="/ap-ar/exposures/new?kind=payable"
              >
                Tạo {payableExposureLabel}
              </Link>{" "}
              <Link
                className="btn btn-sm"
                href="/ap-ar/exposures/new?kind=receivable"
              >
                Tạo {receivableExposureLabel}
              </Link>
            </p>

            <h2 className="section-title sm">{payableExposureLabel}</h2>
            {!peRes.ok ? (
              <div className="alert alert-error" role="alert">
                {peRes.message}
              </div>
            ) : peItems.length === 0 ? (
              <div className="empty-state" role="status">
                Không có exposure phải trả còn mở.{" "}
                <Link
                  className="row-link"
                  href="/ap-ar/exposures/new?kind=payable"
                >
                  Tạo exposure
                </Link>
                .
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">{billLabel}</th>
                      <th scope="col" className="num">
                        Số tiền
                      </th>
                      <th scope="col" className="num">
                        Còn mở
                      </th>
                      <th scope="col">Trạng thái</th>
                      <th scope="col">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {peItems.map((row) => (
                      <tr key={row.id}>
                        <td>
                          {row.billId ? (
                            <Link className="row-link" href={`/bills/${row.billId}`}>
                              Mở {billLabel}
                            </Link>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td className="num">
                          {formatMoney(row.amount, row.currencyCode)}
                        </td>
                        <td className="num">
                          {formatMoney(row.openAmount, row.currencyCode)}
                        </td>
                        <td>{exposureStatusLabel(row.status)}</td>
                        <td>
                          {row.openAmount > 0 ? (
                            <Link
                              className="btn btn-sm"
                              href={`/ap-ar/exposures/${row.id}/recognize?kind=payable`}
                            >
                              Ghi nhận → {apLabel}
                            </Link>
                          ) : (
                            <span className="muted small">—</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <h2 className="section-title sm">{receivableExposureLabel}</h2>
            {!reRes.ok ? (
              <div className="alert alert-error" role="alert">
                {reRes.message}
              </div>
            ) : reItems.length === 0 ? (
              <div className="empty-state" role="status">
                Không có exposure phải thu còn mở.{" "}
                <Link
                  className="row-link"
                  href="/ap-ar/exposures/new?kind=receivable"
                >
                  Tạo exposure
                </Link>
                .
              </div>
            ) : (
              <div className="table-wrap">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">{billLabel}</th>
                      <th scope="col" className="num">
                        Số tiền
                      </th>
                      <th scope="col" className="num">
                        Còn mở
                      </th>
                      <th scope="col">Trạng thái</th>
                      <th scope="col">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {reItems.map((row) => (
                      <tr key={row.id}>
                        <td>
                          {row.billId ? (
                            <Link className="row-link" href={`/bills/${row.billId}`}>
                              Mở {billLabel}
                            </Link>
                          ) : (
                            "—"
                          )}
                        </td>
                        <td className="num">
                          {formatMoney(row.amount, row.currencyCode)}
                        </td>
                        <td className="num">
                          {formatMoney(row.openAmount, row.currencyCode)}
                        </td>
                        <td>{exposureStatusLabel(row.status)}</td>
                        <td>
                          {row.openAmount > 0 ? (
                            <Link
                              className="btn btn-sm"
                              href={`/ap-ar/exposures/${row.id}/recognize?kind=receivable`}
                            >
                              Ghi nhận → {arLabel}
                            </Link>
                          ) : (
                            <span className="muted small">—</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        ) : null}
      </section>
    </AppShell>
  );
}
